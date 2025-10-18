// 檔案: ViewModels/RegisterViewModel.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using collect_all.Commands;
using collect_all.Services;
using MessageBox = System.Windows.MessageBox;

namespace collect_all.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private readonly UserService _userService;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _company = string.Empty;
        private string _fullName = string.Empty;
        
        // 移除點：刪除了 SelectedRole 屬性
        // 移除點：刪除了 Roles 屬性

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); (RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
        }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
        public string ConfirmPassword { get => _confirmPassword; set { _confirmPassword = value; OnPropertyChanged(); } }
        public string Company { get => _company; set { _company = value; OnPropertyChanged(); } }
        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }
        public ICommand RegisterCommand { get; }

        public RegisterViewModel()
        {
            _userService = new UserService();
            // 移除點：刪除了 Roles 的初始化
            RegisterCommand = new RelayCommand(async _ => await ProcessRegistrationAsync(), _ => !string.IsNullOrWhiteSpace(Username));
        }

        private async Task ProcessRegistrationAsync()
        {
            bool userExists = await _userService.UserExistsAsync(Username);

            if (userExists)
            {
                await HandleExistingUserAsync();
            }
            else
            {
                await HandleNewUserRegistrationAsync();
            }
        }

        private async Task HandleExistingUserAsync()
        {
            if (!string.IsNullOrWhiteSpace(Password))
            {
                var userDetails = await _userService.GetUserDetailsAsync(Username);
                if (userDetails != null && !PasswordService.VerifyPasswordHash(Password, userDetails.PasswordHash, userDetails.PasswordSalt))
                {
                    if (Password != ConfirmPassword)
                    {
                        MessageBox.Show("若要變更密碼，兩次輸入的密碼必須一致！", "警告", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                    var changePwdResult = MessageBox.Show($"您輸入的密碼與原密碼不符。\n\n是否要將 '{Username}' 的密碼變更為新密碼？", "確認變更密碼", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                    if (changePwdResult == System.Windows.MessageBoxResult.Yes)
                    {
                        var updateResult = await _userService.UpdatePasswordAsync(Username, Password);
                        MessageBox.Show(updateResult.Message, updateResult.Success ? "成功" : "失敗", System.Windows.MessageBoxButton.OK, updateResult.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);
                        if (updateResult.Success) CloseWindow();
                    }
                    return;
                }
            }

            var deleteConfirmResult = MessageBox.Show($"帳號 '{Username}' 已經存在。\n\n您確定要註銷此帳號嗎？此操作無法復原。", "確認註銷", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (deleteConfirmResult == System.Windows.MessageBoxResult.Yes)
            {
                var deleteResult = await _userService.DeleteUserAsync(Username);
                MessageBox.Show(deleteResult.Message, deleteResult.Success ? "成功" : "失敗", System.Windows.MessageBoxButton.OK, deleteResult.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);
                if (deleteResult.Success) CloseWindow();
            }
        }

        private async Task HandleNewUserRegistrationAsync()
        {
            // 修改點：移除了對 SelectedRole 的檢查
            if (string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword) || string.IsNullOrWhiteSpace(Company) || string.IsNullOrWhiteSpace(FullName))
            {
                MessageBox.Show("註冊新帳號需要填寫所有欄位。", "資訊不完整", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            if (Password != ConfirmPassword)
            {
                MessageBox.Show("兩次輸入的密碼不一致！", "警告", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 修改點：呼叫註冊服務時不再傳遞權限參數
            var registerResult = await _userService.RegisterUserAsync(Username, Password, Company, FullName);
            MessageBox.Show(registerResult.Message, registerResult.Success ? "成功" : "註冊失敗", System.Windows.MessageBoxButton.OK, registerResult.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);
            if (registerResult.Success) CloseWindow();
        }

        private void CloseWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
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