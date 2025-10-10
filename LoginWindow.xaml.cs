using System.Windows;

namespace collect_all
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string user = UsernameBox.Text;
            string pass = PasswordBox.Password;

            // ✅ 指定命名空間，避免與 System.Windows.Forms.MessageBox 衝突
            System.Windows.MessageBox.Show($"帳號：{user}\n密碼：{pass}\n(此功能將來與資料庫連線)");

            this.Close();
        }
    }
}
