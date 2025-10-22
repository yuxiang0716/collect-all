// 檔案: Views/BootHistoryWindow.xaml.cs (已修正)
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader; 
using System.Windows;
using collect_all.Models; 

namespace collect_all.Views
{
    public partial class BootHistoryWindow : Window
    {
        public BootHistoryWindow()
        {
            InitializeComponent();
            this.Loaded += BootHistoryWindow_Loaded;
        }

        private void BootHistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BootEventsListView.ItemsSource = GetBootEvents();
        }

        private List<BootEvent> GetBootEvents()
        {
            var events = new List<BootEvent>();
            DateTime startTime = DateTime.Now.AddDays(-7); 

            string queryString = 
                $"*[System[(" +
                "(Provider[@Name='Microsoft-Windows-Kernel-General'] and (EventID=12 or EventID=13))" +
                $") and TimeCreated[timediff(@SystemTime) <= {DateTime.Now.Subtract(startTime).TotalMilliseconds}]]]";

            EventLogQuery query = new EventLogQuery("System", PathType.LogName, queryString);
            
            try
            {
                EventLogReader reader = new EventLogReader(query);
                EventRecord record;

                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        string eventType = "未知";
                        switch (record.Id)
                        {
                            case 12:  eventType = "開機 (Startup)"; break;
                            case 13:  eventType = "關機 (Shutdown)"; break;
                        }

                        // 【修正 2：處理 CS8629 警告】
                        // 檢查 record.TimeCreated 是否真的有值
                        if (record.TimeCreated.HasValue)
                        {
                            events.Add(new BootEvent
                            {
                                Time = record.TimeCreated.Value.ToLocalTime(), 
                                EventType = eventType
                            });
                        }
                    }
                }
            }
            catch (EventLogException ex)
            {
                // 【修正 1：處理 CS0104 錯誤】
                // 明確指定使用 System.Windows.MessageBox 來消除歧義
                System.Windows.MessageBox.Show($"讀取事件日誌時發生錯誤：\n{ex.Message}\n\n請確認程式是以系統管理員身分執行。");
            }

            events.Sort((x, y) => y.Time.CompareTo(x.Time));
            return events;
        }
    }
}