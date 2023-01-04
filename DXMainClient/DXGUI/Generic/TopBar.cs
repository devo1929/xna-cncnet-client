using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using Rampastring.XNAUI;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI.Input;
using Microsoft.Xna.Framework.Input;
using DTAClient.Online;
using ClientGUI;
using ClientCore;
using System.Threading;
using System.Threading.Tasks;
using ClientCore.Extensions;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.Enums;
using DTAClient.Online.EventArguments;
using DTAClient.Services;
using DTAClient.ViewModels;
using DTAConfig;
using Localization;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// A top bar that allows switching between various client windows.
    /// </summary>
    internal sealed class TopBar : XNAPanel
    {
        /// <summary>
        /// The number of seconds that the top bar will stay down after it has
        /// lost input focus.
        /// </summary>
        const double DOWN_TIME_WAIT_SECONDS = 1.0;
        const double EVENT_DOWN_TIME_WAIT_SECONDS = 2.0;
        const double STARTUP_DOWN_TIME_WAIT_SECONDS = 3.5;

        const double DOWN_MOVEMENT_RATE = 1.7;
        const double UP_MOVEMENT_RATE = 1.7;
        const int APPEAR_CURSOR_THRESHOLD_Y = 8;

        private readonly string DEFAULT_PM_BTN_LABEL = "Private Messages (F4)".L10N("UI:Main:PMButtonF4");

        public TopBar(
            WindowManager windowManager,
            TopBarService topBarService,
            CnCNetManager connectionManager,
            PrivateMessageHandler privateMessageHandler
        ) : base(windowManager)
        {
            downTimeWaitTime = TimeSpan.FromSeconds(DOWN_TIME_WAIT_SECONDS);
            this.topBarService = topBarService;
            this.connectionManager = connectionManager;
            this.privateMessageHandler = privateMessageHandler;
        }

        private List<ISwitchable> primarySwitches = new();
        private ISwitchable cncnetLobbySwitch;
        private ISwitchable privateMessageSwitch;

        private XNAClientButton btnPrimary;
        private XNAClientButton btnSecondary;
        private XNAClientButton btnTertiary;
        private XNAClientButton btnOptions;
        private XNAClientButton btnLogout;
        private XNALabel lblTime;
        private XNALabel lblDate;
        private XNALabel lblCnCNetStatus;
        private XNALabel lblCnCNetPlayerCount;
        private XNALabel lblConnectionStatus;

        private readonly TopBarService topBarService;
        private CnCNetManager connectionManager;
        private readonly PrivateMessageHandler privateMessageHandler;

        private CancellationTokenSource cncnetPlayerCountCancellationSource;
        private static readonly object locker = new object();

        private TimeSpan downTime = TimeSpan.FromSeconds(DOWN_TIME_WAIT_SECONDS - STARTUP_DOWN_TIME_WAIT_SECONDS);

        private TimeSpan downTimeWaitTime;

        private bool isDown = true;

        private double locationY = -40.0;

        private bool lanMode => viewModel?.CncnetConectionStatus == CnCNetConnectionStatusEnum.LanMode;

        public EventHandler LogoutEvent;
        private TopBarViewModel viewModel;

        public void Clean()
        {
            if (cncnetPlayerCountCancellationSource != null)
                cncnetPlayerCountCancellationSource.Cancel();
        }

        public override void Initialize()
        {
            Name = "TopBar";
            ClientRectangle = new Rectangle(0, -39, WindowManager.RenderResolutionX, 39);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            BackgroundTexture = AssetLoader.CreateTexture(Color.Black, 1, 1);
            DrawBorders = false;

            btnPrimary = new XNAClientButton(WindowManager);
            btnPrimary.Name = "btnMainButton";
            btnPrimary.ClientRectangle = new Rectangle(12, 9, UIDesignConstants.BUTTON_WIDTH_160, UIDesignConstants.BUTTON_HEIGHT);
            // btnPrimary.Text = "Main Menu (F2)".L10N("UI:Main:MainMenuF2");
            btnPrimary.LeftClick += BtnPrimary_LeftClick;

            btnSecondary = new XNAClientButton(WindowManager);
            btnSecondary.Name = "btnCnCNetLobby";
            btnSecondary.ClientRectangle = new Rectangle(184, 9, UIDesignConstants.BUTTON_WIDTH_160, UIDesignConstants.BUTTON_HEIGHT);
            // btnSecondary.Text = "CnCNet Lobby (F3)".L10N("UI:Main:LobbyF3");
            btnSecondary.LeftClick += BtnSecondary_LeftClick;

            btnTertiary = new XNAClientButton(WindowManager);
            btnTertiary.Name = "btnPrivateMessages";
            btnTertiary.ClientRectangle = new Rectangle(356, 9, UIDesignConstants.BUTTON_WIDTH_160, UIDesignConstants.BUTTON_HEIGHT);
            // btnTertiary.Text = DEFAULT_PM_BTN_LABEL;
            btnTertiary.LeftClick += BtnTertiary_LeftClick;

            lblDate = new XNALabel(WindowManager);
            lblDate.Name = "lblDate";
            lblDate.FontIndex = 1;
            lblDate.Text = Renderer.GetSafeString(DateTime.Now.ToShortDateString(), lblDate.FontIndex);
            lblDate.ClientRectangle = new Rectangle(Width -
                (int)Renderer.GetTextDimensions(lblDate.Text, lblDate.FontIndex).X - 12, 18,
                lblDate.Width, lblDate.Height);

            lblTime = new XNALabel(WindowManager);
            lblTime.Name = "lblTime";
            lblTime.FontIndex = 1;
            lblTime.Text = Renderer.GetSafeString(new DateTime(1, 1, 1, 23, 59, 59).ToLongTimeString(), lblTime.FontIndex);
            lblTime.ClientRectangle = new Rectangle(Width -
                (int)Renderer.GetTextDimensions(lblTime.Text, lblTime.FontIndex).X - 12, 4,
                lblTime.Width, lblTime.Height);

            btnLogout = new XNAClientButton(WindowManager);
            btnLogout.Name = "btnLogout";
            btnLogout.ClientRectangle = new Rectangle(lblDate.X - 87, 9, 75, 23);
            btnLogout.FontIndex = 1;
            btnLogout.Text = "Log Out".L10N("UI:Main:LogOut");
            btnLogout.AllowClick = false;
            btnLogout.LeftClick += (_, _) => BtnLogout_LeftClickAsync().HandleTask();

            btnOptions = new XNAClientButton(WindowManager);
            btnOptions.Name = "btnOptions";
            btnOptions.ClientRectangle = new Rectangle(btnLogout.X - 122, 9, 110, 23);
            btnOptions.Text = "Options (F12)".L10N("UI:Main:OptionsF12");
            btnOptions.LeftClick += BtnOptions_LeftClick;

            lblConnectionStatus = new XNALabel(WindowManager);
            lblConnectionStatus.Name = "lblConnectionStatus";
            lblConnectionStatus.FontIndex = 1;
            lblConnectionStatus.Text = "OFFLINE".L10N("UI:Main:StatusOffline");

            AddChild(btnPrimary);
            AddChild(btnSecondary);
            AddChild(btnTertiary);
            AddChild(btnOptions);
            AddChild(lblTime);
            AddChild(lblDate);
            AddChild(btnLogout);
            AddChild(lblConnectionStatus);

            if (ClientConfiguration.Instance.DisplayPlayerCountInTopBar)
            {
                lblCnCNetStatus = new XNALabel(WindowManager);
                lblCnCNetStatus.Name = "lblCnCNetStatus";
                lblCnCNetStatus.FontIndex = 1;
                lblCnCNetStatus.Text = ClientConfiguration.Instance.LocalGame.ToUpper() + " PLAYERS ONLINE:";
                lblCnCNetPlayerCount = new XNALabel(WindowManager);
                lblCnCNetPlayerCount.Name = "lblCnCNetPlayerCount";
                lblCnCNetPlayerCount.FontIndex = 1;
                lblCnCNetPlayerCount.Text = "-";
                lblCnCNetPlayerCount.ClientRectangle = new Rectangle(btnOptions.X - 50, 11, lblCnCNetPlayerCount.Width, lblCnCNetPlayerCount.Height);
                lblCnCNetStatus.ClientRectangle = new Rectangle(lblCnCNetPlayerCount.X - lblCnCNetStatus.Width - 6, 11, lblCnCNetStatus.Width, lblCnCNetStatus.Height);
                AddChild(lblCnCNetStatus);
                AddChild(lblCnCNetPlayerCount);
                CnCNetPlayerCountTask.CnCNetGameCountUpdated += CnCNetInfoController_CnCNetGameCountUpdated;
                cncnetPlayerCountCancellationSource = new CancellationTokenSource();
                CnCNetPlayerCountTask.InitializeService(cncnetPlayerCountCancellationSource);
            }

            lblConnectionStatus.CenterOnParent();

            base.Initialize();

            Keyboard.OnKeyPressed += Keyboard_OnKeyPressed;

            privateMessageHandler.UnreadMessageCountUpdated += PrivateMessageHandler_UnreadMessageCountUpdated;

            topBarService.GetViewModel().Subscribe(TopBarViewModelUpdated);
        }

        private void TopBarViewModelUpdated(TopBarViewModel vm)
        {
            viewModel = vm;
            if (vm.PrimarySwitch != null)
                btnPrimary.Text = vm.PrimarySwitch.GetSwitchName() + " (F2)";
            if (vm.SecondarySwitch != null)
                btnSecondary.Text = vm.SecondarySwitch.GetSwitchName() + " (F3)";
            if (vm.PriavateMessageSwitch != null)
                btnTertiary.Text = vm.PriavateMessageSwitch.GetSwitchName() + " (F4)";

            SetSwitchButtonsClickable(!lanMode);
            RefreshConnectionStatus();

            if (!lanMode)
                SetSwitchButtonsClickable(!vm.IsViewingOptionsWindow);

            SetOptionsButtonClickable(!vm.IsViewingOptionsWindow);
        }

        private void RefreshConnectionStatus()
        {
            switch (viewModel.CncnetConectionStatus)
            {
                case CnCNetConnectionStatusEnum.Connected:
                    btnLogout.AllowClick = true;
                    ConnectionEvent("CONNECTED".L10N("UI:Main:StatusConnected"));
                    return;
                case CnCNetConnectionStatusEnum.Connecting:
                    btnLogout.AllowClick = false;
                    ConnectionEvent("CONNECTING...".L10N("UI:Main:StatusConnecting"));
                    BringDown();
                    return;
                case CnCNetConnectionStatusEnum.LanMode:
                    btnLogout.AllowClick = false;
                    ConnectionEvent("LAN MODE".L10N("UI:Main:StatusLanMode"));
                    return;
                case CnCNetConnectionStatusEnum.Offline:
                    btnLogout.AllowClick = false;
                    ConnectionEvent("OFFLINE".L10N("UI:Main:StatusOffline"));
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(viewModel.CncnetConectionStatus));
            }
        }

        private void PrivateMessageHandler_UnreadMessageCountUpdated(object sender, UnreadMessageCountEventArgs args)
            => UpdatePrivateMessagesBtnLabel(args.UnreadMessageCount);

        private void UpdatePrivateMessagesBtnLabel(int unreadMessageCount)
        {
            btnTertiary.Text = DEFAULT_PM_BTN_LABEL;
            if (unreadMessageCount > 0)
            {
                // TODO need to make a wider button to accommodate count
                // btnPrivateMessages.Text += $" ({unreadMessageCount})";
            }
        }

        private void CnCNetInfoController_CnCNetGameCountUpdated(object sender, PlayerCountEventArgs e)
        {
            lock (locker)
            {
                if (e.PlayerCount == -1)
                    lblCnCNetPlayerCount.Text = "N/A".L10N("UI:Main:N/A");
                else
                    lblCnCNetPlayerCount.Text = e.PlayerCount.ToString();
            }
        }

        private void ConnectionEvent(string text)
        {
            lblConnectionStatus.Text = text;
            lblConnectionStatus.CenterOnParent();
            isDown = true;
            downTime = TimeSpan.FromSeconds(DOWN_TIME_WAIT_SECONDS - EVENT_DOWN_TIME_WAIT_SECONDS);
        }

        private async ValueTask BtnLogout_LeftClickAsync()
        {
            await connectionManager.DisconnectAsync().ConfigureAwait(false);
            LogoutEvent?.Invoke(this, null);
            topBarService.SwitchToPrimary();
        }

        private void BtnPrimary_LeftClick(object sender, EventArgs e) 
            => topBarService.SwitchToPrimary();

        private void BtnSecondary_LeftClick(object sender, EventArgs e) 
            => topBarService.SwitchToCncNetLobby();

        private void BtnTertiary_LeftClick(object sender, EventArgs e)
            => topBarService.SwitchToSwitchableType<PrivateMessagingWindow>();

        private void BtnOptions_LeftClick(object sender, EventArgs e)
            => topBarService.SwitchToSwitchableType<OptionsWindow>();

        private void Keyboard_OnKeyPressed(object sender, KeyPressEventArgs e)
        {
            if (!Enabled || !WindowManager.HasFocus || ProgramConstants.IsInGame)
                return;

            switch (e.PressedKey)
            {
                case Keys.F1:
                    BringDown();
                    break;
                case Keys.F2 when btnPrimary.AllowClick:
                    BtnPrimary_LeftClick(this, EventArgs.Empty);
                    break;
                case Keys.F3 when btnSecondary.AllowClick:
                    BtnSecondary_LeftClick(this, EventArgs.Empty);
                    break;
                case Keys.F4 when btnTertiary.AllowClick:
                    BtnTertiary_LeftClick(this, EventArgs.Empty);
                    break;
                case Keys.F12 when btnOptions.AllowClick:
                    BtnOptions_LeftClick(this, EventArgs.Empty);
                    break;
            }
        }

        public override void OnMouseOnControl()
        {
            if (Cursor.Location.Y > -1 && !ProgramConstants.IsInGame)
                BringDown();

            base.OnMouseOnControl();
        }

        void BringDown()
        {
            isDown = true;
            downTime = TimeSpan.Zero;
        }

        public void SetMainButtonText(string text)
            => btnPrimary.Text = text;

        public void SetSwitchButtonsClickable(bool allowClick)
        {
            if (btnPrimary != null)
                btnPrimary.AllowClick = allowClick;
            if (btnSecondary != null)
                btnSecondary.AllowClick = allowClick;
            if (btnTertiary != null)
                btnTertiary.AllowClick = allowClick;
        }

        public void SetOptionsButtonClickable(bool allowClick)
        {
            if (btnOptions != null)
                btnOptions.AllowClick = allowClick;
        }

        public override void Update(GameTime gameTime)
        {
            if (Cursor.Location.Y < APPEAR_CURSOR_THRESHOLD_Y && Cursor.Location.Y > -1 && !ProgramConstants.IsInGame)
                BringDown();

            if (isDown)
            {
                if (locationY < 0)
                {
                    locationY += DOWN_MOVEMENT_RATE * (gameTime.ElapsedGameTime.TotalMilliseconds / 10.0);
                    ClientRectangle = new Rectangle(X, (int)locationY,
                        Width, Height);
                }

                downTime += gameTime.ElapsedGameTime;

                isDown = downTime < downTimeWaitTime;
            }
            else
            {
                if (locationY > -Height - 1)
                {
                    locationY -= UP_MOVEMENT_RATE * (gameTime.ElapsedGameTime.TotalMilliseconds / 10.0);
                    ClientRectangle = new Rectangle(X, (int)locationY,
                        Width, Height);
                }
                else
                    return; // Don't handle input when the cursor is above our game window
            }

            DateTime dtn = DateTime.Now;

            lblTime.Text = Renderer.GetSafeString(dtn.ToLongTimeString(), lblTime.FontIndex);
            string dateText = Renderer.GetSafeString(dtn.ToShortDateString(), lblDate.FontIndex);
            if (lblDate.Text != dateText)
                lblDate.Text = dateText;

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            Renderer.DrawRectangle(new Rectangle(X, ClientRectangle.Bottom - 2, Width, 1), UISettings.ActiveSettings.PanelBorderColor);
        }
    }
}
