// 檔案: Views/MainWindow.xaml.cs (重構後)
using System;
using System.Windows;
using System.Windows.Forms; // Tray Icon
using System.Drawing;      // Icon
using collect_all.ViewModels; // 引用 ViewModels
using System.Windows.Input; // <-- 新增
using System.Windows.Controls;
using System.Windows.Media; // <-- 新增 (為了 VisualTreeHelper)
using System.Windows.Controls.Primitives; // <-- 新增 (為了 TabPanel)

namespace collect_all.Views
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? m_notifyIcon;
        private WindowState m_storedWindowState = WindowState.Normal;

        public MainWindow()
        {
            InitializeComponent();

            // --- 所有資料收集和 DataContext 的程式碼都已刪除 ---

            // --- 只保留系統托盤 (Tray Icon) 的初始化邏輯 ---
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
            exitItem.Click += (s, e) =>
            {
                if (m_notifyIcon != null) m_notifyIcon.Visible = false;
                // 處理 ViewModel 的資源釋放
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

        // --- 所有 Click 事件和資料收集方法都已刪除 ---

        #region Tray Events (保留所有與托盤相關的事件)
        private void OnClose(object? sender, System.ComponentModel.CancelEventArgs args)
        {
            args.Cancel = true;
            Hide();
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

        #region Tray Events (保留所有與托盤相關的事件)
        // ... (這裡是 OnClose, OnStateChanged, OnIsVisibleChanged 方法)
        #endregion

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 尋找外層的 ScrollViewer
            var scrollViewer = FindParent<ScrollViewer>(sender as DependencyObject);

            if (scrollViewer != null)
            {
                // 手動控制 ScrollViewer 滾動
                if (e.Delta < 0) // 滾輪向下
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 40); // 40 是滾動幅度
                }
                else // 滾輪向上
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 40);
                }

                // 標記事件已處理，防止 DataGrid 再次處理它
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