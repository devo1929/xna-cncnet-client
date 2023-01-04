using DTAClient.DXGUI;
using DTAClient.DXGUI.Generic;
using DTAClient.Enums;

namespace DTAClient.ViewModels;

public class ClientViewModel
{
    public GameProcessStateEnum GameProcessState { get; set; }

    public ClientViewModel()
    {
        GameProcessState = GameProcessStateEnum.Exited;
    }
}