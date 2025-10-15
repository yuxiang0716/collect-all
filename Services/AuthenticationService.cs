
using collect_all.Models;

namespace collect_all.Services
{
    public class AuthenticationService
    {
        public static readonly AuthenticationService Instance = new AuthenticationService();

        public event EventHandler? AuthenticationStateChanged;
        public User? CurrentUser { get; private set; }
        private AuthenticationService() { }

        public void Login(User user)
        {
            CurrentUser = user;
            OnAuthenticationStateChanged();
        }

        public void Logout()
        {
            CurrentUser = null;
            OnAuthenticationStateChanged();
        }

        protected virtual void OnAuthenticationStateChanged()
        {
            EventHandler? handler = AuthenticationStateChanged;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
