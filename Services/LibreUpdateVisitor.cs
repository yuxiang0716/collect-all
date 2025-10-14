// 檔案: Services/LibreUpdateVisitor.cs
using LibreHardwareMonitor.Hardware;

namespace collect_all.Services
{
    // 實作 IVisitor 介面，這是函式庫建議的訪問方式，穩定性更高
    public class LibreUpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}