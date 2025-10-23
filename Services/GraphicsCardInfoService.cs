// �ɮ�: Services/GraphicsCardInfoService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// ��d��T�A�� - �B�z��d��T���W�ǻP�P�B
    /// </summary>
    public class GraphicsCardInfoService
    {
        /// <summary>
        /// �W�ǩΧ�s��d��T�]�ϥΤw��������ơA�䴩�h�i��d�^
        /// </summary>
        public async Task UploadOrUpdateGraphicsCardsAsync(string deviceNo, List<string> gpuNames)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[GraphicsCardInfoService] �˸m�s�����šA�L�k�W����d��T");
                return;
            }

            if (gpuNames == null || gpuNames.Count == 0)
            {
                LogService.Log("[GraphicsCardInfoService] �S����d��T�i�W��");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[GraphicsCardInfoService] ��Ʈw�s�u���\");

                    // �h���ùL�o�ť�
                    var uniqueGpuNames = gpuNames
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name.Trim())
                        .Distinct()
                        .ToList();

                    LogService.Log($"[GraphicsCardInfoService] ������ {uniqueGpuNames.Count} �Ӥ����ƪ���d�G");
                    foreach (var gpuName in uniqueGpuNames)
                    {
                        LogService.Log($"  - {gpuName}");
                    }

                    // �d�߸�Ʈw���Ӹ˸m���Ҧ���d�O��
                    var existingCards = await db.GraphicsCardInfos
                        .Where(g => g.DeviceNo == deviceNo)
                        .ToListAsync();

                    // ���o�{����d�W��
                    var existingNames = existingCards.Select(c => c.CardName).ToHashSet();

                    // ��X�ݭn�s�W����d�]��Ʈw���S�����^
                    var cardsToAdd = uniqueGpuNames
                        .Where(name => !existingNames.Contains(name))
                        .Select(name => new GraphicsCardInfo
                        {
                            DeviceNo = deviceNo,
                            CardName = name
                        })
                        .ToList();

                    // ��X�ݭn�R������d�]���b��e�M�椤���^
                    var cardsToRemove = existingCards
                        .Where(card => !uniqueGpuNames.Contains(card.CardName))
                        .ToList();

                    // ����s�W
                    if (cardsToAdd.Count > 0)
                    {
                        await db.GraphicsCardInfos.AddRangeAsync(cardsToAdd);
                        LogService.Log($"[GraphicsCardInfoService] �s�W {cardsToAdd.Count} �i��d�G");
                        foreach (var card in cardsToAdd)
                        {
                            LogService.Log($"  + {card.CardName}");
                        }
                    }

                    // ����R��
                    if (cardsToRemove.Count > 0)
                    {
                        db.GraphicsCardInfos.RemoveRange(cardsToRemove);
                        LogService.Log($"[GraphicsCardInfoService] ���� {cardsToRemove.Count} �i��d�G");
                        foreach (var card in cardsToRemove)
                        {
                            LogService.Log($"  - {card.CardName}");
                        }
                    }

                    // �x�s�ܧ�
                    if (cardsToAdd.Count > 0 || cardsToRemove.Count > 0)
                    {
                        int savedCount = await db.SaveChangesAsync();
                        LogService.Log($"[GraphicsCardInfoService] ? ���\��s��d��T�]�˸m�s��: {deviceNo}�A�ܧ� {savedCount} ���^");
                    }
                    else
                    {
                        LogService.Log($"[GraphicsCardInfoService] ? ��d��T�L�ܰʡA���ݭn��s�]�˸m�s��: {deviceNo}�^");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[GraphicsCardInfoService] ? �W����d��T�ɵo�Ϳ��~�G{ex.GetType().Name}");
                LogService.Log($"[GraphicsCardInfoService] ���~�T���G{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[GraphicsCardInfoService] �������~�G{ex.InnerException.Message}");
                }
            }
        }
    }
}
