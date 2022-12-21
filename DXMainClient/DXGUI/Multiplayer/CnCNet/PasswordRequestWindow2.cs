using System;
using ClientCore.Extensions;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using DTAClient.DXGUI.Services;
using Localization;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class PasswordRequestWindow2 : XNAWindow
    {
        public PasswordRequestWindow2(
            WindowManager windowManager,
            CnCNetLobbyService cncnetLobbyService,
            PrivateMessagingWindow privateMessagingWindow
        ) : base(windowManager)
        {
            this.cncnetLobbyService = cncnetLobbyService;
            this.privateMessagingWindow = privateMessagingWindow;
        }

        private XNATextBox tbPassword;

        private readonly CnCNetLobbyService cncnetLobbyService;
        private readonly PrivateMessagingWindow privateMessagingWindow;
        private bool pmWindowWasEnabled { get; set; }
        private GenericHostedGame hostedGame { get; set; }

        public override void Initialize()
        {
            Name = "PasswordRequestWindow";
            BackgroundTexture = AssetLoader.LoadTexture("passwordquerybg.png");

            var lblDescription = new XNALabel(WindowManager);
            lblDescription.Name = "lblDescription";
            lblDescription.ClientRectangle = new Rectangle(12, 12, 0, 0);
            lblDescription.Text = "Please enter the password for the game and click OK.".L10N("UI:Main:EnterPasswordAndHitOK");

            ClientRectangle = new Rectangle(0, 0, lblDescription.Width + 24, 110);

            tbPassword = new XNATextBox(WindowManager);
            tbPassword.Name = "tbPassword";
            tbPassword.ClientRectangle = new Rectangle(lblDescription.X,
                lblDescription.Bottom + 12, Width - 24, 21);

            var btnOK = new XNAClientButton(WindowManager);
            btnOK.Name = "btnOK";
            btnOK.ClientRectangle = new Rectangle(lblDescription.X,
                ClientRectangle.Bottom - 35, UIDesignConstants.BUTTON_WIDTH_92, UIDesignConstants.BUTTON_HEIGHT);
            btnOK.Text = "OK".L10N("UI:Main:ButtonOK");
            btnOK.LeftClick += BtnOK_LeftClick;

            var btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Name = "btnCancel";
            btnCancel.ClientRectangle = new Rectangle(Width - 104,
                btnOK.Y, UIDesignConstants.BUTTON_WIDTH_92, UIDesignConstants.BUTTON_HEIGHT);
            btnCancel.Text = "Cancel".L10N("UI:Main:ButtonCancel");
            btnCancel.LeftClick += BtnCancel_LeftClick;

            AddChild(lblDescription);
            AddChild(tbPassword);
            AddChild(btnOK);
            AddChild(btnCancel);

            base.Initialize();

            CenterOnParent();

            tbPassword.EnterPressed += TextBoxPassword_EnterPressed;
        }

        public void Show(GenericHostedGame hostedGame)
        {
            this.hostedGame = hostedGame;
            Enable();
            WindowManager.SelectedControl = tbPassword;
            pmWindowWasEnabled = privateMessagingWindow.Enabled;
        }

        private void Hide()
        {
            if (pmWindowWasEnabled)
                privateMessagingWindow.Enable();
            
            Disable();
        }

        private void Submit()
        {
            if (string.IsNullOrEmpty(tbPassword.Text))
                return;

            cncnetLobbyService.JoinGameAsync(hostedGame, tbPassword.Text).HandleTask();
            tbPassword.Text = string.Empty;
            pmWindowWasEnabled = false;
            Hide();
        }

        private void TextBoxPassword_EnterPressed(object sender, EventArgs eventArgs) => Submit();

        private void BtnOK_LeftClick(object sender, EventArgs e) => Submit();

        private void BtnCancel_LeftClick(object sender, EventArgs e) => Hide();
    }
}