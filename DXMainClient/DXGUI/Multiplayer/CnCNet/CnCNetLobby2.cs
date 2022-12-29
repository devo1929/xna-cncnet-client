using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ClientCore;
using ClientCore.CnCNet5;
using ClientCore.Enums;
using ClientCore.Extensions;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.DXGUI.Generic;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using DTAClient.DXGUI.Multiplayer.GameLobby.CommandHandlers;
using DTAClient.DXGUI.Services;
using DTAClient.DXGUI.ViewModels;
using DTAClient.Extensions;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using SixLabors.ImageSharp;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DTAClient.DXGUI.Multiplayer.CnCNet;

using UserChannelPair = Tuple<string, string>;
using InvitationIndex = Dictionary<Tuple<string, string>, WeakReference>;

internal sealed class CnCNetLobby2 : XNAWindow, ISwitchable
{
    public CnCNetLobby2(
        WindowManager windowManager,
        // CnCNetGameLobby gameLobby,
        // CnCNetGameLoadingLobby gameLoadingLobby,
        // TopBar topBar,
        PrivateMessagingWindow pmWindow,
        CnCNetClientService cncnetClientService,
        CnCNetLobbyService cncnetLobbyService,
        IServiceProvider serviceProvider
    )
        : base(windowManager)
    {
        // this.gameLobby = gameLobby;
        // this.gameLoadingLobby = gameLoadingLobby;
        // this.topBar = topBar;
        this.pmWindow = pmWindow;
        this.cncnetClientService = cncnetClientService;
        this.cncnetLobbyService = cncnetLobbyService;
        this.serviceProvider = serviceProvider;

        // topBar.LogoutEvent += LogoutEvent;
    }

    private readonly CnCNetLobbyService cncnetLobbyService;
    private readonly IServiceProvider serviceProvider;
    private CnCNetLobbyViewModel viewModel;

    // private readonly CnCNetGameLobby gameLobby;
    // private readonly CnCNetGameLoadingLobby gameLoadingLobby;
    // private readonly TopBar topBar;
    private readonly PrivateMessagingWindow pmWindow;
    private readonly CnCNetClientService cncnetClientService;
    private PlayerListBox2 lbPlayerList;
    private ChatListBox2 lbChatMessages;
    private GameListBox2 lbGameList;
    private GlobalContextMenu2 globalContextMenu;
    private XNAClientButton btnLogout;
    private XNAClientButton btnNewGame;
    private XNAClientButton btnJoinGame;
    private XNAChatTextBox tbChatInput;
    private XNALabel lblColor;
    private XNALabel lblCurrentChannel;
    private XNALabel lblOnline;
    private XNALabel lblOnlineCount;
    private XNAClientDropDown ddColor;
    private XNAClientDropDown ddCurrentChannel;
    private XNASuggestionTextBox tbGameSearch;
    private XNAClientStateButton<SortDirection> btnGameSortAlpha;
    private XNAClientToggleButton btnGameFilterOptions;
    private DarkeningPanel gameCreationPanel;
    private CnCNetLoginWindow2 loginWindow;
    private PasswordRequestWindow2 passwordRequestWindow;
    private GameFiltersPanel panelGameFilters;

    public void RefreshSearchBox() => tbGameSearch.Text = viewModel.GameFilterViewModel.Search;

    private void RefreshCurrentChannelDropdown()
    {
        if (ddCurrentChannel == null)
            return;

        ddCurrentChannel.Items.Clear();
        foreach (CnCNetGame cnCNetGame in viewModel.Games)
        {
            ddCurrentChannel.AddItem(new XNADropDownItem()
            {
                Text = cnCNetGame.UIName,
                Texture = cnCNetGame.Texture,
                Tag = viewModel.GameChatChannels[cnCNetGame]
            });
        }

        ddCurrentChannel.SelectedIndex = viewModel.LocalGameIndex;
    }

    private void RefreshIrcColors()
    {
        foreach (IRCColor color in viewModel.IrcColors)
        {
            if (!color.Selectable)
                continue;

            var ddItem = new XNADropDownItem();
            ddItem.Text = color.Name;
            ddItem.TextColor = color.XnaColor;
            ddItem.Tag = color;

            ddColor.AddItem(ddItem);
        }
    }
        
    private void GameList_ClientRectangleUpdated(object sender, EventArgs e) 
        => panelGameFilters.ClientRectangle = lbGameList.ClientRectangle;

