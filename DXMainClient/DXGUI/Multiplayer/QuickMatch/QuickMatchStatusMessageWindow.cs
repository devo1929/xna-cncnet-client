using System;
using ClientGUI;
using DTAClient.Domain.Multiplayer.CnCNet.QuickMatch;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.QuickMatch
{
    public class QuickMatchStatusMessageWindow : INItializableWindow
    {
        private int DefaultInternalWidth;

        private XNAPanel statusWindowInternal { get; set; }

        private XNALabel statusMessage { get; set; }

        private Action buttonAction { get; set; }

        public QuickMatchStatusMessageWindow(WindowManager windowManager) : base(windowManager)
        {
        }

        public override void Initialize()
        {
            base.Initialize();

            statusWindowInternal = FindChild<XNAPanel>(nameof(statusWindowInternal));
            DefaultInternalWidth = statusWindowInternal.ClientRectangle.Width;

            statusMessage = FindChild<XNALabel>(nameof(statusMessage));
        }

        public void Show(string message) => Show(new QmStatusMessageEventArgs(message));

        public void Show(QmStatusMessageEventArgs statusMessageEventArgs)
        {
            statusMessage.Text = statusMessageEventArgs.Message;
            buttonAction = statusMessageEventArgs.ButtonAction;

            ResizeForText();
            Enable();
        }

        private void ResizeForText()
        {
            var textDimensions = Renderer.GetTextDimensions(statusMessage.Text, statusMessage.FontIndex);

            statusWindowInternal.Width = (int)Math.Max(DefaultInternalWidth, textDimensions.X + 60);
            statusWindowInternal.X = (Width / 2) - (statusWindowInternal.Width / 2);
        }

        private void Button_LeftClick(object sender, EventArgs eventArgs)
        {
            Disable();
            buttonAction?.Invoke();
        }
    }
}
