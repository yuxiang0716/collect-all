using collect_all.Commands;
using System.Windows.Input;
using System.Windows;
using System.Threading.Tasks;
using collect_all.Services;
using collect_all.Models;

namespace collect_all.ViewModels
{
    public class LoginViewModel: ViewModelBase
    {
        private readonly UserService _userService;
        private string _username = string.Empty;
        private string _password = string.Empty;

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            _userService = new UserService();
            LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => CanLogin());
        }

        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        }

        private async Task LoginAsync()
        {
            var user = await _userService.Login(Username, Password);
            if (user != null)
            {
                AuthenticationService.Instance.Login(user);
                System.Windows.MessageBox.Show($"歡迎回來, {user.Account}！", "登入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindow();
            }
            else
            {
                System.Windows.MessageBox.Show("帳號或密碼錯誤！", "登入失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        window.Close();
                        break;
                    }
                }
            });
        }
    }
}
