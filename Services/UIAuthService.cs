// ÀÉ®×: Services/UIAuthService.cs
using System;
using System.Windows;
using collect_all.Views;
using collect_all.ViewModels;
using collect_all.Models;

namespace collect_all.Services
{
    public static class UIAuthService
    {
        public static User? ShowLoginDialog(string? prefillUsername = null, bool isUsernameReadOnly = false)
        {
            User? result = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var loginVm = new LoginViewModel();
                if (!string.IsNullOrEmpty(prefillUsername))
                {
                    loginVm.Username = prefillUsername;
                    loginVm.IsUsernameReadOnly = isUsernameReadOnly;
                }

                var window = new LoginWindow
                {
                    DataContext = loginVm
                };

                EventHandler? handler = null;
                handler = (s, e) =>
                {
                    if (AuthenticationService.Instance.CurrentUser != null)
                    {
                        try { window.Close(); } catch { }
                    }
                };

                AuthenticationService.Instance.AuthenticationStateChanged += handler;
                window.ShowDialog();
                AuthenticationService.Instance.AuthenticationStateChanged -= handler;

                result = AuthenticationService.Instance.CurrentUser;
            });
            return result;
        }
    }
}