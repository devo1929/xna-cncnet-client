using System;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmStatusMessage : EventArgs
    {
        public string Message { get; set; }
    }
}