    public override void Initialize()
    {
        ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX - 64,
            WindowManager.RenderResolutionY - 64);

        Name = nameof(CnCNetLobby);
        BackgroundTexture = AssetLoader.LoadTexture("cncnetlobbybg.png");

        btnNewGame = new XNAClientButton(WindowManager);
        btnNewGame.Name = nameof(btnNewGame);
        btnNewGame.ClientRectangle = new Rectangle(12, Height - 29, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
        btnNewGame.Text = "Create Game".L10N("UI:Main:CreateGame");
        // btnNewGame.LeftClick += BtnNewGame_LeftClick;

        btnJoinGame = new XNAClientButton(WindowManager);
        btnJoinGame.Name = nameof(btnJoinGame);
        btnJoinGame.ClientRectangle = new Rectangle(btnNewGame.Right + 12,
            btnNewGame.Y, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
        btnJoinGame.Text = "Join Game".L10N("UI:Main:JoinGame");
        btnJoinGame.LeftClick += (_, _) => JoinSelectedGame();

        btnLogout = new XNAClientButton(WindowManager);
        btnLogout.Name = nameof(btnLogout);
        btnLogout.ClientRectangle = new Rectangle(Width - 145, btnNewGame.Y,
            UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
        btnLogout.Text = "Log Out".L10N("UI:Main:ButtonLogOut");
        // btnLogout.LeftClick += (_, _) => BtnLogout_LeftClickAsync().HandleTask();

        var gameListRectangle = new Rectangle(
            btnNewGame.X, 41,
            btnJoinGame.Right - btnNewGame.X, btnNewGame.Y - 47);

        panelGameFilters = serviceProvider.GetControl<GameFiltersPanel>();
        panelGameFilters.ClientRectangle = gameListRectangle;

        lbGameList = serviceProvider.GetControl<GameListBox2>();
        lbGameList.Name = nameof(lbGameList);
        lbGameList.ClientRectangle = gameListRectangle;
        lbGameList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
        // lbGameList.DoubleLeftClick += (_, _) => JoinSelectedGame();
        lbGameList.ClientRectangleUpdated += GameList_ClientRectangleUpdated;

        lbPlayerList = serviceProvider.GetControl<PlayerListBox2>();
        lbPlayerList.Name = nameof(lbPlayerList);
        lbPlayerList.ClientRectangle = new Rectangle(Width - 202,
            20, 190,
            btnLogout.Y - 26);
        // lbPlayerList.DoubleLeftClick += LbPlayerList_DoubleLeftClick;
        // lbPlayerList.RightClick += LbPlayerList_RightClick;

        globalContextMenu = serviceProvider.GetControl<GlobalContextMenu2>();
        // globalContextMenu.JoinEvent += (_, args) => JoinUserAsync(args.IrcUser, connectionManager.MainChannel).HandleTask();

        lbChatMessages = serviceProvider.GetControl<ChatListBox2>();
        lbChatMessages.Name = nameof(lbChatMessages);
        lbChatMessages.ClientRectangle = new Rectangle(lbGameList.Right + 12, lbGameList.Y,
            lbPlayerList.X - lbGameList.Right - 24, lbPlayerList.Height);
        lbChatMessages.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
        lbChatMessages.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
        lbChatMessages.LineHeight = 16;
        lbChatMessages.LeftClick += (_, _) => lbGameList.SelectedIndex = -1;
        // lbChatMessages.RightClick += LbChatMessages_RightClick;

        tbChatInput = new XNAChatTextBox(WindowManager);
        tbChatInput.Name = nameof(tbChatInput);
        tbChatInput.ClientRectangle = new Rectangle(lbChatMessages.X,
            btnNewGame.Y, lbChatMessages.Width,
            btnNewGame.Height);
        tbChatInput.Suggestion = "Type here to chat...".L10N("UI:Main:ChatHere");
        tbChatInput.Enabled = false;
        tbChatInput.MaximumTextLength = 200;
        tbChatInput.EnterPressed += (_, _) => TbChatInput_EnterPressedAsync().HandleTask();

        lblColor = new XNALabel(WindowManager);
        lblColor.Name = nameof(lblColor);
        lblColor.ClientRectangle = new Rectangle(lbChatMessages.X, 14, 0, 0);
        lblColor.FontIndex = 1;
        lblColor.Text = "YOUR COLOR:".L10N("UI:Main:YourColor");

        ddColor = new XNAClientDropDown(WindowManager);
        ddColor.Name = nameof(ddColor);
        ddColor.ClientRectangle = new Rectangle(lblColor.X + 95, 12,
            150, 21);

        int selectedColor = UserINISettings.Instance.ChatColor;

        ddColor.SelectedIndex = selectedColor >= ddColor.Items.Count || selectedColor < 0
            ? ClientConfiguration.Instance.DefaultPersonalChatColorIndex :
            selectedColor;
        // SetChatColor();
        // ddColor.SelectedIndexChanged += DdColor_SelectedIndexChanged;

        ddCurrentChannel = new XNAClientDropDown(WindowManager);
        ddCurrentChannel.Name = nameof(ddCurrentChannel);
        ddCurrentChannel.ClientRectangle = new Rectangle(
            lbChatMessages.Right - 200,
            ddColor.Y, 200, 21);
        ddCurrentChannel.SelectedIndexChanged += (_, _) => DdCurrentChannel_SelectedIndexChangedAsync().HandleTask();
        ddCurrentChannel.AllowDropDown = false;

        lblCurrentChannel = new XNALabel(WindowManager);
        lblCurrentChannel.Name = nameof(lblCurrentChannel);
        lblCurrentChannel.ClientRectangle = new Rectangle(
            ddCurrentChannel.X - 150,
            ddCurrentChannel.Y + 2, 0, 0);
        lblCurrentChannel.FontIndex = 1;
        lblCurrentChannel.Text = "CURRENT CHANNEL:".L10N("UI:Main:CurrentChannel");

        lblOnline = new XNALabel(WindowManager);
        lblOnline.Name = nameof(lblOnline);
        lblOnline.ClientRectangle = new Rectangle(310, 14, 0, 0);
        lblOnline.Text = "Online:".L10N("UI:Main:OnlineLabel");
        lblOnline.FontIndex = 1;
        lblOnline.Disable();

        lblOnlineCount = new XNALabel(WindowManager);
        lblOnlineCount.Name = nameof(lblOnlineCount);
        lblOnlineCount.ClientRectangle = new Rectangle(lblOnline.X + 50, 14, 0, 0);
        lblOnlineCount.FontIndex = 1;
        lblOnlineCount.Disable();

        tbGameSearch = new XNASuggestionTextBox(WindowManager);
        tbGameSearch.Name = nameof(tbGameSearch);
        tbGameSearch.ClientRectangle = new Rectangle(lbGameList.X, 12, lbGameList.Width - 62, 21);
        tbGameSearch.MaximumTextLength = 64;
        // tbGameSearch.InputReceived += TbGameSearch_InputReceived;
        tbGameSearch.Disable();

        btnGameSortAlpha = new XNAClientStateButton<SortDirection>(WindowManager, new Dictionary<SortDirection, Texture2D>()
        {
            { SortDirection.None , AssetLoader.LoadTexture("sortAlphaNone.png")},
            { SortDirection.Asc , AssetLoader.LoadTexture("sortAlphaAsc.png")},
            { SortDirection.Desc , AssetLoader.LoadTexture("sortAlphaDesc.png")},
        });
        btnGameSortAlpha.Name = nameof(btnGameSortAlpha);
        btnGameSortAlpha.ClientRectangle = new Rectangle(
            tbGameSearch.X + tbGameSearch.Width + 10, tbGameSearch.Y,
            21, 21
        );
        // btnGameSortAlpha.LeftClick += BtnGameSortAlpha_LeftClick;
        btnGameSortAlpha.SetToolTipText("Sort Games Alphabetically".L10N("UI:Main:SortAlphabet"));
        // RefreshGameSortAlphaBtn();

        btnGameFilterOptions = new XNAClientToggleButton(WindowManager);
        btnGameFilterOptions.Name = nameof(btnGameFilterOptions);
        btnGameFilterOptions.ClientRectangle = new Rectangle(
            btnGameSortAlpha.X + btnGameSortAlpha.Width + 10, tbGameSearch.Y,
            21, 21
        );
        btnGameFilterOptions.CheckedTexture = AssetLoader.LoadTexture("filterActive.png");
        btnGameFilterOptions.UncheckedTexture = AssetLoader.LoadTexture("filterInactive.png");
        // btnGameFilterOptions.LeftClick += BtnGameFilterOptions_LeftClick;
        btnGameFilterOptions.SetToolTipText("Game Filters");
        // RefreshGameFiltersBtn();

        // InitializeGameList();

        AddChild(btnNewGame);
        AddChild(btnJoinGame);
        AddChild(btnLogout);
        AddChild(lbPlayerList);
        AddChild(lbChatMessages);
        AddChild(lbGameList);
        AddChild(panelGameFilters);
        AddChild(tbChatInput);
        AddChild(lblColor);
        AddChild(ddColor);
        AddChild(lblCurrentChannel);
        AddChild(ddCurrentChannel);
        AddChild(globalContextMenu);
        AddChild(lblOnline);
        AddChild(lblOnlineCount);
        AddChild(tbGameSearch);
        AddChild(btnGameSortAlpha);
        AddChild(btnGameFilterOptions);

        // panelGameFilters.VisibleChanged += GameFiltersPanel_VisibleChanged;

        // CnCNetPlayerCountTask.CnCNetGameCountUpdated += OnCnCNetGameCountUpdated;

        // pmWindow.SetJoinUserAction((user, messageView) => JoinUserAsync(user, messageView).HandleTask());

        base.Initialize();

        WindowManager.CenterControlOnScreen(this);
        cncnetLobbyService.GetViewModel().Subscribe(ViewModelUpdated);
        cncnetLobbyService.GetPromptForPassword().Subscribe(JoinGamePasswordPrompt);
        cncnetLobbyService.GetLogoutBtnText().Subscribe(SetLogoutBtnText);

        RefreshIrcColors();
        RefreshCurrentChannelDropdown();
        PostUIInit();
    }

    private void SetLogoutBtnText(string text) => btnLogout.Text = text;

    private void JoinGamePasswordPrompt(GenericHostedGame hostedGame)
    {
        if (hostedGame == null)
            return;

        passwordRequestWindow.Show(hostedGame);
    }

    private void JoinSelectedGame() => cncnetLobbyService.JoinSelectedGameAsync().HandleTask();

    private async ValueTask TbChatInput_EnterPressedAsync()
    {
        if (string.IsNullOrEmpty(tbChatInput.Text))
            return;

        var selectedColor = (IRCColor)ddColor.SelectedItem.Tag;

        await cncnetLobbyService.SendChatMessageAsync(tbChatInput.Text, selectedColor).ConfigureAwait(false);
        tbChatInput.Text = string.Empty;
    }

    private void ViewModelUpdated(CnCNetLobbyViewModel viewModel)
    {
        this.viewModel = viewModel;

        btnNewGame.Enabled = viewModel.IsNewGameBtnEnabled;
        btnJoinGame.Enabled = viewModel.IsJoinGameBtnEnabled;
        ddCurrentChannel.AllowDropDown = viewModel.IsCurrentChannelDdEnabled;
        tbChatInput.Enabled = viewModel.IsChatTbEnabled;
        btnGameFilterOptions.Checked = viewModel.GameFilterViewModel.IsApplied;
    }

    private void PostUIInit()
    {
        gameCreationPanel = new DarkeningPanel(WindowManager);
        AddChild(gameCreationPanel);

        GameCreationWindow gcw = serviceProvider.GetControl<GameCreationWindow>();
        gameCreationPanel.AddChild(gcw);
        gameCreationPanel.Tag = gcw;
        gameCreationPanel.Hide();

        loginWindow = serviceProvider.GetControl<CnCNetLoginWindow2>();

        var loginWindowPanel = new DarkeningPanel(WindowManager);
        loginWindowPanel.Alpha = 0.0f;

        AddChild(loginWindowPanel);
        loginWindowPanel.AddChild(loginWindow);
        loginWindow.Disable();

        passwordRequestWindow = serviceProvider.GetControl<PasswordRequestWindow2>();

        var passwordRequestWindowPanel = new DarkeningPanel(WindowManager);
        passwordRequestWindowPanel.Alpha = 0.0f;
        AddChild(passwordRequestWindowPanel);
        passwordRequestWindowPanel.AddChild(passwordRequestWindow);
        passwordRequestWindow.Disable();
    }
    
    private async ValueTask DdCurrentChannel_SelectedIndexChangedAsync()
        {
            
        }

    public void SwitchOn()
    {
        Enable();
        cncnetLobbyService.PromptLogin();
    }
    
    public void SwitchOff() => Disable();

    public string GetSwitchName() => "CnCNet Lobby".L10N("UI:Main:CnCNetLobby");
}