using System;
using ClientGUI;
using DTAClient.Domain.Multiplayer.CnCNet.QuickMatch;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.QuickMatch
{
    public class QuickMatchWindow : INItializableWindow
    {
        private readonly QmService qmService;

        private QuickMatchLoginPanel loginPanel;
        private QuickMatchLobbyPanel lobbyPanel;
        private QuickMatchStatusMessageWindow statusWindow;

        public QuickMatchWindow(WindowManager windowManager) : base(windowManager)
        {
            qmService = QmService.GetInstance();
        }

        public override void Initialize()
        {
            Name = nameof(QuickMatchWindow);
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), 1, 1);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            base.Initialize();

            loginPanel = FindChild<QuickMatchLoginPanel>(nameof(QuickMatchLoginPanel));
            loginPanel.LoginEvent += LoginEvent;
            loginPanel.Exit += (sender, args) => Disable();

            lobbyPanel = FindChild<QuickMatchLobbyPanel>(nameof(QuickMatchLobbyPanel));
            lobbyPanel.LogoutEvent += LogoutEvent;
            lobbyPanel.Exit += (sender, args) => Disable();

            statusWindow = FindChild<QuickMatchStatusMessageWindow>(nameof(statusWindow));

            WindowManager.CenterControlOnScreen(this);

            qmService.StatusMessageEvent += StatusMessageEvent;

            EnabledChanged += EnabledChangedEvent;
        }

        private void EnabledChangedEvent(object sender, EventArgs e)
        {
            if (!Enabled)
                return;

            loginPanel.Enable();
        }

        private void StatusMessageEvent(object sender, QmStatusMessageEventArgs qmStatusMessageEventArgs)
        {
            if (string.IsNullOrEmpty(qmStatusMessageEventArgs?.Message))
            {
                statusWindow.Disable();
                return;
            }

            statusWindow.Show(qmStatusMessageEventArgs);
        }

        private void LoginEvent(object sender, EventArgs args)
        {
            lobbyPanel.Enable();
            loginPanel.Disable();
        }

        private void LogoutEvent(object sender, EventArgs args)
        {
            loginPanel.Enable();
            lobbyPanel.Disable();
            qmService.ClearStatus();
        }
    }
}
