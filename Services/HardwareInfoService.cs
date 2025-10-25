// �ɮ�: Services/HardwareInfoService.cs
using System;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// �w���T�A�� - �B�z�w���T���W�ǻP��s
    /// </summary>
    public class HardwareInfoService
    {
        /// <summary>
        /// �W�ǩΧ�s�w���T�]�ϥΤw��������ơA�קK���Ƭd�ߡ^
        /// </summary>
        public async Task UploadOrUpdateHardwareInfoAsync(string deviceNo, HardwareInfo currentInfo)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[HardwareInfoService] �˸m�s�����šA�L�k�W�ǵw���T");
                return;
            }

            if (currentInfo == null)
            {
                LogService.Log("[HardwareInfoService] �w���T���šA�L�k�W��");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[HardwareInfoService] ��Ʈw�s�u���\");

                    // �O���ǳƤW�Ǫ��w���T
                    LogService.Log($"[HardwareInfoService] �w���T�G");
                    LogService.Log($"  - �B�z��: {currentInfo.Processor}");
                    LogService.Log($"  - �D���O: {currentInfo.Motherboard}");
                    LogService.Log($"  - �O�����`�e�q: {currentInfo.MemoryTotalGB:F2} GB");
                    LogService.Log($"  - �O����i�ζq: {currentInfo.MemoryAvailableGB:F2} GB");
                    LogService.Log($"  - IP ��}: {currentInfo.IPAddress}");

                    // �d�߸�Ʈw���O�_�w�����˸m���O��
                    var existingInfo = await db.HardwareInfos
                        .FirstOrDefaultAsync(h => h.DeviceNo == deviceNo);

                    if (existingInfo == null)
                    {
                        // �s�W�O��
                        currentInfo.DeviceNo = deviceNo;
                        currentInfo.CreateDate = DateTime.Now;
                        currentInfo.UpdateDate = DateTime.Now;

                        await db.HardwareInfos.AddAsync(currentInfo);
                        await db.SaveChangesAsync();

                        LogService.Log($"[HardwareInfoService] ? �s�W�w���T�O���]�˸m�s��: {deviceNo}�^");
                    }
                    else
                    {
                        // �ˬd�O�_���ܰ�
                        bool hasChanged = HasHardwareChanged(existingInfo, currentInfo);

                        if (hasChanged)
                        {
                            // ��s�O���]�O�d CreateDate�^
                            existingInfo.Processor = currentInfo.Processor;
                            existingInfo.Motherboard = currentInfo.Motherboard;
                            existingInfo.MemoryTotalGB = currentInfo.MemoryTotalGB;
                            existingInfo.MemoryAvailableGB = currentInfo.MemoryAvailableGB;
                            existingInfo.IPAddress = currentInfo.IPAddress;
                            existingInfo.UpdateDate = DateTime.Now;

                            await db.SaveChangesAsync();

                            LogService.Log($"[HardwareInfoService] ? �w���T�w�ܰʡA�w��s�O���]�˸m�s��: {deviceNo}�^");
                            LogService.Log($"[HardwareInfoService] �ܰʤ��e�G");
                            LogComparisonDetails(existingInfo, currentInfo);
                        }
                        else
                        {
                            LogService.Log($"[HardwareInfoService] ? �w���T�L�ܰʡA���ݧ�s�]�˸m�s��: {deviceNo}�^");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[HardwareInfoService] ? �W�ǵw���T�ɵo�Ϳ��~�G{ex.GetType().Name}");
                LogService.Log($"[HardwareInfoService] ���~�T���G{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[HardwareInfoService] �������~�G{ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// �ˬd�w���T�O�_���ܰ�
        /// </summary>
        private bool HasHardwareChanged(HardwareInfo existing, HardwareInfo current)
        {
            return existing.Processor != current.Processor ||
                   existing.Motherboard != current.Motherboard ||
                   Math.Abs(existing.MemoryTotalGB - current.MemoryTotalGB) > 0.01f ||
                   existing.IPAddress != current.IPAddress;
            // �`�N�GMemoryAvailableGB �|�`�`�ܰʡA�ҥH�����
        }

        /// <summary>
        /// �O������ԲӸ�T
        /// </summary>
        private void LogComparisonDetails(HardwareInfo existing, HardwareInfo current)
        {
            if (existing.Processor != current.Processor)
                LogService.Log($"  - �B�z��: {existing.Processor} �� {current.Processor}");

            if (existing.Motherboard != current.Motherboard)
                LogService.Log($"  - �D���O: {existing.Motherboard} �� {current.Motherboard}");

            if (Math.Abs(existing.MemoryTotalGB - current.MemoryTotalGB) > 0.01f)
                LogService.Log($"  - �O�����`�e�q: {existing.MemoryTotalGB:F2} GB �� {current.MemoryTotalGB:F2} GB");

            if (existing.IPAddress != current.IPAddress)
                LogService.Log($"  - IP ��}: {existing.IPAddress} �� {current.IPAddress}");
        }
    }
}
