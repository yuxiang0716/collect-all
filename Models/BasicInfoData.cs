// 檔案: Models/BasicInfoData.cs
namespace collect_all.Models // <-- 建議加上 .Models
{
    public class BasicInfoData
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public BasicInfoData(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}