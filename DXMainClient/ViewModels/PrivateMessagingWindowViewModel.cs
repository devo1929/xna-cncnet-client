using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using ClientCore;
using ClientCore.CnCNet5;
using ClientCore.Enums;
using ClientCore.Extensions;
using ClientGUI;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.Online;
using DTAClient.Online.EventArguments;
using Localization;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SixLabors.ImageSharp;
using Color = Microsoft.Xna.Framework.Color;

namespace DTAClient.ViewModels;

public class PrivateMessagingWindowViewModel : ReactiveObject
{
    public const int MESSAGES_TAB_INDEX = 0;
    public const int FRIEND_LIST_TAB_INDEX = 1;
    public const int ALL_PLAYERS_TAB_INDEX = 2;
    public const int RECENT_PLAYERS_TAB_INDEX = 3;
    
    private readonly CnCNetManager connectionManager;
    private readonly CnCNetUserData cncnetUserData;
    private readonly GameCollection gameCollection;
    private readonly Texture2D unknownGameIcon;
    
    /// <summary>
    /// The current visibilty of the private message window
    /// </summary>
    [Reactive]
    public bool Visible { get; set; }

    /// <summary>
    /// The currently selected tab index in the private message window.
    /// </summary>
    [Reactive]
    public int SelectedTabIndex { get; set; }

    /// <summary>
    /// A reactive list of users for the currently selected tab.
    /// This list is updated when a private message window tab is selelected.
    /// </summary>
    [Reactive]
    private List<PrivateMessageUser> users { get; set; } = new();

    /// <summary>
    /// A reactive list of XNAListBoxItems for users.
    /// This list is updated every time the <see cref="users"/> list is updated.
    /// </summary>
    [Reactive]
    public List<XNAListBoxItem> UserListItems { get; set; } = new();

    /// <summary>
    /// This is a cache of all messages received in the current client session
    /// </summary>
    public List<PrivateMessage> messages { get; set; } = new();

    /// <summary>
    /// This is a reactive/filtered list of messages for the currently selected user
    /// </summary>
    [Reactive]
    public List<PrivateMessage> Messages { get; set; } = new();

    /// <summary>
    /// A reactive list of recent players
    /// </summary>
    [Reactive]
    public List<RecentPlayer> RecentPlayers { get; set; } = new();

    /// <summary>
    /// A reactive index of the currently selected user. This can be updated
    /// when the user changes tabs
    /// </summary>
    [Reactive]
    public int SelectedUserIndex { get; set; }

    /// <summary>
    /// A reactive instance of the currently selected user. This is updated
    /// when the <see cref="SelectedUserIndex"/> is updated.
    /// </summary>
    [Reactive]
    public PrivateMessageUser SelectedUser { get; set; }

    /// <summary>
    /// A reactive property that controls the enablement of the message input box
    /// </summary>
    [Reactive]
    public bool MessageInputEnabled { get; set; }

    /// <summary>
    /// A reactive property that can be used to reset the message input box to empty
    /// </summary>
    [Reactive]
    public string MessageInputText { get; set; }

    /// <summary>
    /// A reactive property to trigger a popup for a received message
    /// </summary>
    [Reactive]
    public NotificationPopupMessage NotificationPopupMessage { get; set; }

    /// <summary>
    /// A statically defined list of tabs for the private message window
    /// </summary>
    public static readonly List<XNAClientTabControlTab> Tabs = InitTabs();

    /// <summary>
    /// The color of the message text for the current user's messages
    /// </summary>
    private readonly Color personalMessageColor;

    /// <summary>
    /// The color of the message text for the other user's messages
    /// </summary>
    private readonly Color otherUserMessageColor;

    /// <summary>
    /// The sound that is played when a notification popup is received
    /// </summary>
    private readonly EnhancedSoundEffect sndNotification;

    /// <summary>
    /// The sound that is played when a message is sent or received while viewing the conversation
    /// </summary>
    private readonly EnhancedSoundEffect sndMessage;
    
    /// <summary>
    /// This is the LAST message that was received while in game. This should trigger a popup notification
    /// upon exiting the game
    /// </summary>
    private PrivateMessage pmReceivedDuringGame;

