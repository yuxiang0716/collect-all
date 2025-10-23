// �ɮ�: Services/PowerLogService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// �}�����O���A��
    /// </summary>
    public class PowerLogService
    {
        /// <summary>
        /// �����̪� N �Ѫ��}�����O��
        /// </summary>
        public List<PowerLog> GetPowerLogs(int days = 30)
        {
            LogService.Log($"[PowerLogService] �}�l�����̪� {days} �Ѫ��}�����O��...");
            
            var powerLogs = new List<PowerLog>();
            DateTime startTime = DateTime.Now.AddDays(-days);

            string queryString =
                $"*[System[(" +
                "(Provider[@Name='Microsoft-Windows-Kernel-General'] and (EventID=12 or EventID=13))" +
                $") and TimeCreated[timediff(@SystemTime) <= {DateTime.Now.Subtract(startTime).TotalMilliseconds}]]]";

            EventLogQuery query = new EventLogQuery("System", PathType.LogName, queryString);

            try
            {
                EventLogReader reader = new EventLogReader(query);
                EventRecord? record;

                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        if (record.TimeCreated.HasValue)
                        {
                            string action = record.Id switch
                            {
                                12 => "Startup",   // �}��
                                13 => "Shutdown",  // ����
                                _ => "Unknown"
                            };

                            powerLogs.Add(new PowerLog
                            {
                                Timestamp = record.TimeCreated.Value.ToLocalTime(),
                                Action = action,
                                DeviceNo = string.Empty  // �y��|�]�w
                            });
                        }
                    }
                }

                LogService.Log($"[PowerLogService] ���������A�@ {powerLogs.Count} ���}�����O��");
            }
            catch (EventLogException ex)
            {
                LogService.Log($"[PowerLogService] ? Ū���ƥ��x�ɵo�Ϳ��~�G{ex.Message}");
                LogService.Log("[PowerLogService] ���ܡG�нT�{�{���O�H�t�κ޲z����������");
            }
            catch (Exception ex)
            {
                LogService.Log($"[PowerLogService] ? �����}�����O���ɵo�Ϳ��~�G{ex.Message}");
            }

            return powerLogs;
        }

        /// <summary>
        /// �W�Ƕ}�����O�����Ʈw�]�u�W�Ƿs�O���A�u�ƪ��^
        /// </summary>
        public async Task UploadPowerLogsAsync(string deviceNo, List<PowerLog> powerLogs)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[PowerLogService] �˸m�s�����šA�L�k�W�Ƕ}�����O��");
                return;
            }

            if (powerLogs == null || powerLogs.Count == 0)
            {
                LogService.Log("[PowerLogService] �S���}�����O���i�W��");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[PowerLogService] ��Ʈw�s�u���\");

                    // �]�w�Ҧ��O���� DeviceNo
                    foreach (var log in powerLogs)
                    {
                        log.DeviceNo = deviceNo;
                    }

                    // �u�ơG���o�n�W�Ǫ��O�����ɶ��d��
                    var minTimestamp = powerLogs.Min(p => p.Timestamp);
                    var maxTimestamp = powerLogs.Max(p => p.Timestamp);

                    LogService.Log($"[PowerLogService] �ǳƤW�Ǯɶ��d��G{minTimestamp:yyyy-MM-dd HH:mm:ss} �� {maxTimestamp:yyyy-MM-dd HH:mm:ss}");

                    // �d�߸Ӯɶ��d�򤺤w�s�b���O���]�ϥνƦX��GDeviceNo + Timestamp + Action�^
                    var existingRecords = await db.PowerLogs
                        .Where(p => p.DeviceNo == deviceNo 
                                 && p.Timestamp >= minTimestamp 
                                 && p.Timestamp <= maxTimestamp)
                        .Select(p => new { p.Timestamp, p.Action })
                        .ToListAsync();

                    // �إ� HashSet �Ω�ֳt�d��]�ϥ� Timestamp + Action �ƦX��^
                    var existingKeys = new HashSet<string>(
                        existingRecords.Select(r => $"{r.Timestamp:yyyy-MM-dd HH:mm:ss}|{r.Action}")
                    );

                    LogService.Log($"[PowerLogService] ��Ʈw���Ӯɶ��d�򤺤w�� {existingKeys.Count} ���O��");

                    // �u�W�Ƿs�O���]�ϥ� Timestamp + Action �ƦX����^
                    var newLogs = powerLogs
                        .Where(log => {
                            string key = $"{log.Timestamp:yyyy-MM-dd HH:mm:ss}|{log.Action}";
                            return !existingKeys.Contains(key);
                        })
                        .ToList();

                    int duplicateCount = powerLogs.Count - newLogs.Count;
                    if (duplicateCount > 0)
                    {
                        LogService.Log($"[PowerLogService] ���L {duplicateCount} ���w�s�b���O��");
                    }

                    if (newLogs.Count > 0)
                    {
                        await db.PowerLogs.AddRangeAsync(newLogs);
                        int savedCount = await db.SaveChangesAsync();
                        LogService.Log($"[PowerLogService] ? ���\�W�� {savedCount} ���s���}�����O���]�@���� {powerLogs.Count} ���A���L {duplicateCount} �����ơ^");
                        
                        // ��̦ܳ��M�̷s���O��
                        var earliest = newLogs.Min(l => l.Timestamp);
                        var latest = newLogs.Max(l => l.Timestamp);
                        LogService.Log($"[PowerLogService] �W�ǽd��G{earliest:yyyy-MM-dd HH:mm:ss} �� {latest:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        LogService.Log($"[PowerLogService] ? �Ҧ��O�����w�s�b�]�@���� {powerLogs.Count} ���A�������L�^�A�S���s�O���ݭn�W��");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[PowerLogService] ? �W�Ƕ}�����O���ɵo�Ϳ��~�G{ex.GetType().Name}");
                LogService.Log($"[PowerLogService] ���~�T���G{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[PowerLogService] �������~�G{ex.InnerException.Message}");
                }
            }
        }
    }
}
