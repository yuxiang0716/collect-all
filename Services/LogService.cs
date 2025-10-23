using System;
using System.Collections.ObjectModel;

namespace collect_all.Services
{
    /// <summary>
    /// 即時日誌服務 - 替代 Console.WriteLine
    /// </summary>
    public static class LogService
    {
        private static ObservableCollection<string> _logs = new ObservableCollection<string>();
        public static ObservableCollection<string> Logs => _logs;

        public static event EventHandler<string>? LogAdded;

        public static void Log(string message)
        {
            string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            
            // 在 UI 執行緒上加入日誌
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _logs.Add(timestampedMessage);
                
                // 限制日誌數量，避免記憶體溢出
                if (_logs.Count > 1000)
                {
                    _logs.RemoveAt(0);
                }
            });

            // 同時輸出到 Console（如果有的話）
            Console.WriteLine(timestampedMessage);
            
            // 觸發事件
            LogAdded?.Invoke(null, timestampedMessage);
        }

        public static void Clear()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => _logs.Clear());
        }
    }
}
