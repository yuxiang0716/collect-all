// 檔案: Views/MainWindow.xaml.cs (重構後)
using System;
using System.Windows;
using System.Windows.Forms; // Tray Icon
using System.Drawing;      // Icon
using collect_all.ViewModels; // 引用 ViewModels

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
                BalloonTipTitle = "系統資訊收集器",
                Text = "系統資訊收集器"
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
    }
}