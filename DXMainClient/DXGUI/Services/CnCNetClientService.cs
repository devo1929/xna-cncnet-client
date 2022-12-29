using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using ClientCore.CnCNet5;
using ClientGUI;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using DTAClient.DXGUI.ViewModels;
using DTAClient.Enums;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using SixLabors.ImageSharp;

namespace DTAClient.DXGUI.Services;

public class CnCNetClientService
{
    private readonly GameCollection gameCollection;
    private readonly Texture2D adminGameIcon;
    private readonly Texture2D unknownGameIcon;
    private readonly Texture2D badgeGameIcon = AssetLoader.LoadTexture("Badges/badge.png");
    private readonly Texture2D friendIcon = AssetLoader.LoadTexture("friendicon.png");
    private readonly Texture2D ignoreIcon = AssetLoader.LoadTexture("ignoreicon.png");
    private readonly BehaviorSubject<CnCNetClientViewModel> viewModelSubject = new(new CnCNetClientViewModel());
    private readonly BehaviorSubject<GlobalContextMenuData2> showContextMenuSubject = new(null);
    private readonly BehaviorSubject<PromptWindowEnum> promptWindowSubject = new(PromptWindowEnum.None);
    private readonly BehaviorSubject<GameProcessStateEnum> gameProcessStartingSubject = new(GameProcessStateEnum.Exited);
    private bool _updateInProgress;

    private CnCNetClientViewModel viewModel => viewModelSubject.Value;

    public CnCNetClientService(
        GameCollection gameCollection
    )
    {
        this.gameCollection = gameCollection;

        var assembly = Assembly.GetAssembly(typeof(GameCollection));
        using Stream cncnetIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.cncneticon.png");
        using Stream unknownIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.unknownicon.png");
        adminGameIcon = AssetLoader.TextureFromImage(Image.Load(cncnetIconStream));
        unknownGameIcon = AssetLoader.TextureFromImage(Image.Load(unknownIconStream));

        GameProcessLogic.GameProcessStarted += () => SetGameProcessState(GameProcessStateEnum.Started);
        GameProcessLogic.GameProcessStarting += () => SetGameProcessState(GameProcessStateEnum.Starting);
        GameProcessLogic.GameProcessExited += () => SetGameProcessState(GameProcessStateEnum.Exited);
    }

    public Texture2D GetGameIcon(int gameId)
    {
        if (gameId < 0 || gameId >= gameCollection.GameList.Count)
            return unknownGameIcon;

        return gameCollection.GameList[gameId].Texture;
    }

    public Texture2D GetAdminGameIcon() => adminGameIcon;
    public Texture2D GetBadgeGameIcon() => badgeGameIcon;
    public Texture2D GetFriendIcon() => friendIcon;
    public Texture2D GetIgnoreIcon() => ignoreIcon;

    public void ShowContextMenu(GlobalContextMenuData2 menuData) => showContextMenuSubject.OnNext(menuData);

    public IObservable<CnCNetClientViewModel> GetViewModel() => viewModelSubject.AsObservable();

    public IObservable<GlobalContextMenuData2> GetShowContextMenu() => showContextMenuSubject.AsObservable();

    public IObservable<PromptWindowEnum> GetPromptWindow() => promptWindowSubject.AsObservable();

    public IObservable<GameProcessStateEnum> GetGameProcessStarting() => gameProcessStartingSubject.AsObservable();

    public void ShowOptionsWindow()
    {
        promptWindowSubject.OnNext(PromptWindowEnum.Options);
        viewModel.PrivateMessageSwitch.SwitchOff();
        RefreshModel();
    }

    public void AddPrimarySwitchable(ISwitchable switchable)
    {
        viewModel.PrimarySwitches.Add(switchable);
        RefreshModel();
    }

    public void RemovePrimarySwitchable(ISwitchable switchable)
    {
        viewModel.PrimarySwitches.Remove(switchable);
        RefreshModel();
    }

    public void SetSecondarySwitch(ISwitchable switchable)
    {
        viewModel.CncnetLobbySwitch = switchable;
        RefreshModel();
    }

    public void SetTertiarySwitch(ISwitchable switchable)
    {
        viewModel.PrivateMessageSwitch = switchable;
        RefreshModel();
    }

    public void ShowCncnetLobby()
    {
        viewModel.LastSwitchType = SwitchType.SECONDARY;
        viewModel.PrimarySwitches[^1].SwitchOff();
        viewModel.CncnetLobbySwitch.SwitchOn();
        viewModel.PrivateMessageSwitch.SwitchOff();

        // HACK warning
        // TODO: add a way for DarkeningPanel to skip transitions
        ((DarkeningPanel)((XNAControl)viewModel.CncnetLobbySwitch).Parent).Alpha = 1.0f;
        RefreshModel();
    }

    public void ShowMainMenu()
    {
        if (viewModel.PrimarySwitches.Count == 0)
            return;

        viewModel.LastSwitchType = SwitchType.PRIMARY;
        viewModel.CncnetLobbySwitch.SwitchOff();
        viewModel.PrivateMessageSwitch.SwitchOff();
        viewModel.PrimarySwitches[^1].SwitchOn();

        // HACK warning
        // TODO: add a way for DarkeningPanel to skip transitions
        if (((XNAControl)viewModel.PrimarySwitches[^1]).Parent is DarkeningPanel darkeningPanel)
            darkeningPanel.Alpha = 1.0f;
        RefreshModel();
    }

    public void ShowPrivateMessages()
        => viewModel.PrivateMessageSwitch.SwitchOn();

    public ISwitchable GetTopMostPrimarySwitchable() => viewModel.PrimarySwitches[^1];

    public void SetUpdateInProgress(bool isUpdateInProgress)
    {
        viewModel.IsUpdateInProgress = isUpdateInProgress;
        RefreshModel();
    }

    private void SetGameProcessState(GameProcessStateEnum gameProcessState)
    {
        viewModel.GameProcessState = gameProcessState;
        RefreshModel();
    }

    private void RefreshModel()
    {
        viewModelSubject.OnNext(viewModel);
    } 
}