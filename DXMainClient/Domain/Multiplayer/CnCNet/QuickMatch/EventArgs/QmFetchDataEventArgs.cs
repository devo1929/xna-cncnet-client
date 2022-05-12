using System;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmFetchDataEventArgs : EventArgs
    {
        public bool IsSuccess { get; set; }

        public QmFetchDataEventArgs(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }
}
