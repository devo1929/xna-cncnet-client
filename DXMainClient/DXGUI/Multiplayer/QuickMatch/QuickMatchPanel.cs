using System;
using ClientGUI;
using Rampastring.XNAUI;

namespace DTAClient.DXGUI.Multiplayer.QuickMatch
{
    public abstract class QuickMatchPanel : INItializableWindow
    {
        public event EventHandler Exit;

        protected QuickMatchPanel(WindowManager windowManager) : base(windowManager)
        {
        }

        protected void Exit_Click(object sender, EventArgs args) => Exit?.Invoke(sender, args);

        protected void ShowError(string error, string title = "Error")
            => XNAMessageBox.Show(WindowManager, title, error);
    }
}
