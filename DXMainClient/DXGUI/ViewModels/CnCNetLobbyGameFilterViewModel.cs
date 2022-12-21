using ClientCore;
using Localization;

namespace DTAClient.DXGUI.ViewModels;

public class CnCNetLobbyGameFilterViewModel
{
    private static readonly string SuggestionText = "Filter by name, map, game mode, player...".L10N("UI:Main:FilterByBlahBlah");

    public string Search { get; set; } = SuggestionText;

    public bool IsShowFriendGamesOnly { get; set; } = UserINISettings.DEFAULT_SHOW_FRIENDS_ONLY_GAMES;

    public bool IsHideLockedGames { get; set; } = UserINISettings.DEFAULT_HIDE_LOCKED_GAMES;

    public bool IsHidePasswordedGames { get; set; } = UserINISettings.DEFAULT_HIDE_PASSWORDED_GAMES;

    public bool IsHideIncompatibleGames { get; set; } = UserINISettings.DEFAULT_HIDE_INCOMPATIBLE_GAMES;

    public int MaxPlayerCount { get; set; }

    public bool IsSearchApplied => !string.IsNullOrEmpty(Search) && Search != SuggestionText;

    public bool IsApplied =>
        IsSearchApplied ||
        IsShowFriendGamesOnly != UserINISettings.DEFAULT_SHOW_FRIENDS_ONLY_GAMES ||
        IsHideIncompatibleGames != UserINISettings.DEFAULT_HIDE_INCOMPATIBLE_GAMES ||
        IsHideLockedGames != UserINISettings.DEFAULT_HIDE_LOCKED_GAMES ||
        IsHidePasswordedGames != UserINISettings.DEFAULT_HIDE_PASSWORDED_GAMES;


    public CnCNetLobbyGameFilterViewModel()
    {
        Search = string.Empty;
        MaxPlayerCount = 8;
    }
}