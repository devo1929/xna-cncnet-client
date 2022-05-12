using System;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmRequestEventArgs : EventArgs
    {
        public readonly QmRequestResponse Response;

        public QmRequestEventArgs(QmRequestResponse qmRequestResponse)
        {
            Response = qmRequestResponse;
        }
    }
}
