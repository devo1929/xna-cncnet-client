using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ClientGUI;
using DTAClient.DXGUI;
using DTAClient.DXGUI.Generic;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.Enums;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using DTAClient.ViewModels;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.Services;

public class TopBarService
{
    private readonly CnCNetManager connectionManager;
    private readonly BehaviorSubject<TopBarViewModel> clientViewModelSubject = new(new TopBarViewModel());
    private OptionsWindow optionsWindow;
    private PrivateMessagingWindow privateMessagingWindow;
    private CnCNetLobby cncnetLobby;

    private TopBarViewModel viewModel => clientViewModelSubject.Value;

    public TopBarService(
        CnCNetManager connectionManager
    )
    {
        this.connectionManager = connectionManager;
        connectionManager.Connected += ConnectionManager_Connected;
        connectionManager.Disconnected += ConnectionManager_Disconnected;
        connectionManager.ConnectionLost += ConnectionManager_ConnectionLost;
        connectionManager.WelcomeMessageReceived += ConnectionManager_WelcomeMessageReceived;
        connectionManager.AttemptedServerChanged += ConnectionManager_AttemptedServerChanged;
        connectionManager.ConnectAttemptFailed += ConnectionManager_ConnectAttemptFailed;
    }

    private void ConnectionManager_Connected(object sender, EventArgs e)
    {
        viewModel.CncnetConectionStatus = CnCNetConnectionStatusEnum.Connected;
        RefreshViewModel();
    }


    private void ConnectionManager_ConnectionLost(object sender, ConnectionLostEventArgs e)
    {
        if (!viewModel.IsViewingLanLobby)
            SetConnectionStatus(CnCNetConnectionStatusEnum.Offline);
    }

    private void ConnectionManager_ConnectAttemptFailed(object sender, EventArgs e)
    {
        if (!viewModel.IsViewingLanLobby)
            SetConnectionStatus(CnCNetConnectionStatusEnum.Offline);
    }

    private void ConnectionManager_AttemptedServerChanged(object sender, AttemptedServerEventArgs e)
        => SetConnectionStatus(CnCNetConnectionStatusEnum.Connecting);

    private void ConnectionManager_WelcomeMessageReceived(object sender, ServerMessageEventArgs e)
        => SetConnectionStatus(CnCNetConnectionStatusEnum.Connected);

    private void ConnectionManager_Disconnected(object sender, EventArgs e)
    {
        if (!viewModel.IsViewingLanLobby)
            SetConnectionStatus(CnCNetConnectionStatusEnum.Offline);
    }

    private void SetConnectionStatus(CnCNetConnectionStatusEnum connectionStatus)
    {
        viewModel.CncnetConectionStatus = connectionStatus;
        RefreshViewModel();
    }

    public IObservable<TopBarViewModel> GetViewModel() => clientViewModelSubject.AsObservable();

    public void AddPrimarySwitchable(ISwitchable switchable)
    {
        if (!viewModel.Switchables.Contains(switchable))
            viewModel.Switchables.Add(switchable);
        RefreshViewModel();
    }

    public void RemoveSwitchable(ISwitchable switchable)
    {
        switchable.SwitchOff();
        viewModel.Switchables.Remove(switchable);
        RefreshViewModel();
    }


    // public void SetPrimarySwitchable(ISwitchable switchable)
    // {
    //     viewModel.PrimarySwitch = switchable;
    //     SwitchToPrimary();
    //     RefreshViewModel();
    // }
    //
    // public void SetSecondarySwitchable(ISwitchable switchable)
    // {
    //     viewModel.SecondarySwitch = switchable;
    //     RefreshViewModel();
    // }
    //
    // public void SetTertiarySwitchable(ISwitchable switchable)
    // {
    //     viewModel.TertiarySwitch = switchable;
    //     RefreshViewModel();
    // }

    public void SetCncNetLobby(CnCNetLobby cl) => cncnetLobby = cl;

    public void SetOptionsWindow(OptionsWindow ow) => optionsWindow = ow;

    public void SetPrivateMessagingWindow(PrivateMessagingWindow pmw) => privateMessagingWindow = pmw;

    public void SwitchToPrimary() => SwitchToSwitchable(viewModel.ActiveSwitchable);
    
    public void SwitchToCncNetLobby() => cncnetLobby.Open();

    // public void SwitchToOptions()
    // {
    //     viewModel.TertiarySwitch?.SwitchOff();
    //     SwitchToSwitchable(viewModel.OptionsSwitch);
    // }

    public void SwitchToSwitchable(ISwitchable switchable)
    {
        viewModel.ActiveSwitchable = switchable;
        if (switchable == null)
            return;

        viewModel.Switchables.ForEach(s => s.SwitchOff());

        switchable.SwitchOn();

        if (viewModel.IsViewingLanLobby)
            SetConnectionStatus(CnCNetConnectionStatusEnum.LanMode);
        else if (connectionManager.IsConnected)
            SetConnectionStatus(CnCNetConnectionStatusEnum.Connected);
        else
            SetConnectionStatus(CnCNetConnectionStatusEnum.Offline);

        // HACK warning
        // TODO: add a way for DarkeningPanel to skip transitions
        if (((XNAControl)switchable).Parent is DarkeningPanel darkeningPanel)
            darkeningPanel.Alpha = 1.0f;

        RefreshViewModel();
    }

    private void RefreshViewModel() => clientViewModelSubject.OnNext(viewModel);
}