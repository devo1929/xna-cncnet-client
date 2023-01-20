using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ClientGUI;
using DTAClient.Extensions;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using DTAClient.ViewModels;
using DynamicData.Binding;
using Localization;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using ReactiveUI;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public sealed class PrivateMessagingWindow : XNAWindow, ISwitchable, IViewFor<PrivateMessagingWindowViewModel>
    {
        private const int MESSAGES_INDEX = 0;
        private const int FRIEND_LIST_VIEW_INDEX = 1;
        private const int ALL_PLAYERS_VIEW_INDEX = 2;
        private const int RECENT_PLAYERS_VIEW_INDEX = 3;

        private const int LB_USERS_WIDTH = 150;

        private readonly string DEFAULT_PLAYERS_TEXT = "PLAYERS:".L10N("UI:Main:Players");
        private readonly string RECENT_PLAYERS_TEXT = "RECENT PLAYERS:".L10N("UI:Main:RecentPlayers");

        private CnCNetUserData cncnetUserData;

        // private readonly PrivateMessagingWindowViewModel viewModel;
        // private PrivateMessagingWindowModel model;

        public PrivateMessagingWindow(
            WindowManager windowManager,
            CnCNetManager connectionManager,
            CnCNetUserData cncnetUserData,
            PrivateMessagingWindowViewModel viewModel,
            RecentPlayerTable recentPlayerTable
        ) : base(windowManager)
        {
            this.connectionManager = connectionManager;
            this.cncnetUserData = cncnetUserData;
            ViewModel = viewModel;
            mclbRecentPlayerList = recentPlayerTable;
        }

        private XNALabel lblPrivateMessaging;

        private XNAClientTabControl tabControl;

        private XNALabel lblPlayers;
        private XNAClientListBox lbUserList;
        private RecentPlayerTable mclbRecentPlayerList;

        private XNALabel lblMessages;
        private ChatListBox lbMessages;

        private XNATextBox tbMessageInput;

        private GlobalContextMenu globalContextMenu;

        private CnCNetManager connectionManager;

        private string lastReceivedPMSender;
        private string lastConversationPartner;

        /// <summary>
        /// Because the user cannot view PMs during a game, we store the latest
        /// PM received during a game in this variable and display it when the
        /// user has returned from the game.
        /// </summary>
        private NotificationPopupMessage pmReceivedDuringGame;

        // These are used by the "invite to game" feature in the
        // context menu and are kept up-to-date by the lobby
        private string inviteChannelName;
        private string inviteGameName;
        private string inviteChannelPassword;

        private Action<IRCUser, IMessageView> JoinUserAction;

        public override void Initialize()
        {
            Name = nameof(PrivateMessagingWindow);
            ClientRectangle = new Rectangle(0, 0, 600, 600);
            BackgroundTexture = AssetLoader.LoadTextureUncached("privatemessagebg.png");

            lblPrivateMessaging = new XNALabel(WindowManager);
            lblPrivateMessaging.Name = nameof(lblPrivateMessaging);
            lblPrivateMessaging.FontIndex = 1;
            lblPrivateMessaging.Text = "PRIVATE MESSAGING".L10N("UI:Main:PMLabel");

            AddChild(lblPrivateMessaging);
            lblPrivateMessaging.CenterOnParent();
            lblPrivateMessaging.ClientRectangle = new Rectangle(
                lblPrivateMessaging.X, 12,
                lblPrivateMessaging.Width,
                lblPrivateMessaging.Height);

            tabControl = new XNAClientTabControl(WindowManager);
            tabControl.Name = nameof(tabControl);
            tabControl.ClientRectangle = new Rectangle(34, 50, 0, 0);
            tabControl.ClickSound = new EnhancedSoundEffect("button.wav");
            tabControl.FontIndex = 1;
            PrivateMessagingWindowViewModel.Tabs.ForEach(tabControl.AddTab);
            tabControl.SelectedIndexChanged += (_, _) => ViewModel.SelectedTabIndex = tabControl.SelectedTab;

            lblPlayers = new XNALabel(WindowManager);
            lblPlayers.Name = nameof(lblPlayers);
            lblPlayers.ClientRectangle = new Rectangle(12, tabControl.Bottom + 24, 0, 0);
            lblPlayers.FontIndex = 1;
            lblPlayers.Text = DEFAULT_PLAYERS_TEXT;

            lbUserList = new XNAClientListBox(WindowManager);
            lbUserList.Name = nameof(lbUserList);
            lbUserList.ClientRectangle = new Rectangle(lblPlayers.X,
                lblPlayers.Bottom + 6,
                LB_USERS_WIDTH, Height - lblPlayers.Bottom - 18);
            lbUserList.RightClick += LbUserList_RightClick;
            lbUserList.SelectedIndexChanged += (_, _) => ViewModel.SelectedUserIndex = lbUserList.SelectedIndex;
            lbUserList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbUserList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            lblMessages = new XNALabel(WindowManager);
            lblMessages.Name = nameof(lblMessages);
            lblMessages.ClientRectangle = new Rectangle(lbUserList.Right + 12,
                lblPlayers.Y, 0, 0);
            lblMessages.FontIndex = 1;
            lblMessages.Text = "MESSAGES:".L10N("UI:Main:Messages");

            lbMessages = new ChatListBox(WindowManager);
            lbMessages.Name = nameof(lbMessages);
            lbMessages.ClientRectangle = new Rectangle(lblMessages.X,
                lbUserList.Y,
                Width - lblMessages.X - 12,
                lbUserList.Height - 25);
            lbMessages.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbMessages.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbMessages.RightClick += ChatListBox_RightClick;

            tbMessageInput = new XNATextBox(WindowManager);
            tbMessageInput.Name = nameof(tbMessageInput);
            tbMessageInput.ClientRectangle = new Rectangle(lbMessages.X,
                lbMessages.Bottom + 6, lbMessages.Width, 19);
            tbMessageInput.EnterPressed += TbMessageInput_EnterPressedAsync;
            tbMessageInput.MaximumTextLength = 200;

            mclbRecentPlayerList.ClientRectangle = new Rectangle(lbUserList.X, lbUserList.Y, lbMessages.Right - lbUserList.X, lbUserList.Height);
            mclbRecentPlayerList.PlayerRightClick += RecentPlayersList_RightClick;
            mclbRecentPlayerList.Disable();

            globalContextMenu = new GlobalContextMenu(WindowManager, connectionManager, cncnetUserData, this);
            globalContextMenu.JoinEvent += PlayerContextMenu_JoinUser;

            AddChild(tabControl);
            AddChild(lblPlayers);
            AddChild(lbUserList);
            AddChild(lblMessages);
            AddChild(lbMessages);
            AddChild(tbMessageInput);
            AddChild(mclbRecentPlayerList);
            AddChild(globalContextMenu);

            base.Initialize();

            CenterOnParent();

            tabControl.SelectedTab = MESSAGES_INDEX;

            GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;

            CreateBindings();
        }

        private void CreateBindings()
        {
            // update user list items 
            ViewModel.WhenAnyValue(vm => vm.UserListItems)
                .ObserveOnCurrent()
                .Subscribe(lbUserList.SetItems);
            // update messages
            ViewModel.WhenAnyValue(vm => vm.Messages)
                .ObserveOnCurrent()
                .Select(privateMessages => privateMessages.Select(m => m.ChatMessage))
                .Subscribe(SetMessages);
            // update recent players
            ViewModel.WhenAnyValue(vm => vm.RecentPlayers)
                .ObserveOnCurrent()
                .Subscribe(SetRecentPlayers);
            // update selected tab index
            ViewModel.WhenAnyValue(vm => vm.SelectedTabIndex)
                .Subscribe(SetSelectedTabIndex);
            // update selected user index
            ViewModel.WhenAnyValue(vm => vm.SelectedUserIndex)
                .BindTo(this, v => v.lbUserList.SelectedIndex);
            // update message input field enabled
            ViewModel.WhenAnyValue(vm => vm.MessageInputEnabled)
                .BindTo(this, v => v.tbMessageInput.Enabled);
            // update message input field text
            ViewModel.WhenAnyValue(vm => vm.MessageInputText)
                .BindTo(this, v => v.tbMessageInput.Text);
        }

        private void SetSelectedTabIndex(int tabIndex)
        {
            tabControl.SelectedTab = tabIndex;
            ShowRecentPlayers(tabIndex == PrivateMessagingWindowViewModel.RECENT_PLAYERS_TAB_INDEX);
        }

        private void SetSelectedUserIndex(int userIndex) => lbUserList.SelectedIndex = userIndex;

        private void SetMessageInputEnabled(bool enabled)
        {
            tbMessageInput.Enabled = enabled;
            if (!enabled)
                tbMessageInput.Text = string.Empty;
        }

        private void SetMessages(IEnumerable<ChatMessage> messages)
        {
            lbMessages.Clear();
            lbMessages.SelectedIndex = -1;

            foreach (ChatMessage chatMessage in messages)
                lbMessages.AddMessage(chatMessage);

            lbMessages.ScrollToBottom();
        }

        private void SetRecentPlayers(List<RecentPlayer> recentPlayers)
        {
            mclbRecentPlayerList.ClearItems();

            foreach (RecentPlayer recentPlayer in recentPlayers)
                mclbRecentPlayerList.AddRecentPlayer(recentPlayer);
        }

        private void ChatListBox_RightClick(object sender, EventArgs e)
        {
            if (lbMessages.HoveredIndex < 0 || lbMessages.HoveredIndex >= lbMessages.Items.Count)
                return;

            lbMessages.SelectedIndex = lbMessages.HoveredIndex;
            var chatMessage = lbMessages.SelectedItem.Tag as ChatMessage;
            if (chatMessage == null)
                return;

            globalContextMenu.Show(chatMessage, GetCursorPoint());
        }

        private void RecentPlayersList_RightClick(object sender, RecentPlayerTableRightClickEventArgs e)
            => globalContextMenu.Show(e.IrcUser, GetCursorPoint());


        public void SetInviteChannelInfo(string channelName, string gameName, string channelPassword)
        {
            inviteChannelName = channelName;
            inviteGameName = gameName;
            inviteChannelPassword = channelPassword;
        }

        public void ClearInviteChannelInfo() => SetInviteChannelInfo(string.Empty, string.Empty, string.Empty);

        private void LbUserList_RightClick(object sender, EventArgs e)
        {
            lbUserList.SelectedIndex = lbUserList.HoveredIndex;
            var ircUser = (IRCUser)lbUserList.SelectedItem?.Tag;
            if (ircUser == null)
                return;

            globalContextMenu.Show(new GlobalContextMenuData()
            {
                IrcUser = ircUser,
                inviteChannelName = inviteChannelName,
                inviteChannelPassword = inviteChannelPassword,
                inviteGameName = inviteGameName
            }, GetCursorPoint());
        }

        private void PlayerContextMenu_JoinUser(object sender, JoinUserEventArgs args)
        {
            if (tabControl.SelectedTab == RECENT_PLAYERS_VIEW_INDEX)
                JoinUserAction(args.IrcUser, new RecentPlayerMessageView(WindowManager));
            else
                JoinUserAction(args.IrcUser, lbMessages);
        }

        private void SharedUILogic_GameProcessExited() =>
            WindowManager.AddCallback(HandleGameProcessExited);

        private void HandleGameProcessExited()
        {
            // if (pmReceivedDuringGame != null)
            // {
            //     ShowNotification(pmReceivedDuringGame.User, pmReceivedDuringGame.Message);
            //     pmReceivedDuringGame = null;
            // }
        }

        private void TbMessageInput_EnterPressedAsync(object sender, EventArgs eventArgs) => ViewModel.SendMessageToUser(tbMessageInput.Text);

        private void ShowRecentPlayers(bool show)
        {
            if (mclbRecentPlayerList.Enabled == show)
                return;

            tbMessageInput.Visible = !show;
            if (show)
            {
                lbMessages.Disable();
                lblMessages.Disable();
                lbUserList.Disable();
                lblPlayers.Text = RECENT_PLAYERS_TEXT;
                mclbRecentPlayerList.Enable();
            }
            else
            {
                lbMessages.Enable();
                lblMessages.Enable();
                lbUserList.Enable();
                lblPlayers.Text = DEFAULT_PLAYERS_TEXT;
                mclbRecentPlayerList.Disable();
            }
        }

        /// <summary>
        /// Prepares a recipient for sending a private message.
        /// </summary>
        /// <param name="name"></param>
        public void InitPM(string name)
        {
            // Visible = true;
            // Enabled = true;
            //
            // // Check if we've already talked with the user during this session
            // // and if so, open the old conversation
            // int pmUserIndex = privateMessageUsers.FindIndex(
            //     pmUser => pmUser.IrcUser.Name == name);
            //
            // if (pmUserIndex > -1)
            // {
            //     tabControl.SelectedTab = MESSAGES_INDEX;
            //     lbUserList.SelectedIndex = FindItemIndexForName(name);
            //     WindowManager.SelectedControl = tbMessageInput;
            //     return;
            // }
            //
            // if (cncnetUserData.IsFriend(name))
            // {
            //     // If we haven't talked with the user, check if they are a friend and if so,
            //     // let's enter the friend list and talk to them there
            //     tabControl.SelectedTab = FRIEND_LIST_VIEW_INDEX;
            // }
            // else
            // {
            //     // If the user isn't a friend, switch to the "all players" view and
            //     // open the conversation there
            //     tabControl.SelectedTab = ALL_PLAYERS_VIEW_INDEX;
            // }
            //
            // lbUserList.SelectedIndex = FindItemIndexForName(name);
            //
            // if (lbUserList.SelectedIndex > -1)
            // {
            //     WindowManager.SelectedControl = tbMessageInput;
            //
            //     lbUserList.TopIndex = lbUserList.SelectedIndex > -1 ? lbUserList.SelectedIndex : 0;
            // }
            //
            // if (lbUserList.LastIndex - lbUserList.TopIndex < lbUserList.NumberOfLinesOnList - 1)
            //     lbUserList.ScrollToBottom();
        }

        public void SwitchOn()
        {
            // tabControl.SelectedTab = MESSAGES_INDEX;
            //
            // WindowManager.SelectedControl = null;
            // privateMessageHandler.ResetUnreadMessageCount();
            //
            // if (Visible)
            // {
            //     if (!string.IsNullOrEmpty(lastReceivedPMSender))
            //     {
            //         int index = FindItemIndexForName(lastReceivedPMSender);
            //
            //         if (index > -1)
            //             lbUserList.SelectedIndex = index;
            //     }
            // }
            // else
            // {
            //     Enable();
            //
            // }
        }

        public void SetJoinUserAction(Action<IRCUser, IMessageView> joinUserAction)
        {
            JoinUserAction = joinUserAction;
        }

        public void SwitchOff() => Disable();

        public string GetSwitchName() => "Private Messaging".L10N("UI:Main:PrivateMessaging");

        class RecentPlayerMessageView : IMessageView
        {
            private readonly WindowManager windowManager;

            public RecentPlayerMessageView(WindowManager windowManager)
            {
                this.windowManager = windowManager;
            }

            public void AddMessage(ChatMessage message)
                => XNAMessageBox.Show(windowManager, "Message".L10N("UI:Main:MessageTitle"), message.Message);
        }

        public PrivateMessagingWindowViewModel ViewModel { get; set; }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (PrivateMessagingWindowViewModel)value;
        }
    }
}