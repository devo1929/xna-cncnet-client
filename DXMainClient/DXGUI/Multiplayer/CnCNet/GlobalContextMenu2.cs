﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ClientCore;
using ClientCore.Extensions;
using ClientGUI;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.DXGUI.Services;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using Localization;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using TextCopy;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public sealed class GlobalContextMenu2 : XNAContextMenu
    {
        private readonly string PRIVATE_MESSAGE = "Private Message".L10N("UI:Main:PrivateMessage");
        private readonly string ADD_FRIEND = "Add Friend".L10N("UI:Main:AddFriend");
        private readonly string REMOVE_FRIEND = "Remove Friend".L10N("UI:Main:RemoveFriend");
        private readonly string BLOCK = "Block".L10N("UI:Main:Block");
        private readonly string UNBLOCK = "Unblock".L10N("UI:Main:Unblock");
        private readonly string INVITE = "Invite".L10N("UI:Main:Invite");
        private readonly string JOIN = "Join".L10N("UI:Main:Join");
        private readonly string COPY_LINK = "Copy Link".L10N("UI:Main:CopyLink");
        private readonly string OPEN_LINK = "Open Link".L10N("UI:Main:OpenLink");

        private readonly CnCNetUserData cncnetUserData;
        private readonly PrivateMessagingWindow2 pmWindow;
        private readonly CnCNetClientService cncnetClientService;
        private readonly CnCNetLobbyService cncnetLobbyService;
        private XNAContextMenuItem privateMessageItem;
        private XNAContextMenuItem toggleFriendItem;
        private XNAContextMenuItem toggleIgnoreItem;
        private XNAContextMenuItem invitePlayerItem;
        private XNAContextMenuItem joinPlayerItem;
        private XNAContextMenuItem copyLinkItem;
        private XNAContextMenuItem openLinkItem;

        private readonly CnCNetManager connectionManager;
        private GlobalContextMenuData2 contextMenuData;

        public GlobalContextMenu2(
            WindowManager windowManager,
            CnCNetManager connectionManager,
            CnCNetUserData cncnetUserData,
            PrivateMessagingWindow2 pmWindow,
            CnCNetClientService cncnetClientService,
            CnCNetLobbyService cncnetLobbyService
        ) : base(windowManager)
        {
            this.connectionManager = connectionManager;
            this.cncnetUserData = cncnetUserData;
            this.pmWindow = pmWindow;
            this.cncnetClientService = cncnetClientService;
            this.cncnetLobbyService = cncnetLobbyService;

            Name = nameof(GlobalContextMenu);
            ClientRectangle = new Rectangle(0, 0, 150, 2);
            Enabled = false;
            Visible = false;
        }

        public override void Initialize()
        {
            privateMessageItem = new XNAContextMenuItem()
            {
                Text = PRIVATE_MESSAGE,
                SelectAction = () => pmWindow.InitPM(GetIrcUser().Name)
            };
            toggleFriendItem = new XNAContextMenuItem()
            {
                Text = ADD_FRIEND,
                SelectAction = () => cncnetUserData.ToggleFriend(GetIrcUser().Name)
            };
            toggleIgnoreItem = new XNAContextMenuItem()
            {
                Text = BLOCK,
                SelectAction = () => GetIrcUserIdentAsync(cncnetUserData.ToggleIgnoreUser).HandleTask()
            };
            invitePlayerItem = new XNAContextMenuItem()
            {
                Text = INVITE,
                SelectAction = () => InviteAsync().HandleTask()
            };
            joinPlayerItem = new XNAContextMenuItem()
            {
                Text = JOIN,
                SelectAction = JoinUserGame
            };

            copyLinkItem = new XNAContextMenuItem()
            {
                Text = COPY_LINK
            };

            openLinkItem = new XNAContextMenuItem()
            {
                Text = OPEN_LINK
            };

            AddItem(privateMessageItem);
            AddItem(toggleFriendItem);
            AddItem(toggleIgnoreItem);
            AddItem(invitePlayerItem);
            AddItem(joinPlayerItem);
            AddItem(copyLinkItem);
            AddItem(openLinkItem);

            cncnetClientService.GetShowContextMenu().Subscribe(Show);
        }

        private void JoinUserGame()
        {
            IRCUser ircUser = GetIrcUser();
            if (ircUser == null)
                return;

            cncnetLobbyService.JoinUserGameAsync(ircUser).HandleTask();
        }

        private static Point GetCursorPoint(XNAControl control)
        {
            Point controlCursorPoint = control.GetCursorPoint();
            Point controlWindowPoint = control.GetWindowPoint();
            return new Point(controlWindowPoint.X + controlCursorPoint.X, controlWindowPoint.Y + controlCursorPoint.Y);
        }

        private async ValueTask InviteAsync()
        {
            // note it's assumed that if the channel name is specified, the game name must be also
            if (string.IsNullOrEmpty(contextMenuData.inviteChannelName) || ProgramConstants.IsInGame)
            {
                return;
            }

            string messageBody = CnCNetCommands.GAME_INVITE + " " + contextMenuData.inviteChannelName + ";" + contextMenuData.inviteGameName;

            if (!string.IsNullOrEmpty(contextMenuData.inviteChannelPassword))
            {
                messageBody += ";" + contextMenuData.inviteChannelPassword;
            }

            await connectionManager.SendCustomMessageAsync(new QueuedMessage(
                IRCCommands.PRIVMSG + " " + GetIrcUser().Name + " :\u0001" + messageBody + "\u0001", QueuedMessageType.CHAT_MESSAGE, 0)).ConfigureAwait(false);
        }

        private void UpdateButtons()
        {
            UpdatePlayerBasedButtons();
            UpdateMessageBasedButtons();
        }

        private void UpdatePlayerBasedButtons()
        {
            var ircUser = GetIrcUser();
            var isOnline = ircUser != null && connectionManager.UserList.Any(u => u.Name == ircUser.Name);
            var isAdmin = contextMenuData.ChannelUser?.IsAdmin ?? false;

            toggleFriendItem.Visible = ircUser != null;
            privateMessageItem.Visible = ircUser != null && isOnline;
            toggleIgnoreItem.Visible = ircUser != null;
            invitePlayerItem.Visible = ircUser != null && isOnline && !string.IsNullOrEmpty(contextMenuData.inviteChannelName);
            joinPlayerItem.Visible = ircUser != null && !contextMenuData.PreventJoinGame && isOnline;

            toggleIgnoreItem.Selectable = !isAdmin;

            if (ircUser == null)
                return;

            toggleFriendItem.Text = cncnetUserData.IsFriend(ircUser.Name) ? REMOVE_FRIEND : ADD_FRIEND;
            toggleIgnoreItem.Text = cncnetUserData.IsIgnored(ircUser.Ident) ? UNBLOCK : BLOCK;
        }

        private void UpdateMessageBasedButtons()
        {
            var link = contextMenuData?.ChatMessage?.Message?.GetLink();

            copyLinkItem.Visible = link != null;
            openLinkItem.Visible = link != null;

            copyLinkItem.SelectAction = () =>
            {
                if (link == null)
                    return;
                CopyLink(link);
            };
            openLinkItem.SelectAction = () =>
            {
                if (link == null)
                    return;

                ProcessLauncher.StartShellProcess(link);
            };
        }

        private void CopyLink(string link)
        {
            try
            {
                ClipboardService.SetText(link);
            }
            catch (Exception ex)
            {
                ProgramConstants.LogException(ex, "Unable to copy link.");
                XNAMessageBox.Show(WindowManager, "Error".L10N("UI:Main:Error"), "Unable to copy link".L10N("UI:Main:ClipboardCopyLinkFailed"));
            }
        }

        private async ValueTask GetIrcUserIdentAsync(Action<string> callback)
        {
            var ircUser = GetIrcUser();

            if (!string.IsNullOrEmpty(ircUser.Ident))
            {
                callback.Invoke(ircUser.Ident);
                return;
            }

            void WhoIsReply(object sender, WhoEventArgs whoEventargs)
            {
                ircUser.Ident = whoEventargs.Ident;
                callback.Invoke(whoEventargs.Ident);
                connectionManager.WhoReplyReceived -= WhoIsReply;
            }

            connectionManager.WhoReplyReceived += WhoIsReply;
            await connectionManager.SendWhoIsMessageAsync(ircUser.Name).ConfigureAwait(false);
        }

        private IRCUser GetIrcUser()
        {
            if (contextMenuData.IrcUser != null)
                return contextMenuData.IrcUser;

            if (contextMenuData.ChannelUser?.IRCUser != null)
                return contextMenuData.ChannelUser.IRCUser;

            if (!string.IsNullOrEmpty(contextMenuData.PlayerName))
                return connectionManager.UserList.Find(u => u.Name == contextMenuData.PlayerName);

            if (!string.IsNullOrEmpty(contextMenuData.ChatMessage?.SenderName))
                return connectionManager.UserList.Find(u => u.Name == contextMenuData.ChatMessage.SenderName);

            return null;
        }

        public void Show(GlobalContextMenuData2 data)
        {
            if (data == null)
                return;

            Disable();
            contextMenuData = data;
            UpdateButtons();

            if (!Items.Any(i => i.Visible))
                return;

            Open(GetCursorPoint(data.ParentControl));
        }
    }
}