using System.Windows;

namespace collect_all
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password != ConfirmBox.Password)
            {
                // 指定命名空間，避免衝突
                System.Windows.MessageBox.Show("兩次密碼不一致！");
                return;
            }

            System.Windows.MessageBox.Show($"註冊帳號：{UsernameBox.Text}\n(此功能將來與資料庫連線)");
            this.Close();
        }
    }
}
