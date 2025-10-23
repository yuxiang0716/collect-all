// 檔案: Views/MainWindow.xaml.cs (重構後)
using System;
using System.Windows;
using System.Windows.Forms; // Tray Icon
using System.Drawing;      // Icon
using collect_all.ViewModels; // 引用 ViewModels
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using collect_all.Services;  // 加入 Services

namespace collect_all.Views
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? m_notifyIcon;
        private WindowState m_storedWindowState = WindowState.Normal;
        private bool _isExitingFromTray = false;

        public MainWindow()
        {
            InitializeComponent();

            m_notifyIcon = new NotifyIcon
            {
                BalloonTipText = "程式已最小化。點擊圖示以顯示。",
                BalloonTipTitle = "鴻盛資訊維護服務識別器",
                Text = "鴻盛資訊維護服務識別器"
            };
            try
            {
                m_notifyIcon.Icon = new Icon("TheAppIcon.ico");
            }
            catch { }
            
            m_notifyIcon.MouseClick += (s, e) =>
            {
                 if (e.Button == MouseButtons.Left)
                 {
                    Show();
                    WindowState = m_storedWindowState;
                    Activate();
                 }
            };

            var contextMenu = new ContextMenuStrip();
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += async (s, e) =>
            {
                _isExitingFromTray = true;
                
                // 在背景執行上傳，不阻塞 UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        LogService.Log("[MainWindow] 程式退出前嘗試上傳開關機記錄...");
                        
                        var currentUser = AuthenticationService.Instance.CurrentUser;
                        if (currentUser != null)
                        {
                            var macService = new SystemMacLoginService();
                            string mac = macService.GetPrimaryMac();
                            var (found, macRecord) = await macService.CheckMacInTableAsync(mac);
                            
                            if (found && macRecord != null)
                            {
                                var powerLogService = new PowerLogService();
                                var logs = powerLogService.GetPowerLogs(7);
                                
                                // 使用 Task 配合超時，但不等待太久
                                var uploadTask = powerLogService.UploadPowerLogsAsync(macRecord.DeviceId, logs);
                                var completedTask = await Task.WhenAny(uploadTask, Task.Delay(3000)); // 最多等 3 秒
                                
                                if (completedTask == uploadTask)
                                {
                                    LogService.Log("[MainWindow] ✓ 退出前上傳完成");
                                }
                                else
                                {
                                    LogService.Log("[MainWindow] ⚠ 退出前上傳超時，程式將直接關閉");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"[MainWindow] 退出前上傳失敗：{ex.Message}");
                    }
                });
                
                // 立即關閉程式，不等待上傳完成
                await Task.Delay(500); // 給一點時間讓上傳開始
                
                if (m_notifyIcon != null) m_notifyIcon.Visible = false;
                if (DataContext is IDisposable disposable) disposable.Dispose();
                System.Windows.Application.Current.Shutdown();
            };
            
            contextMenu.Items.Add(exitItem);
            m_notifyIcon.ContextMenuStrip = contextMenu;

            this.StateChanged += OnStateChanged;
            this.IsVisibleChanged += OnIsVisibleChanged;
            this.Closing += OnClose;

            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.ResizeMode = ResizeMode.CanMinimize;

            m_notifyIcon.Visible = true;
        }

        #region Tray Events
        private void OnClose(object? sender, System.ComponentModel.CancelEventArgs args)
        {
            if (!_isExitingFromTray)
            {
                args.Cancel = true;
                Hide();
            }
        }
        
        private void OnStateChanged(object? sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized) Hide();
            else m_storedWindowState = WindowState;
        }
        
        private void OnIsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs args)
        {
            if (m_notifyIcon != null) m_notifyIcon.Visible = !IsVisible;
        }
        #endregion

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindParent<ScrollViewer>(sender as DependencyObject);

            if (scrollViewer != null)
            {
                if (e.Delta < 0)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 40);
                }
                else
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 40);
                }

                e.Handled = true;
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
    }
}