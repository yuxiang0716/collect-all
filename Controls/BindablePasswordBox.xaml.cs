using System.Windows;
using System.Windows.Controls;

namespace collect_all.Controls
{
    public partial class BindablePasswordBox : System.Windows.Controls.UserControl
    {
        private bool _isUpdating;

        // 步驟 1: 建立一個可以被 ViewModel 綁定的依賴屬性 "Password"
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register(
                "Password", // 屬性名稱
                typeof(string),
                typeof(BindablePasswordBox),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnPasswordChanged // 當 ViewModel 改變 Password 時呼叫此方法
                )
            );

        public string Password
        {
            get => (string)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        public BindablePasswordBox()
        {
            InitializeComponent();
            // 步驟 2: 監聽內部 PasswordBox 的內容變化
            InternalPasswordBox.PasswordChanged += OnInternalPasswordChanged;
        }

        // 當 ViewModel 改變我們的 Password 屬性時，我們手動更新內部 PasswordBox 的內容
        private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BindablePasswordBox control && !control._isUpdating)
            {
                control.InternalPasswordBox.Password = (string)e.NewValue;
            }
        }

        // 當使用者在內部 PasswordBox 打字時，我們手動更新我們的 Password 屬性
        private void OnInternalPasswordChanged(object sender, RoutedEventArgs e)
        {
            _isUpdating = true;
            Password = InternalPasswordBox.Password; // 這會自動通知 ViewModel
            _isUpdating = false;
        }
    }
}