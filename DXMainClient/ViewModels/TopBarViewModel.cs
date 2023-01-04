using System.Collections.Generic;
using System.Linq;
using DTAClient.DXGUI;
using DTAClient.DXGUI.Generic;
using DTAClient.DXGUI.Multiplayer;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.Enums;

namespace DTAClient.ViewModels;

public class TopBarViewModel
{
    public readonly List<ISwitchable> Switchables = new();

    public ISwitchable ActiveSwitchable { get; set; }

    public bool IsViewingMainMenu => ActiveSwitchable?.GetType() == typeof(MainMenu);

    public bool IsViewingLanLobby => ActiveSwitchable?.GetType() == typeof(LANLobby);

    public CnCNetConnectionStatusEnum CncnetConectionStatus { get; set; } = CnCNetConnectionStatusEnum.Offline;
}