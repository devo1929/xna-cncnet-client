using System;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmStatusMessageEventArgs : EventArgs
    {
        public string Message { get; }

        public string ButtonText { get; }

        public Action ButtonAction { get; }

        public QmStatusMessageEventArgs(string message, string buttonText = null, Action buttonAction = null)
        {
            Message = message;
            ButtonText = buttonText;
            ButtonAction = buttonAction;
        }
    }
}
