// �ɮ�: Services/DiskInfoService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// �Ϻи�T�A�� - �B�z�Ϻи�T�W�ǻP��s
    /// </summary>
    public class DiskInfoService
    {
        /// <summary>
        /// �����Ҧ��ϺмѸ�T�]�Ω�W�ǡ^
        /// </summary>
        public List<DiskInfo> CollectDiskInfos(string deviceNo)
        {
            var diskInfoList = new List<DiskInfo>();
            
            try
            {
                LogService.Log("[DiskInfoService] �}�l�����ϺмѸ�T");
                
                // ���o�Ҧ��ǳƦn���Ϻо�
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                
                LogService.Log($"[DiskInfoService] ��� {drives.Count} �ӺϺм�");
                
                foreach (var drive in drives)
                {
                    try
                    {
                        // ���o�ӺϺмѩҦb������w�ЦW��
                        string physicalDiskName = GetPhysicalDiskName(drive.Name);
                        
                        var diskInfo = new DiskInfo
                        {
                            DeviceNo = deviceNo,
                            SlotName = drive.Name,  // �Ҧp: C:\
                            TotalCapacityGB = (float)(drive.TotalSize / 1024.0 / 1024 / 1024),
                            AvailableCapacityGB = (float)(drive.AvailableFreeSpace / 1024.0 / 1024 / 1024),
                            DeviceInfoDeviceNo = physicalDiskName  // �Ȯɦs��ӼѩҦb���w�ЦW��
                        };
                        
                        diskInfoList.Add(diskInfo);
                        
                        LogService.Log($"[DiskInfoService] �����Ϻм�: {drive.Name}, �`�e�q: {diskInfo.TotalCapacityGB:F2} GB, �i��: {diskInfo.AvailableCapacityGB:F2} GB, �w��: {physicalDiskName}");
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"[DiskInfoService] �����Ϻм� {drive.Name} �ɵo�Ϳ��~: {ex.Message}");
                    }
                }
                
                LogService.Log($"[DiskInfoService] ? �ϺмѸ�T���������A�@ {diskInfoList.Count} ��");
            }
            catch (Exception ex)
            {
                LogService.Log($"[DiskInfoService] ? �����ϺмѸ�T�ɵo�Ϳ��~�G{ex.Message}");
            }
            
            return diskInfoList;
        }
        
        /// <summary>
        /// ���o�ϺмѩҦb������w�ЦW�١]�ϥ� WMI �d�ߡ^
        /// </summary>
        private string GetPhysicalDiskName(string driveLetter)
        {
            try
            {
                // ���� :\ �u�O�d�Ϻо��N�� (�Ҧp C:\ -> C)
                string driveLetterOnly = driveLetter.TrimEnd('\\', ':');
                
                // �ϥ� WMI �d�߸ӺϺмѹ���������w��
                using (var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetterOnly}:'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                {
                    foreach (ManagementObject partition in partitionSearcher.Get())
                    {
                        using (var diskSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                        {
                            foreach (ManagementObject disk in diskSearcher.Get())
                            {
                                string model = disk["Model"]?.ToString() ?? "Unknown";
                                return model;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[DiskInfoService] �L�k���o {driveLetter} ������w�ЦW��: {ex.Message}");
            }
            
            return "Unknown";
        }
        
        /// <summary>
        /// �W�ǩΧ�s�Ϻи�T
        /// �޿�G
        /// 1. �����ƤW�ǡ]�p�G��ƬۦP�h���L�^
        /// 2. ���R���¸��
        /// 3. �u�R���u��Ʈw�����q���w���s�b�v���Ϻм�
        /// </summary>
        public async Task UploadOrUpdateDiskInfosAsync(string deviceNo, List<DiskInfo> currentDiskInfos)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[DiskInfoService] �˸m�s�����šA�L�k�W�ǺϺи�T");
                return;
            }
            
            if (currentDiskInfos == null || currentDiskInfos.Count == 0)
            {
                LogService.Log("[DiskInfoService] �Ϻи�T���šA�L�k�W��");
                return;
            }
            
            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[DiskInfoService] ��Ʈw�s�u���\");
                    
                    // ���o��Ʈw���Ӹ˸m���Ҧ��ϺмѰO��
                    var existingDisks = await db.DiskInfos
                        .Where(d => d.DeviceNo == deviceNo)
                        .ToListAsync();
                    
                    LogService.Log($"[DiskInfoService] ��Ʈw���{�� {existingDisks.Count} ���ϺмѰO��");
                    
                    // ���o�ثe�q���s�b���ϺмѦW�٦C��
                    var currentSlotNames = currentDiskInfos.Select(d => d.SlotName).ToHashSet();
                    
                    // 1. �R���u��Ʈw�����q���w���s�b�v���Ϻм�
                    var disksToDelete = existingDisks
                        .Where(d => !currentSlotNames.Contains(d.SlotName))
                        .ToList();
                    
                    if (disksToDelete.Count > 0)
                    {
                        db.DiskInfos.RemoveRange(disksToDelete);
                        LogService.Log($"[DiskInfoService] ? �R�� {disksToDelete.Count} �Ӥw���s�b���ϺмѡG{string.Join(", ", disksToDelete.Select(d => d.SlotName))}");
                    }
                    
                    // 2. ��s�ηs�W�ϺмѸ�T
                    int updatedCount = 0;
                    int addedCount = 0;
                    int unchangedCount = 0;
                    
                    foreach (var currentDisk in currentDiskInfos)
                    {
                        var existingDisk = existingDisks.FirstOrDefault(d => d.SlotName == currentDisk.SlotName);
                        
                        if (existingDisk == null)
                        {
                            // �s�W�Ϻм�
                            await db.DiskInfos.AddAsync(currentDisk);
                            addedCount++;
                            LogService.Log($"[DiskInfoService] + �s�W�Ϻм�: {currentDisk.SlotName}, {currentDisk.TotalCapacityGB:F2} GB, �w��: {currentDisk.DeviceInfoDeviceNo}");
                        }
                        else
                        {
                            // �ˬd�O�_���ܰ�
                            if (HasDiskInfoChanged(existingDisk, currentDisk))
                            {
                                // ��s��T
                                existingDisk.TotalCapacityGB = currentDisk.TotalCapacityGB;
                                existingDisk.AvailableCapacityGB = currentDisk.AvailableCapacityGB;
                                existingDisk.DeviceInfoDeviceNo = currentDisk.DeviceInfoDeviceNo;
                                updatedCount++;
                                LogService.Log($"[DiskInfoService] ? ��s�Ϻм�: {currentDisk.SlotName}, �`�e�q: {currentDisk.TotalCapacityGB:F2} GB, �i��: {currentDisk.AvailableCapacityGB:F2} GB");
                            }
                            else
                            {
                                unchangedCount++;
                            }
                        }
                    }
                    
                    // �x�s�ܧ�
                    int savedCount = await db.SaveChangesAsync();
                    
                    LogService.Log($"[DiskInfoService] ? �Ϻи�T�W�ǧ��� (�˸m: {deviceNo})");
                    LogService.Log($"[DiskInfoService]   - �s�W: {addedCount} ��");
                    LogService.Log($"[DiskInfoService]   - ��s: {updatedCount} ��");
                    LogService.Log($"[DiskInfoService]   - ����: {unchangedCount} ��");
                    LogService.Log($"[DiskInfoService]   - �R��: {disksToDelete.Count} ��");
                    LogService.Log($"[DiskInfoService]   - �`�@�x�s: {savedCount} ���ܧ�");
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[DiskInfoService] ? �W�ǺϺи�T�ɵo�Ϳ��~�G{ex.GetType().Name}");
                LogService.Log($"[DiskInfoService] ���~�T���G{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[DiskInfoService] �������~�G{ex.InnerException.Message}");
                }
            }
        }
        
        /// <summary>
        /// �ˬd�Ϻи�T�O�_���ܰ�
        /// </summary>
        private bool HasDiskInfoChanged(DiskInfo existing, DiskInfo current)
        {
            // ����`�e�q�]�e�\ 0.1 GB �~�t�^
            bool capacityChanged = Math.Abs(existing.TotalCapacityGB - current.TotalCapacityGB) > 0.1f;
            
            // ����i�ΪŶ��]�e�\ 1 GB �~�t�A�]���i�ΪŶ��g�`�ܰʡ^
            bool availableChanged = Math.Abs(existing.AvailableCapacityGB - current.AvailableCapacityGB) > 1.0f;
            
            // ����w�ЦW��
            bool deviceChanged = existing.DeviceInfoDeviceNo != current.DeviceInfoDeviceNo;
            
            return capacityChanged || availableChanged || deviceChanged;
        }
    }
}
