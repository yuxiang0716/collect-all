using System;
using System.Collections.ObjectModel;

namespace collect_all.Services
{
    /// <summary>
    /// �Y�ɤ�x�A�� - ���N Console.WriteLine
    /// </summary>
    public static class LogService
    {
        private static ObservableCollection<string> _logs = new ObservableCollection<string>();
        public static ObservableCollection<string> Logs => _logs;

        public static event EventHandler<string>? LogAdded;

        public static void Log(string message)
        {
            string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            
            // �b UI ������W�[�J��x
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _logs.Add(timestampedMessage);
                
                // �����x�ƶq�A�קK�O���鷸�X
                if (_logs.Count > 1000)
                {
                    _logs.RemoveAt(0);
                }
            });

            // �P�ɿ�X�� Console�]�p�G�����ܡ^
            Console.WriteLine(timestampedMessage);
            
            // Ĳ�o�ƥ�
            LogAdded?.Invoke(null, timestampedMessage);
        }

        public static void Clear()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => _logs.Clear());
        }
    }
}
