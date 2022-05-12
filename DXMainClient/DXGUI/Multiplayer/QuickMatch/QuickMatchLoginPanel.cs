using System;
using System.Threading.Tasks;
using ClientCore.Exceptions;
using ClientGUI;
using DTAClient.Domain.Multiplayer.CnCNet.QuickMatch;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.QuickMatch
{
    public class QuickMatchLoginPanel : QuickMatchPanel
    {
        private const string LoginErrorTitle = "Login Error";
        private readonly QmService qmService;

        private XNATextBox tbEmail;
        private XNAPasswordBox tbPassword;
        private bool loginInitialized;

        public event EventHandler LoginEvent;

        public QuickMatchLoginPanel(WindowManager windowManager) : base(windowManager)
        {
            qmService = QmService.GetInstance();
        }

        public override void Initialize()
        {
            Name = nameof(QuickMatchLoginPanel);

            base.Initialize();

            XNAClientButton btnLogin;
            btnLogin = FindChild<XNAClientButton>(nameof(btnLogin));
            btnLogin.LeftClick += BtnLogin_LeftClick;

            XNAClientButton btnCancel;
            btnCancel = FindChild<XNAClientButton>(nameof(btnCancel));
            btnCancel.LeftClick += Exit_Click;

            tbEmail = FindChild<XNATextBox>(nameof(tbEmail));
            tbEmail.Text = qmService.GetCachedEmail() ?? string.Empty;

            tbPassword = FindChild<XNAPasswordBox>(nameof(tbPassword));

            EnabledChanged += InitLogin;
        }

        public void InitLogin(object sender, EventArgs eventArgs)
        {
            if (!Enabled || loginInitialized)
                return;

            if (qmService.IsLoggedIn())
                LoginAsync(qmService.RefreshAsync);

            loginInitialized = true;
        }

        private void LoginAsync(Func<Task> qmServiceLoginAction)
        {
            qmService.ShowStatus("Logging in...");
            Task.Run(async () =>
            {
                try
                {
                    await qmServiceLoginAction();
                    qmService.ClearStatus();
                    LoginEvent?.Invoke(this, null);
                }
                catch (Exception e)
                {
                    string message = (e as ClientException)?.Message ?? "Error logging in";
                    qmService.ClearStatus();
                    ShowError(message, LoginErrorTitle);
                }
            });
        }

        private void BtnLogin_LeftClick(object sender, EventArgs eventArgs)
        {
            if (!ValidateForm())
                return;

            LoginAsync(async () => await qmService.LoginAsync(tbEmail.Text, tbPassword.Password));
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrEmpty(tbEmail.Text))
            {
                ShowError("No Email specified", LoginErrorTitle);
                return false;
            }

            if (string.IsNullOrEmpty(tbPassword.Text))
            {
                ShowError("No Password specified", LoginErrorTitle);
                return false;
            }

            return true;
        }
    }
}
