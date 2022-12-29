using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ClientCore;
using ClientCore.CnCNet5;
using ClientCore.Enums;
using ClientCore.Extensions;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer.GameLobby.CommandHandlers;
using DTAClient.DXGUI.ViewModels;
using DTAClient.Enums;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using Localization;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;

namespace DTAClient.DXGUI.Services;

public class CnCNetLobbyService
{
    private readonly GameCollection gameCollection;
    private readonly CnCNetManager connectionManager;
    private readonly TunnelHandler tunnelHandler;
    private readonly CnCNetUserData cncnetUserData;
    private string localGameId => ClientConfiguration.Instance.LocalGame;

    private readonly BehaviorSubject<CnCNetLobbyViewModel> viewModelSubject;
    private readonly BehaviorSubject<List<GenericHostedGame>> hostedGamesSubject;
    private readonly BehaviorSubject<List<ChannelUser>> channelUsersSubject;
    private readonly BehaviorSubject<Channel> currentChatChannelSubject;
    private readonly ReplaySubject<ChatMessage> messageSubject;
    private readonly BehaviorSubject<GenericHostedGame> promptForPasswordSubject;
    private readonly BehaviorSubject<SourcePanel> sourcePanelSubject;
    private readonly BehaviorSubject<bool> promptLoginSubject;
    private readonly BehaviorSubject<string> logoutBtnTextSubject;
    private CnCNetLobbyViewModel viewModel => viewModelSubject?.Value;
    private List<GenericHostedGame> hostedGames => hostedGamesSubject?.Value;


    private readonly EnhancedSoundEffect sndGameCreated;
    private readonly EnhancedSoundEffect sndGameInviteReceived;
    private readonly List<string> followedGames = new List<string>();
    private Channel currentChatChannel => currentChatChannelSubject?.Value;

    private CancellationTokenSource gameCheckCancellation;
    private readonly CommandHandlerBase[] ctcpCommandHandlers;

    public CnCNetLobbyService(
        GameCollection gameCollection,
        CnCNetManager connectionManager,
        TunnelHandler tunnelHandler,
        CnCNetUserData cncnetUserData
    )
    {
        this.gameCollection = gameCollection;
        this.connectionManager = connectionManager;
        this.tunnelHandler = tunnelHandler;
        this.cncnetUserData = cncnetUserData;
        sndGameCreated = new EnhancedSoundEffect("gamecreated.wav");
        sndGameInviteReceived = new EnhancedSoundEffect("pm.wav");
        hostedGamesSubject = new BehaviorSubject<List<GenericHostedGame>>(new List<GenericHostedGame>());
        channelUsersSubject = new BehaviorSubject<List<ChannelUser>>(new List<ChannelUser>());
        messageSubject = new ReplaySubject<ChatMessage>();
        currentChatChannelSubject = new BehaviorSubject<Channel>(null);
        promptForPasswordSubject = new BehaviorSubject<GenericHostedGame>(null);
        viewModelSubject = new BehaviorSubject<CnCNetLobbyViewModel>(new CnCNetLobbyViewModel { LocalGame = gameCollection.GameList.Find(g => g.InternalName.ToUpper() == ClientConfiguration.Instance.LocalGame.ToUpper()), IrcColors = this.connectionManager.GetIRCColors().ToList() });
        sourcePanelSubject = new BehaviorSubject<SourcePanel>(SourcePanel.Primary);
        promptLoginSubject = new BehaviorSubject<bool>(false);
        logoutBtnTextSubject = new BehaviorSubject<string>(string.Empty);

        InitializeGameList();
        SetLogOutButtonText();
        UserINISettings.Instance.SettingsSaved += (_, _) => Instance_SettingsSavedAsync().HandleTask();

        connectionManager.Disconnected += ConnectionManager_Disconnected;
        connectionManager.WelcomeMessageReceived += (_, _) => ConnectionManager_WelcomeMessageReceivedAsync().HandleTask();

        // GameProcessLogic.GameProcessStarted += () => SharedUILogic_GameProcessStartedAsync().HandleTask();
        // GameProcessLogic.GameProcessExited += () => SharedUILogic_GameProcessExitedAsync().HandleTask();

        ctcpCommandHandlers = new CommandHandlerBase[] { new StringCommandHandler(CnCNetCommands.GAME_INVITE, (sender, argumentsString) => HandleGameInviteCommandAsync(sender, argumentsString).HandleTask()), new NoParamCommandHandler(CnCNetCommands.GAME_INVITATION_FAILED, HandleGameInvitationFailedNotification) };
    }

