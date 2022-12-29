using Rampastring.XNAUI.XNAControls;
using Rampastring.XNAUI;
using DTAClient.Online;
using Microsoft.Xna.Framework;
using System;
using ClientCore;
using ClientCore.Extensions;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.DXGUI.Services;

namespace DTAClient.DXGUI.Multiplayer
{
    /// <summary>
    /// A list box for CnCNet chat. Supports opening links with a double-click,
    /// and easy adding of IRC messages to the list box.
    /// </summary>
    public class ChatListBox2 : XNAListBox
    {
        private readonly CnCNetLobbyService cncnetLobbyService;
        private readonly CnCNetClientService cncnetClientService;

        public ChatListBox2(
            WindowManager windowManager,
            CnCNetLobbyService cncnetLobbyService,
            CnCNetClientService cncnetClientService
        ) : base(windowManager)
        {
            this.cncnetLobbyService = cncnetLobbyService;
            this.cncnetClientService = cncnetClientService;
            DoubleLeftClick += ChatListBox_DoubleLeftClick;
            RightClick += ChatListBox_RightClick;
        }

        public override void Initialize()
        {
            base.Initialize();

            cncnetLobbyService.GetCurrentChatChannel().Subscribe(CurrentChatChannelUpdated);
            cncnetLobbyService.GetMessages().Subscribe(AddMessage);
        }

        private void CurrentChatChannelUpdated(Channel _)
        {
            TopIndex = 0;
            Clear();
        }

        private void ChatListBox_DoubleLeftClick(object sender, EventArgs e)
        {
            if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
                return;

            var link = Items[SelectedIndex].Text?.GetLink();
            if (link == null)
                return;

            ProcessLauncher.StartShellProcess(link);
        }
        
        private void ChatListBox_RightClick(object sender, EventArgs e)
        {
            SelectedIndex = HoveredIndex;
            if (SelectedIndex == -1)
                return;
            
            cncnetClientService.ShowContextMenu(new GlobalContextMenuData2()
            {
                ChatMessage = (ChatMessage)SelectedItem.Tag,
                ParentControl = this
            });
        }

        private void AddMessage(ChatMessage message)
        {
            var listBoxItem = new XNAListBoxItem { TextColor = message.Color, Selectable = true, Tag = message };

            if (message.SenderName == null)
            {
                listBoxItem.Text = Renderer.GetSafeString(string.Format("[{0}] {1}",
                    message.DateTime.ToShortTimeString(),
                    message.Message), FontIndex);
            }
            else
            {
                listBoxItem.Text = Renderer.GetSafeString(string.Format("[{0}] {1}: {2}",
                    message.DateTime.ToShortTimeString(), message.SenderName, message.Message), FontIndex);
            }

            AddItem(listBoxItem);

            if (LastIndex >= Items.Count - 2)
            {
                ScrollToBottom();
            }
        }
    }
}