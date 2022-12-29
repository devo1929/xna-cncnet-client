using System.Collections.Generic;
using DTAClient.Enums;
using Localization;

namespace DTAClient.DXGUI.ViewModels;

public class CnCNetClientViewModel
{
    public SwitchType LastSwitchType;
    public bool LanMode;
    public List<ISwitchable> PrimarySwitches = new();
    public ISwitchable CncnetLobbySwitch;
    public ISwitchable PrivateMessageSwitch;
    public bool IsUpdateInProgress;
    public GameProcessStateEnum GameProcessState;
}