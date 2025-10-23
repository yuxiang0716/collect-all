// 檔案: Models/NetworkTrafficData.cs (新檔案)

namespace collect_all.Models
{
    public class NetworkTrafficData
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string DownloadSpeed { get; set; } = string.Empty;
        public string UploadSpeed { get; set; } = string.Empty;
    }
}