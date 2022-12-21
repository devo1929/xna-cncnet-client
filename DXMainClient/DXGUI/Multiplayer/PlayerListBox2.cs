using System;
using System.Collections.Generic;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.DXGUI.Services;
using DTAClient.Online;
using Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer
{
    /// <summary>
    /// A list box for listing the players in the CnCNet lobby.
    /// </summary>
    public class PlayerListBox2 : XNAListBox
    {
        private readonly CnCNetClientService cncnetClientService;
        private readonly CnCNetLobbyService cncnetLobbyService;
        private const int MARGIN = 2;

        private readonly Texture2D adminGameIcon;
        private readonly Texture2D friendIcon;
        private readonly Texture2D ignoreIcon;

        public PlayerListBox2(
            WindowManager windowManager,
            CnCNetClientService cncnetClientService,
            CnCNetLobbyService cncnetLobbyService
        ) : base(windowManager)
        {
            TopIndex = 0;
            this.cncnetClientService = cncnetClientService;
            this.cncnetLobbyService = cncnetLobbyService;

            adminGameIcon = cncnetClientService.GetAdminGameIcon();
            friendIcon = cncnetClientService.GetFriendIcon();
            ignoreIcon = cncnetClientService.GetIgnoreIcon();
        }

        public override void Initialize()
        {
            base.Initialize();

            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            LineHeight = 16;

            RightClick += List_RightClick;
            cncnetLobbyService.GetChannelUsers().Subscribe(SetChannelUsers);
            cncnetLobbyService.GetCurrentChatChannel().Subscribe(CurrentChatChannelUpdated);
        }

        private void List_RightClick(object sender, EventArgs args)
        {
            SelectedIndex = HoveredIndex;
            if (SelectedIndex == -1)
                return;

            cncnetClientService.ShowContextMenu(new GlobalContextMenuData() { ChannelUser = (ChannelUser)SelectedItem.Tag, ParentControl = this });
        }

        private void SetChannelUsers(List<ChannelUser> channelUsers)
        {
            Items.Clear();
            foreach (ChannelUser channelUser in channelUsers)
                AddItem(CreateListBoxItem(channelUser));
        }

        private void CurrentChatChannelUpdated(Channel _)
        {
            TopIndex = 0;
        }

        private XNAListBoxItem CreateListBoxItem(ChannelUser user)
        {
            var item = new XNAListBoxItem { Tag = user, Text = user.IRCUser.Name };

            if (user.IsAdmin)
            {
                item.Text += " " + "(Admin)".L10N("UI:Main:AdminSuffix");
                item.TextColor = Color.Red;
                item.Texture = cncnetClientService.GetAdminGameIcon();
            }
            else
            {
                item.Texture = cncnetClientService.GetGameIcon(user.IRCUser.GameID);
            }

            return item;
        }

        public override void Draw(GameTime gameTime)
        {
            DrawPanel();

            int height = 2 - (ViewTop % LineHeight);

            for (int i = TopIndex; i < Items.Count; i++)
            {
                XNAListBoxItem lbItem = Items[i];
                var user = (ChannelUser)lbItem.Tag;

                if (height > Height)
                    break;

                int x = TextBorderDistance;

                if (i == SelectedIndex)
                {
                    int drawnWidth;

                    if (DrawSelectionUnderScrollbar || !ScrollBar.IsDrawn() || !EnableScrollbar)
                    {
                        drawnWidth = Width - 2;
                    }
                    else
                    {
                        drawnWidth = Width - 2 - ScrollBar.Width;
                    }

                    FillRectangle(new Rectangle(1, height,
                            drawnWidth, lbItem.TextLines.Count * LineHeight),
                        FocusColor);
                }

                DrawTexture(user.IsAdmin ? adminGameIcon : lbItem.Texture, new Rectangle(x, height,
                    adminGameIcon.Width, adminGameIcon.Height), Color.White);

                x += adminGameIcon.Width + MARGIN;

                // Friend Icon
                if (user.IRCUser.IsFriend)
                {
                    DrawTexture(friendIcon,
                        new Rectangle(x, height,
                            friendIcon.Width, friendIcon.Height), Color.White);

                    x += friendIcon.Width + MARGIN;
                }
                // Ignore Icon
                else if (user.IRCUser.IsIgnored && !user.IsAdmin)
                {
                    DrawTexture(ignoreIcon,
                        new Rectangle(x, height,
                            ignoreIcon.Width, ignoreIcon.Height), Color.White);

                    x += ignoreIcon.Width + MARGIN;
                }

                // Badge Icon - coming soon
                /*
                Renderer.DrawTexture(badgeGameIcon,
                    new Rectangle(windowRectangle.X + x, windowRectangle.Y + height,
                    badgeGameIcon.Width, badgeGameIcon.Height), Color.White);

                x += badgeGameIcon.Width + margin;
                */

                // Player Name
                string name = user.IsAdmin ? user.IRCUser.Name + " " + "(Admin)".L10N("UI:Main:AdminSuffix") : user.IRCUser.Name;
                x += lbItem.TextXPadding;

                DrawStringWithShadow(name, FontIndex,
                    new Vector2(x, height),
                    user.IsAdmin ? Color.Red : lbItem.TextColor);

                height += LineHeight;
            }

            if (DrawBorders)
                DrawPanelBorders();

            DrawChildren(gameTime);
        }
    }
}