    private void HandleGameInvitationFailedNotification(string obj)
    {
        throw new NotImplementedException();
    }

    public void SetCurrentChannel(Channel channel)
    {
        if (currentChatChannel != null)
        {
            currentChatChannel.UserAdded -= RefreshPlayerList;
            currentChatChannel.UserLeft -= RefreshPlayerList;
            currentChatChannel.UserQuitIRC -= RefreshPlayerList;
            currentChatChannel.UserKicked -= RefreshPlayerList;
            currentChatChannel.UserListReceived -= RefreshPlayerList;
            currentChatChannel.MessageAdded -= CurrentChatChannel_MessageAdded;
            currentChatChannel.UserGameIndexUpdated -= CurrentChatChannel_UserGameIndexUpdated;

            if (currentChatChannel.ChannelName != "#cncnet" &&
                currentChatChannel.ChannelName != gameCollection.GetGameChatChannelNameFromIdentifier(viewModel.LocalGame.InternalName))
            {
                // Remove the assigned channels from the users so we don't have ghost users on the PM user list
                currentChatChannel.Users.DoForAllUsers(user =>
                {
                    connectionManager.RemoveChannelFromUser(user.IRCUser.Name, currentChatChannel.ChannelName);
                });

                // await currentChatChannel.LeaveAsync().ConfigureAwait(false);
            }
        }

        currentChatChannelSubject.OnNext(channel);
        currentChatChannel.UserAdded += RefreshPlayerList;
        currentChatChannel.UserLeft += RefreshPlayerList;
        currentChatChannel.UserQuitIRC += RefreshPlayerList;
        currentChatChannel.UserKicked += RefreshPlayerList;
        currentChatChannel.UserListReceived += RefreshPlayerList;
        currentChatChannel.MessageAdded += CurrentChatChannel_MessageAdded;
        currentChatChannel.UserGameIndexUpdated += CurrentChatChannel_UserGameIndexUpdated;
        connectionManager.SetMainChannel(currentChatChannel);

        currentChatChannel.Messages.ForEach(msg => messageSubject.OnNext(msg));

        if (currentChatChannel.ChannelName != "#cncnet" &&
            currentChatChannel.ChannelName != gameCollection.GetGameChatChannelNameFromIdentifier(localGameId))
        {
            currentChatChannel.JoinAsync().HandleTask();
        }
    }

    private void CurrentChatChannel_UserGameIndexUpdated(object sender, ChannelUserEventArgs e)
    {
        // throw new NotImplementedException();
    }

    private void CurrentChatChannel_MessageAdded(object sender, IRCMessageEventArgs e) => messageSubject.OnNext(e.Message);

    private void RefreshPlayerList(object sender, EventArgs e)
    {
        var users = new List<ChannelUser>();
        LinkedListNode<ChannelUser> current = currentChatChannel.Users.GetFirst();
        while (current != null)
        {
            ChannelUser user = current.Value;
            user.IRCUser.IsFriend = cncnetUserData.IsFriend(user.IRCUser.Name);
            user.IRCUser.IsIgnored = cncnetUserData.IsIgnored(user.IRCUser.Ident);
            users.Add(user);
            current = current.Next;
        }

        channelUsersSubject.OnNext(users);
    }

    private async ValueTask HandleGameInviteCommandAsync(string sender, string argumentsString)
    {
    }

    public async ValueTask JoinSelectedGameAsync() => await JoinGameAsync(viewModel.SelectedGame).ConfigureAwait(false);

    public async ValueTask JoinUserGameAsync(IRCUser ircUser)
    {
        SetSelectedGame(FindGameForIrcUser(ircUser));
        
        await JoinSelectedGameAsync().ConfigureAwait(false);
    }

