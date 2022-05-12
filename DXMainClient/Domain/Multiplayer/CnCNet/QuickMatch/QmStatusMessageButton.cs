using System;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmStatusMessageButton
    {
        public string Text { get; }

        public Action Action { get; }

        public QmStatusMessageButton(string message, Action action)
        {
            Text = message;
            Action = action;
        }
    }
}