    public PrivateMessagingWindowViewModel(
        CnCNetManager connectionManager,
        CnCNetUserData cncnetUserData,
        GameCollection gameCollection)
    {
        this.connectionManager = connectionManager;
        this.connectionManager.UserAdded += ConnectionManager_UserAdded;
        this.connectionManager.MultipleUsersAdded += ConnectionManager_MultipleUsersAdded;
        this.connectionManager.UserRemoved += ConnectionManager_UserRemoved;
        this.connectionManager.UserGameIndexUpdated += ConnectionManager_UserGameIndexUpdated;
        this.connectionManager.PrivateMessageReceived += ConnectionManager_PrivateMessageReceived;
        GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;

        this.cncnetUserData = cncnetUserData;
        this.gameCollection = gameCollection;

        var assembly = Assembly.GetAssembly(typeof(GameCollection));
        using Stream unknownIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.unknownicon.png");
        using Stream cncnetIconStream = assembly.GetManifestResourceStream("ClientCore.Resources.cncneticon.png");
        unknownGameIcon = AssetLoader.TextureFromImage(Image.Load(unknownIconStream));
        personalMessageColor = AssetLoader.GetColorFromString(ClientConfiguration.Instance.SentPMColor);
        otherUserMessageColor = AssetLoader.GetColorFromString(ClientConfiguration.Instance.ReceivedPMColor);
        sndNotification = new EnhancedSoundEffect("pm.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundPrivateMessageCooldown);
        sndMessage = new EnhancedSoundEffect("message.wav", 0.0, 0.0, ClientConfiguration.Instance.SoundMessageCooldown);
        sndMessage.Enabled = UserINISettings.Instance.MessageSound;

        SelectedTabIndex = -1;
        SelectedUserIndex = -1;

        // When the selected tab is updated, update the list of users for it
        this.WhenAnyValue(vm => vm.SelectedTabIndex)
            .Subscribe(_ => RefreshUserList());

        // When the user list is updated, update the user list box item list
        this.WhenAnyValue(vm => vm.users)
            .Subscribe(u =>
                UserListItems = u
                    .Select(CreateUserListBoxItem)
                    .ToList()
            );

        // when selected user index is updated, update selected user 
        this.WhenAnyValue(vm => vm.SelectedUserIndex)
            .Select(GetUserForIndex)
            .Do(UpdateUserState)
            .BindTo(this, vm => vm.SelectedUser);

        // when selected user is updated, update messages 
        this.WhenAnyValue(vm => vm.SelectedUser)
            .Select(GetMessagesForUser)
            .BindTo(this, vm => vm.Messages);

        // when selected user is not null and is online, enable input field, else disable
        this.WhenAnyValue(vm => vm.SelectedUser)
            .Select(user => user is { IsOnline: true })
            .BindTo(this, vm => vm.MessageInputEnabled);

        // when recent players tab is selected, update recent players 
        this.WhenAnyValue(vm => vm.SelectedTabIndex)
            .Where(index => index == RECENT_PLAYERS_TAB_INDEX)
            .Select(_ => GetRecentPlayers())
            .BindTo(this, v => v.RecentPlayers);
    }

    /// <summary>
    /// Updates the "state" of the private messag user
    /// </summary>
    /// <param name="privateMessageUser"></param>
    private void UpdateUserState(PrivateMessageUser privateMessageUser)
    {
        if (privateMessageUser == null)
            return;

        privateMessageUser.IsOnline = IsOnline(privateMessageUser.Name);
        privateMessageUser.IsFriend = IsFriend(privateMessageUser.Name);
    }

    /// <summary>
    /// Called when the game process is exited
    /// </summary>
    private void SharedUILogic_GameProcessExited()
    {
        RefreshUserList();
        RefreshMessageList();
        if (pmReceivedDuringGame == null)
            return;

        // We received a message while in game. Show it now.
        ShowNotification(pmReceivedDuringGame);
        pmReceivedDuringGame = null;
    }

    /// <summary>
    /// Convenience function to get the user for the specified user index
    /// </summary>
    /// <param name="userIndex"></param>
    /// <returns></returns>
    private PrivateMessageUser GetUserForIndex(int userIndex)
    {
        if (userIndex < 0 || userIndex > users.Count)
            return null;

        return users[userIndex];
    }

    /// <summary>
    /// Convert a private message user to a <see cref="XNAListBoxItem"/>
    /// </summary>
    /// <param name="privateMessageUser"></param>
    /// <returns></returns>
    private static XNAListBoxItem CreateUserListBoxItem(PrivateMessageUser privateMessageUser)
    {
        return new XNAListBoxItem
        {
            Text = privateMessageUser.Name,
            Tag = privateMessageUser,
            TextColor = privateMessageUser.IsOnline ? UISettings.ActiveSettings.AltColor : UISettings.ActiveSettings.DisabledItemColor,
            Texture = privateMessageUser.GameIcon
        };
    }

    /// <summary>
    /// Send a message to the currently selected user
    /// </summary>
    /// <param name="message">The message text to send</param>
    public void SendMessageToUser(string message)
    {
        if (SelectedUser == null || string.IsNullOrWhiteSpace(message))
            return;

        string userName = SelectedUser.Name;
        string cncMessage = $"{IRCCommands.PRIVMSG} {userName} :{message}";
        connectionManager.SendCustomMessageAsync(new QueuedMessage(cncMessage, QueuedMessageType.CHAT_MESSAGE, 0)).HandleTask();

        PrivateMessageUser privateMessageUser = CreatePrivateMessageUser(userName, IsFriend(userName));

        var sentMessage = new ChatMessage(ProgramConstants.PLAYERNAME, personalMessageColor, DateTime.Now, message);

        messages.Add(new PrivateMessage(sentMessage, privateMessageUser));

        sndMessage?.Play();

        MessageInputText = string.Empty;
        RefreshUserList();
        RefreshMessageList();
    }

    /// <summary>
    /// Convenience function to get the user index for the specified user name
    /// </summary>
    /// <param name="userName"></param>
    /// <returns></returns>
    private int GetUserIndexForName(string userName) => users.IndexOf(users.FirstOrDefault(u => u.Name == userName));

    /// <summary>
    /// Get a list of <see cref="PrivateMessageUser"/> for the specified private message window tab index
    /// </summary>
    /// <param name="tabIndex">The currently selected tab index in the private message window</param>
    /// <returns>A list of <see cref="PrivateMessageUser"/></returns>
    private List<PrivateMessageUser> GetUsersForTabIndex(int tabIndex)
    {
        IEnumerable<PrivateMessageUser> u = tabIndex switch
        {
            MESSAGES_TAB_INDEX => GetCachedUsers(),
            FRIEND_LIST_TAB_INDEX => GetFriendUsers(),
            ALL_PLAYERS_TAB_INDEX => GetAllUsers(),
            _ => new List<PrivateMessageUser>()
        };
        return u.ToList();
    }

    /// <summary>
    /// Get all messages that were sent to/received from the specified user
    /// </summary>
    /// <param name="privateMessageUser">The user to get messages for</param>
    /// <returns>A list of messages sent to/received from the specified user</returns>
    private List<PrivateMessage> GetMessagesForUser(PrivateMessageUser privateMessageUser)
    {
        if (privateMessageUser == null)
            return new List<PrivateMessage>();

        return messages
            .Where(m => m.User.Equals(privateMessageUser))
            .ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private IEnumerable<PrivateMessageUser> GetCachedUsers()
    {
        IEnumerable<PrivateMessageUser> cachedUsers = messages
            .Select(m => m.User)
            // this Distinct will use PrivateMessageUser.GetHashCode 
            .Distinct()
            .ToList();

        // refresh online and friend state
        foreach (PrivateMessageUser cachedUser in cachedUsers)
            UpdateUserState(cachedUser);

        // return sorted list
        return cachedUsers
            .OrderByDescending(u => u.IsOnline)
            .ThenByDescending(u => u.IsFriend)
            .ThenBy(u => u.Name);
    }

    private IEnumerable<PrivateMessageUser> GetFriendUsers()
    {
        return cncnetUserData.FriendList
            .Select(friendName => CreatePrivateMessageUser(friendName, true))
            .OrderByDescending(u => u.IsOnline)
            .ThenBy(u => u.Name);
    }

    private IEnumerable<PrivateMessageUser> GetAllUsers()
    {
        return connectionManager.UserList
            .Select(ircUser => new PrivateMessageUser(ircUser, true, IsFriend(ircUser.Name)))
            .OrderByDescending(u => u.IsOnline)
            .ThenByDescending(u => u.IsFriend)
            .ThenBy(u => u.Name);
    }


    private PrivateMessageUser CreatePrivateMessageUser(string userName, bool isFriend)
    {
        IRCUser ircUser = connectionManager.UserList.Find(u => u.Name == userName);
        if (ircUser != null)
            return new PrivateMessageUser(ircUser, true, isFriend);

        Logger.Log("Null IRCUser in private messaging?");
        return new PrivateMessageUser(new IRCUser("Unknown"), false, isFriend);
    }

    private List<RecentPlayer> GetRecentPlayers()
    {
        var recentPlayers = cncnetUserData.RecentList
            .OrderByDescending(rp => rp.GameTime)
            .ToList();

        foreach (RecentPlayer recentPlayer in recentPlayers)
        {
            IRCUser user = connectionManager.UserList.Find(u => u.Name == recentPlayer.PlayerName);
            recentPlayer.IsOnline = user != null;
            recentPlayer.User = user ?? new IRCUser(recentPlayer.PlayerName);
        }

        return recentPlayers;
    }

    private bool CanReceivePrivateMessageFromUser(IRCUser ircUser)
    {
        if (ircUser == null || IsIgnored(ircUser.Ident))
            return false;

        return UserINISettings.Instance.AllowPrivateMessagesFromState.Value switch
        {
            (int)AllowPrivateMessagesFromEnum.All => true,
            (int)AllowPrivateMessagesFromEnum.Friends => IsFriend(ircUser.Name),
            _ => false
        };
    }

    private void ConnectionManager_PrivateMessageReceived(object sender, CnCNetPrivateMessageEventArgs e)
    {
        IRCUser ircUser = connectionManager.UserList.Find(u => u.Name == e.Sender);
        if (!CanReceivePrivateMessageFromUser(ircUser))
            return;

        PrivateMessageUser privateMessageUser = CreatePrivateMessageUser(e.Sender, IsFriend(e.Sender));

        var message = new ChatMessage(e.Sender, otherUserMessageColor, DateTime.Now, e.Message);

        var privateMessage = new PrivateMessage(message, privateMessageUser);
        messages.Add(privateMessage);

        if (ProgramConstants.IsInGame)
        {
            pmReceivedDuringGame = privateMessage;
            return;
        }

        ShowNotification(privateMessage);
        RefreshUserList();
        RefreshMessageList();
    }

    private void ShowNotification(PrivateMessage privateMessage)
    {
        var notificationPopupMessage = new NotificationPopupMessage
        {
            GameIcon = GetUserTexture(privateMessage.User.IrcUser),
            IrcUser = privateMessage.User.IrcUser,
            Message = privateMessage.ChatMessage.Message
        };
        if (Visible && SelectedUser?.Name == privateMessage.User.Name)
        {
            // if currently viewing the conversation
            sndMessage?.Play();
        }
        else if (!UserINISettings.Instance.DisablePrivateMessagePopups)
        {
            // if PM popups are not disabled
            NotificationPopupMessage = notificationPopupMessage;
            sndNotification?.Play();
        }
    }

    public void ShowForUser(IRCUser user)
    {
        Show();
        SelectedUserIndex = GetUserIndexForName(user.Name);
    }

    public void Show()
    {
        if (SelectedTabIndex == -1)
            SelectedTabIndex = MESSAGES_TAB_INDEX;
        Visible = true;
    }

    public void Hide()
    {
        Visible = false;
    }

    private Texture2D GetUserTexture(IRCUser user)
    {
        if (user.GameID < 0 || user.GameID >= gameCollection.GameList.Count)
            return unknownGameIcon;

        return gameCollection.GameList[user.GameID].Texture;
    }

    private void AddCachedUserMessage(string userName, string message)
    {
        PrivateMessageUser privateMessageUser = CreatePrivateMessageUser(userName, IsFriend(userName));

        if (privateMessageUser == null)
            return;

        messages.Add(new PrivateMessage(new ChatMessage(message), privateMessageUser));
    }

    private void AddCachedOfflineMessage(string userName)
        => AddCachedUserMessage(userName, string.Format("{0} is now offline.".L10N("UI:Main:PlayerOffline"), userName));

    private void AddCachedOnlineMessage(string userName)
        => AddCachedUserMessage(userName, string.Format("{0} is now online.".L10N("UI:Main:PlayerOnline"), userName));

    private void RefreshUserList()
    {
        users = GetUsersForTabIndex(SelectedTabIndex);
        SelectedUserIndex = users.FindIndex(u => u.Equals(SelectedUser));
    }

    private void RefreshMessageList() => Messages = GetMessagesForUser(SelectedUser);

    private void ConnectionManager_UserAdded(object sender, UserEventArgs e)
    {
        AddCachedOnlineMessage(e.User.Name);
        RefreshUserList();
    }

    private void ConnectionManager_MultipleUsersAdded(object sender, EventArgs eventArgs)
    {
        foreach (IRCUser ircUser in connectionManager.UserList)
            AddCachedOnlineMessage(ircUser.Name);

        RefreshUserList();
    }

    private void ConnectionManager_UserRemoved(object sender, UserNameIndexEventArgs e)
    {
        AddCachedOfflineMessage(e.UserName);
        RefreshUserList();
    }

    private void ConnectionManager_UserGameIndexUpdated(object sender, UserEventArgs e)
        => RefreshUserList();

    private bool IsFriend(string userName)
        => cncnetUserData.FriendList.Contains(userName);

    private bool IsOnline(string userName)
        => connectionManager.UserList.Any(u => u.Name == userName);

    private bool IsIgnored(string ident)
        => cncnetUserData.IsIgnored(ident);

    private static List<XNAClientTabControlTab> InitTabs() =>
        new()
        {
            new XNAClientTabControlTab("Messages".L10N("UI:Main:MessagesTab"), UIDesignConstants.BUTTON_WIDTH_133),
            new XNAClientTabControlTab("Friend List".L10N("UI:Main:FriendListTab"), UIDesignConstants.BUTTON_WIDTH_133),
            new XNAClientTabControlTab("All Players".L10N("UI:Main:AllPlayersTab"), UIDesignConstants.BUTTON_WIDTH_133),
            new XNAClientTabControlTab("Recent Players".L10N("UI:Main:RecentPlayersTab"), UIDesignConstants.BUTTON_WIDTH_133),
        };
}