    public async ValueTask JoinGameAsync(GenericHostedGame hostedGame, string password = null)
    {
        var hostedCncnetGame = (HostedCnCNetGame)hostedGame;
        string error = GetJoinGameError(hostedGame);
        if (!string.IsNullOrEmpty(error))
        {
            PostMessage(new ChatWarningMessage(error));
            return;
        }

        if (viewModel.IsInGameRoom)
        {
            sourcePanelSubject.OnNext(SourcePanel.Primary);
            return;
        }
        
        if (hostedCncnetGame.Passworded && string.IsNullOrEmpty(password))
        {
            promptForPasswordSubject.OnNext(viewModel.SelectedGame);
            return;
        }

        // if (hg.GameVersion != ProgramConstants.GAME_VERSION)
        // TODO Show warning
        
        if (!viewModel.SelectedGame.IsLoadedGame)
        {
            password = Utilities.CalculateSHA1ForString(hostedCncnetGame.ChannelName + hostedCncnetGame.RoomName)[..10];
        }
        else
        {
            var spawnSgIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, ProgramConstants.SAVED_GAME_SPAWN_INI));
            password = Utilities.CalculateSHA1ForString(
                spawnSgIni.GetStringValue("Settings", "GameID", string.Empty))[..10];
        }
    }
    
    private async ValueTask JoinGameAsync(HostedCnCNetGame hg, string password)
    {
        messageSubject.OnNext(new ChatMessage(string.Format("Attempting to join game {0} ...".L10N("UI:Main:AttemptJoin"), hg.RoomName)));
        // isJoiningGame = true;
        // gameOfLastJoinAttempt = hg;

        Channel gameChannel = connectionManager.CreateChannel(hg.RoomName, hg.ChannelName, false, true, password);
        connectionManager.AddChannel(gameChannel);

        if (hg.IsLoadedGame)
        {
            // gameLoadingLobby.SetUp(false, hg.TunnelServer, gameChannel, hg.HostName);
            // gameChannel.UserAdded += gameLoadingChannel_UserAddedFunc;
            // gameChannel.InvalidPasswordEntered += gameChannel_InvalidPasswordEntered_LoadedGameFunc;
        }
        else
        {
            // await gameLobby.SetUpAsync(gameChannel, false, hg.MaxPlayers, hg.TunnelServer, hg.HostName, hg.Passworded).ConfigureAwait(false);
            gameChannel.UserAdded += (sender, e) => GameChannel_UserAddedAsync(sender, e).HandleTask();
            // gameChannel.InvalidPasswordEntered += gameChannel_InvalidPasswordEntered_NewGameFunc;
            // gameChannel.InviteOnlyErrorOnJoin += gameChannel_InviteOnlyErrorOnJoinFunc;
            // gameChannel.ChannelFull += gameChannel_ChannelFullFunc;
            // gameChannel.TargetChangeTooFast += gameChannel_TargetChangeTooFastFunc;
        }

        await connectionManager.SendCustomMessageAsync(new QueuedMessage(IRCCommands.JOIN + " " + hg.ChannelName + " " + password,
            QueuedMessageType.INSTANT_MESSAGE, 0)).ConfigureAwait(false);
    }

    private async ValueTask GameChannel_UserAddedAsync(object sender, ChannelUserEventArgs e)
    {
        Channel gameChannel = (Channel)sender;
        
        if (e.User.IRCUser.Name == ProgramConstants.PLAYERNAME)
        {
            // ClearGameChannelEvents(gameChannel);
            // await gameLobby.OnJoinedAsync().ConfigureAwait(false);
            // SetLogOutButtonText();
            viewModel.IsInGameRoom = true;
            RefreshViewModel();
        }
    }

    private void PostMessage(ChatMessage message) => messageSubject.OnNext(message);

    private GenericHostedGame FindGameForIrcUser(IRCUser ircUser)
        => hostedGames?.Find(g => g.HostName == ircUser.Name);

    private void ConnectionManager_Disconnected(object sender, EventArgs e)
    {
        followedGames.Clear();
        gameCheckCancellation?.Cancel();
        gameCheckCancellation?.Dispose();
    }

    private async ValueTask ConnectionManager_WelcomeMessageReceivedAsync()
    {
        Channel cncnetChannel = connectionManager.FindChannel("#cncnet");
        await cncnetChannel.JoinAsync().ConfigureAwait(false);

        string localGameChatChannelName = gameCollection.GetGameChatChannelNameFromIdentifier(viewModel.LocalGame.InternalName);
        await connectionManager.FindChannel(localGameChatChannelName).JoinAsync().ConfigureAwait(false);

        string localGameBroadcastChannel = gameCollection.GetGameBroadcastingChannelNameFromIdentifier(viewModel.LocalGame.InternalName);
        await connectionManager.FindChannel(localGameBroadcastChannel).JoinAsync().ConfigureAwait(false);

        foreach (CnCNetGame game in gameCollection.GameList)
        {
            if (!game.Supported ||
                game.Equals(viewModel.LocalGame) ||
                !UserINISettings.Instance.IsGameFollowed(game.InternalName.ToUpper(CultureInfo.InvariantCulture)))
                continue;

            await connectionManager.FindChannel(game.GameBroadcastChannel).JoinAsync().ConfigureAwait(false);
            followedGames.Add(game.InternalName);
        }

        gameCheckCancellation = new CancellationTokenSource();
        CnCNetGameCheck.RunServiceAsync(gameCheckCancellation.Token).HandleTask();

        viewModel.IsNewGameBtnEnabled = true;
        viewModel.IsChatTbEnabled = true;
        viewModel.IsCurrentChannelDdEnabled = true;
        RefreshViewModel();
    }

    public async ValueTask SendChatMessageAsync(string message, IRCColor color)
    {
        await currentChatChannel.SendChatMessageAsync(message, color).ConfigureAwait(false);
    }

    public void SetIsInGameRoom(bool isInGameRoom)
    {
        viewModel.IsInGameRoom = isInGameRoom;
        RefreshViewModel();
    }

    public void DenyUpdate()
    {
        viewModel.IsUpdatedDenied = true;
        RefreshViewModel();
    }

    public void AcceptUpdate()
    {
    }

    public void SetIsJoiningGame(bool isJoiningGame)
    {
        viewModel.IsJoiningGame = isJoiningGame;
        RefreshViewModel();
    }

    public void ApplyGameFilter(CnCNetLobbyGameFilterViewModel filterViewModel)
    {
        viewModel.GameFilterViewModel = filterViewModel;
        RefreshViewModel();
    }

    private void RefreshViewModel() => viewModelSubject.OnNext(viewModel);

    /// <summary>
    /// Returns an error message if game is not join-able, otherwise null.
    /// </summary>
    /// <param name="hostedGame"></param>
    /// <returns></returns>
    public string GetJoinGameError(GenericHostedGame hostedGame)
    {
        if (!hostedGame.Game.Equals(localGameId))
            return string.Format("The selected game is for {0}!".L10N("UI:Main:GameIsOfPurpose"), gameCollection.GetGameNameFromInternalName(hostedGame.Game.InternalName));

        if (hostedGame.Locked)
            return "The selected game is locked!".L10N("UI:Main:GameLocked");

        if (hostedGame.IsLoadedGame && !hostedGame.Players.Contains(ProgramConstants.PLAYERNAME))
            return "You do not exist in the saved game!".L10N("UI:Main:NotInSavedGame");

        return GetJoinGameErrorBase();
    }

    /// <summary>
    /// Checks if the user can join a game.
    /// Returns null if the user can, otherwise returns an error message
    /// that tells the reason why the user cannot join the game.
    /// </summary>
    /// <param name="gameIndex">The index of the game in the game list box.</param>
    public string GetJoinGameErrorByIndex(int gameIndex)
    {
        if (gameIndex < 0 || gameIndex >= hostedGames.Count)
            return "Invalid game index".L10N("UI:Main:InvalidGameIndex");

        return GetJoinGameErrorBase();
    }


    private string GetJoinGameErrorBase()
    {
        if (viewModel.IsJoiningGame)
            return "Cannot join game - joining game in progress. If you believe this is an error, please log out and back in.".L10N("UI:Main:JoinGameErrorInProgress");

        if (ProgramConstants.IsInGame)
            return "Cannot join game while the main game executable is running.".L10N("UI:Main:JoinGameErrorGameRunning");

        return null;
    }


    /// <summary>
    /// Generates and returns a random, unused cannel name.
    /// </summary>
    /// <returns>A random channel name based on the currently played game.</returns>
    public string RandomizeChannelName()
    {
        string localGameId = viewModel.LocalGame.InternalName;
        while (true)
        {
            string channelName = string.Format("{0}-game{1}".L10N("UI:Main:RamdomChannelName"), gameCollection.GetGameChatChannelNameFromIdentifier(localGameId), new Random().Next(1000000, 9999999));
            int index = hostedGames.FindIndex(c => ((HostedCnCNetGame)c).ChannelName == channelName);
            if (index == -1)
                return channelName;
        }
    }

    private async ValueTask Instance_SettingsSavedAsync()
    {
        if (!connectionManager.IsConnected)
            return;

        foreach (CnCNetGame game in gameCollection.GameList)
        {
            if (!game.Supported)
                continue;

            if (game.InternalName == viewModel.LocalGame.InternalName)
                continue;

            if (followedGames.Contains(game.InternalName) &&
                !UserINISettings.Instance.IsGameFollowed(game.InternalName.ToUpper()))
            {
                await connectionManager.FindChannel(game.GameBroadcastChannel).LeaveAsync().ConfigureAwait(false);
                followedGames.Remove(game.InternalName);
            }
            else if (!followedGames.Contains(game.InternalName) &&
                     UserINISettings.Instance.IsGameFollowed(game.InternalName.ToUpper()))
            {
                await connectionManager.FindChannel(game.GameBroadcastChannel).JoinAsync().ConfigureAwait(false);
                followedGames.Add(game.InternalName);
            }
        }
    }

    private void InitializeGameList()
    {
        foreach (CnCNetGame game in gameCollection.GameList)
        {
            if (!game.Supported || string.IsNullOrEmpty(game.ChatChannel))
                continue;


            viewModel.Games.Add(game);

            Channel chatChannel = connectionManager.FindChannel(game.ChatChannel);

            if (chatChannel == null)
            {
                chatChannel = connectionManager.CreateChannel(game.UIName, game.ChatChannel,
                    true, true, "ra1-derp");
                connectionManager.AddChannel(chatChannel);
            }

            viewModel.GameChatChannels.Add(game, chatChannel);

            if (!string.IsNullOrEmpty(game.GameBroadcastChannel))
            {
                var gameBroadcastChannel = connectionManager.FindChannel(game.GameBroadcastChannel);

                if (gameBroadcastChannel == null)
                {
                    gameBroadcastChannel = connectionManager.CreateChannel(
                        string.Format("{0} Broadcast Channel".L10N("UI:Main:BroadcastChannel"), game.UIName),
                        game.GameBroadcastChannel, true, false, null);
                    connectionManager.AddChannel(gameBroadcastChannel);
                }

                gameBroadcastChannel.CTCPReceived += GameBroadcastChannel_CTCPReceived;
                gameBroadcastChannel.UserLeft += GameBroadcastChannel_UserLeftOrQuit;
                gameBroadcastChannel.UserQuitIRC += GameBroadcastChannel_UserLeftOrQuit;
                gameBroadcastChannel.UserKicked += GameBroadcastChannel_UserLeftOrQuit;
            }

            if (game.InternalName.ToUpper() == viewModel.LocalGame.InternalName.ToUpper())
                SetCurrentChannel(chatChannel);
        }

        if (currentChatChannel == null)
            SetCurrentChannel(viewModel.GameChatChannels.Select(gcc => gcc.Value).Last());

        // if (connectionManager.MainChannel == null)
        // {
        //     // Set CnCNet channel as main channel if no channel found
        //     ddCurrentChannel.SelectedIndex = ddCurrentChannel.Items.Count - 1;
        // }
    }

    private bool IsClientUpdateMessage(string message)
        => message.StartsWith("UPDATE ") &&
           message.Length > 7 &&
           message.Substring(7) != ProgramConstants.GAME_VERSION;

    private bool IsGameMessage(string message) => message.StartsWith("GAME ");

    public void GameBroadcastChannel_CTCPReceived(object sender, ChannelCTCPEventArgs channelCtcpEventArgs)
    {
        var channel = (Channel)sender;
        string userName = channelCtcpEventArgs.UserName;
        string message = channelCtcpEventArgs.Message;
        ChannelUser channelUser = channel.Users.Find(userName);
        if (IsClientUpdateMessage(message) && channelUser != null)
        {
            viewModel.IsUpdateAvailable = true;
            // RefreshViewModel();
            return;
        }

        if (IsGameMessage(message) && channelUser != null)
            RefreshViewModelForGame(channel, userName, message);
    }

    private GenericHostedGame FindGameForUserName(string userName)
    {
        int index = FindGameIndexForUserName(userName);
        return index >= 0 ? hostedGames[index] : null;
    }

    private int FindGameIndexForUserName(string userName)
        => hostedGames.FindIndex(g => g.HostName.ToUpper() == userName.ToUpper());

    private void HandleClosedGame(string userName)
    {
        GenericHostedGame closedGame = FindGameForUserName(userName);
        if (closedGame == null)
            return;

        hostedGames.Remove(closedGame);
        // RefreshViewModel();
    }

    private void RefreshViewModelForGame(Channel channel, string userName, string message)
    {
        string msg = message[5..]; // Cut out GAME part
        string[] splitMessage = msg.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        if (splitMessage.Length != 11)
        {
            Logger.Log("Ignoring CTCP game message because of an invalid amount of parameters.");
            return;
        }

        try
        {
            if (Conversions.BooleanFromString(splitMessage[5].Substring(2, 1), true))
                HandleClosedGame(userName);

            string revision = splitMessage[0];

            if (revision != ProgramConstants.CNCNET_PROTOCOL_REVISION)
                return;

            string gameVersion = splitMessage[1];
            int maxPlayers = Conversions.IntFromString(splitMessage[2], 0);
            string gameRoomChannelName = splitMessage[3];
            string gameRoomDisplayName = splitMessage[4];
            bool locked = Conversions.BooleanFromString(splitMessage[5][..1], true);
            bool isCustomPassword = Conversions.BooleanFromString(splitMessage[5].Substring(1, 1), false);
            bool isLoadedGame = Conversions.BooleanFromString(splitMessage[5].Substring(3, 1), false);
            bool isLadder = Conversions.BooleanFromString(splitMessage[5].Substring(4, 1), false);
            string[] players = splitMessage[6].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string mapName = splitMessage[7];
            string gameMode = splitMessage[8];
            string tunnelHash = splitMessage[9];
            string loadedGameId = splitMessage[10];
            tunnelHash = tunnelHash.Substring(0, tunnelHash.IndexOf(":"));

            CnCNetGame cncnetGame = gameCollection.GameList.Find(g => g.GameBroadcastChannel == channel.ChannelName);

            if (cncnetGame == null)
                return;

            CnCNetTunnel tunnel = null;

            if (!ProgramConstants.CNCNET_DYNAMIC_TUNNELS.Equals(tunnelHash, StringComparison.OrdinalIgnoreCase))
            {
                tunnel = tunnelHandler.Tunnels.Find(t => t.Hash.Equals(tunnelHash, StringComparison.OrdinalIgnoreCase));

                if (tunnel == null)
                    tunnel = tunnelHandler.Tunnels.Find(t => $"{t.IPAddress}".Equals(tunnelHash, StringComparison.OrdinalIgnoreCase));

                if (tunnel == null)
                    return;
            }

            var game = new HostedCnCNetGame(gameRoomChannelName, revision, gameVersion, maxPlayers,
                gameRoomDisplayName, isCustomPassword, true, players, userName, mapName, gameMode);

            game.IsLoadedGame = isLoadedGame;
            game.MatchID = loadedGameId;
            game.LastRefreshTime = DateTime.Now;
            game.IsLadder = isLadder;
            game.Game = cncnetGame;
            game.Locked = locked || (game.IsLoadedGame && !game.Players.Contains(ProgramConstants.PLAYERNAME));
            game.Incompatible = cncnetGame.Equals(localGameId) && game.GameVersion != ProgramConstants.GAME_VERSION;
            game.TunnelServer = tunnel;

            // Seek for the game in the internal game list based on the name of its host;
            // if found, then refresh that game's information, otherwise add as new game
            int gameIndex = hostedGames.FindIndex(hg => hg.HostName == userName);

            if (gameIndex > -1)
            {
                hostedGames[gameIndex] = game;
            }
            else
            {
                if (UserINISettings.Instance.PlaySoundOnGameHosted &&
                    cncnetGame.InternalName == viewModel.LocalGame.InternalName &&
                    !ProgramConstants.IsInGame && !game.Locked)
                {
                    SoundPlayer.Play(sndGameCreated);
                }

                hostedGames.Add(game);
            }

            RefreshHostedGames();
        }
        catch (Exception ex)
        {
            ProgramConstants.LogException(ex, "Game parsing error");
        }
    }

    private void RefreshHostedGames() => hostedGamesSubject.OnNext(GetSortedAndFilteredGames().ToList());

    private IEnumerable<GenericHostedGame> GetSortedAndFilteredGames() => GetSortedGames(GetFilteredGames(hostedGames));

    private IEnumerable<GenericHostedGame> GetSortedGames(IEnumerable<GenericHostedGame> games)
    {
        IOrderedEnumerable<GenericHostedGame> sortedGames =
            games
                .OrderBy(hg => hg.Locked)
                .ThenBy(hg => string.Equals(hg.Game.InternalName, viewModel.LocalGame.InternalName, StringComparison.CurrentCultureIgnoreCase))
                .ThenBy(hg => hg.GameVersion != ProgramConstants.GAME_VERSION)
                .ThenBy(hg => hg.Passworded);

        sortedGames = (SortDirection)viewModel.GameSortState switch
        {
            SortDirection.Asc => sortedGames.ThenBy(hg => hg.RoomName),
            SortDirection.Desc => sortedGames.ThenByDescending(hg => hg.RoomName),
            _ => sortedGames
        };

        return sortedGames;
    }

    private IEnumerable<GenericHostedGame> GetFilteredGames(IEnumerable<GenericHostedGame> games)
        => games.Where(HostedGameMatches).ToList();

    private bool HostedGameMatches(GenericHostedGame hg)
    {
        CnCNetLobbyGameFilterViewModel filterViewModel = viewModel.GameFilterViewModel;
        // friends list takes priority over other filters below
        if (filterViewModel.IsShowFriendGamesOnly)
            return hg.Players.Any(p => cncnetUserData.IsFriend(p));

        if (filterViewModel.IsHideLockedGames && hg.Locked)
            return false;

        if (filterViewModel.IsHideIncompatibleGames && hg.Incompatible)
            return false;

        if (filterViewModel.IsHidePasswordedGames && hg.Passworded)
            return false;

        if (hg.MaxPlayers > UserINISettings.Instance.MaxPlayerCount.Value)
            return false;

        return
            !filterViewModel.IsSearchApplied ||
            hg.RoomName.ToUpper().Contains(filterViewModel.Search.ToUpper()) ||
            hg.GameMode.ToUpper().Equals(filterViewModel.Search.ToUpper()) ||
            hg.Map.ToUpper().Contains(filterViewModel.Search.ToUpper()) ||
            hg.Players.Any(pl => pl.ToUpper().Equals(filterViewModel.Search.ToUpper()));
    }

    /// <summary>
    /// Removes a game from the list when the host quits CnCNet or
    /// leaves the game broadcast channel.
    /// </summary>
    private void GameBroadcastChannel_UserLeftOrQuit(object sender, UserNameEventArgs e)
    {
        int gameIndex = hostedGames.FindIndex(hg => hg.HostName == e.UserName);

        if (gameIndex > -1)
        {
            hostedGames.RemoveAt(gameIndex);

            // dismiss any outstanding invitations that are no longer valid
            // DismissInvalidInvitations();
        }
    }

    private async ValueTask GameChannel_TargetChangeTooFastAsync(object sender, MessageEventArgs e)
    {
        // AddMainChannelMessage(new ChatMessage(Color.White, e.Message))
        // await ClearGameJoinAttemptAsync((Channel)sender).ConfigureAwait(false);
    }

    private HostedCnCNetGame FindGameByChannelName(string channelName)
    {
        // var game = viewModel.HostedGames.Find(hg => ((HostedCnCNetGame)hg).ChannelName == channelName);
        // if (game == null)
        //     return null;
        //
        // return (HostedCnCNetGame)game;
        return null;
    }

    private async ValueTask GameChannel_InvalidPasswordEntered_NewGameAsync(object sender)
    {
        // connectionManager.MainChannel.AddMessage(new ChatMessage(Color.White, "Incorrect password!".L10N("UI:Main:PasswordWrong")));
        // await ClearGameJoinAttemptAsync((Channel)sender).ConfigureAwait(false);
    }

    public IObservable<List<GenericHostedGame>> GetHostedGames() => hostedGamesSubject.AsObservable();

    public IObservable<List<ChannelUser>> GetChannelUsers() => channelUsersSubject.AsObservable();

    public IObservable<Channel> GetCurrentChatChannel() => currentChatChannelSubject.AsObservable();

    public IObservable<ChatMessage> GetMessages() => messageSubject.AsObservable();
    public IObservable<CnCNetLobbyViewModel> GetViewModel() => viewModelSubject.AsObservable();

    public IObservable<GenericHostedGame> GetPromptForPassword() => promptForPasswordSubject.AsObservable();

    public IObservable<SourcePanel> GetSourcePanel() => sourcePanelSubject.AsObservable();

    public IObservable<bool> GetPromptLogin() => promptLoginSubject.AsObservable();

    public IObservable<string> GetLogoutBtnText() => logoutBtnTextSubject.AsObservable();

    private async ValueTask OnGameLocked(object sender)
    {
        messageSubject.OnNext(new ChatMessage("The selected game is locked!".L10N("UI:Main:GameLocked")));
        var channel = (Channel)sender;
        //
        // var game = FindGameByChannelName(channel.ChannelName);
        // if (game != null)
        // {
        //     game.Locked = true;
        //     // SortAndRefreshHostedGames();
        // }
        //
        // await ClearGameJoinAttemptAsync((Channel)sender).ConfigureAwait(false);
    }

    public void Connect() => connectionManager.Connect();

    public void AddMainChannelMessage(ChatMessage chatMessage) => connectionManager.MainChannel.AddMessage(chatMessage);

    public void SetSelectedGame(GenericHostedGame selectedGame)
    {
        if (viewModel.SelectedGame == selectedGame)
            return;

        viewModel.SelectedGame = selectedGame;
        viewModel.IsJoinGameBtnEnabled = selectedGame is { Locked: false };
        RefreshViewModel();
    }

    public void PromptLogin()
    {
        if (!connectionManager.IsConnected && !connectionManager.IsAttemptingConnection)
        {
            promptLoginSubject.OnNext(true);
        }

        // SetLogOutButtonText();
    }

    public void LoginConnect()
    {
        connectionManager.Connect();
        promptLoginSubject.OnNext(false);
        SetLogOutButtonText();
    }

    public void LoginCancel()
    {
        promptLoginSubject.OnNext(false);
        sourcePanelSubject.OnNext(SourcePanel.Primary);
    }

    private void SetLogOutButtonText()
    {
        if (viewModel.IsInGameRoom)
        {
            logoutBtnTextSubject.OnNext("Game Lobby".L10N("UI:Main:GameLobby"));
            return;
        }

        if (UserINISettings.Instance.PersistentMode)
        {
            logoutBtnTextSubject.OnNext("Main Menu".L10N("UI:Main:MainMenu"));
            return;
        }

        logoutBtnTextSubject.OnNext("Log Out".L10N("UI:Main:LogOut"));
    }
}