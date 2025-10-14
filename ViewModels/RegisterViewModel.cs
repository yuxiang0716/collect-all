// 檔案: ViewModels/RegisterViewModel.cs
using System.Windows;
using System.Windows.Input;
using collect_all.Commands;
using collect_all.Services;

namespace collect_all.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private readonly UserService _userService;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;

        public string Username
        {
            get => _username;
            set
            {
                if (_username == value) return;
                _username = value;
                OnPropertyChanged();
                (RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password == value) return;
                _password = value;
                OnPropertyChanged();
                (RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (_confirmPassword == value) return;
                _confirmPassword = value;
                OnPropertyChanged();
                (RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand RegisterCommand { get; }

        public RegisterViewModel()
        {
            _userService = new UserService();
            RegisterCommand = new RelayCommand(async _ => await RegisterAsync(), _ => CanRegister());
        }

        private bool CanRegister()
        {
            return !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   !string.IsNullOrWhiteSpace(ConfirmPassword);
        }

        // (RegisterAsync 和 CloseWindow 方法維持不變)
        private async Task RegisterAsync()
        {
            if (Password != ConfirmPassword)
            {
                System.Windows.MessageBox.Show("兩次輸入的密碼不一致！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = await _userService.RegisterUserAsync(Username, Password);

            if (result.Success)
            {
                System.Windows.MessageBox.Show(result.Message, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindow();
            }
            else
            {
                System.Windows.MessageBox.Show(result.Message, "註冊失敗", MessageBoxButton.OK, MessageBoxImage.Error);
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