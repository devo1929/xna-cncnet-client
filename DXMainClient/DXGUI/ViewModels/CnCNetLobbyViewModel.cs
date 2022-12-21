using System.Collections.Generic;
using ClientCore.CnCNet5;
using DTAClient.Domain.Multiplayer;
using DTAClient.Online;
using Microsoft.Xna.Framework;

namespace DTAClient.DXGUI.ViewModels;

public class CnCNetLobbyViewModel
{
    // public List<GenericHostedGame> HostedGames { get; set; }

    public List<CnCNetGame> Games { get; }
    
    public List<IRCColor> IrcColors { get; set; }
    
    public Dictionary<CnCNetGame, Channel> GameChatChannels { get;  }

    public CnCNetGame LocalGame { get; set; }
    
    public GenericHostedGame SelectedGame { get; set; }
    
    public int LocalGameIndex => Games?.FindIndex(g => g.InternalName == LocalGame?.InternalName) ?? -1;

    public bool IsInGameRoom { get; set; }

    public bool IsUpdateAvailable { get; set; }

    public bool IsUpdatedDenied { get; set; }

    public bool IsJoiningGame { get; set; }
    
    public int GameSortState { get; set; }
    
    public bool IsNewGameBtnEnabled { get; set; }
    
    public bool IsJoinGameBtnEnabled { get; set; }
    
    public bool IsCurrentChannelDdEnabled { get; set; }
    
    public bool IsColorDdEnabled { get; set; }
    
    public bool IsChatTbEnabled { get; set; }
    
    public CnCNetLobbyGameFilterViewModel GameFilterViewModel { get; set; }

    public CnCNetLobbyViewModel()
    {
        // HostedGames = new List<GenericHostedGame>();
        Games = new List<CnCNetGame>();
        GameChatChannels = new Dictionary<CnCNetGame, Channel>();
        GameFilterViewModel = new CnCNetLobbyGameFilterViewModel();
    }

    public CnCNetGame FindGame(string gameName) => Games.Find(g => g.InternalName.ToUpper() == gameName.ToUpper());
}