using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using ClientCore.CnCNet5;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.XNAUI;
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
    private readonly BehaviorSubject<GlobalContextMenuData> showContextMenuSubject;

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
        showContextMenuSubject = new BehaviorSubject<GlobalContextMenuData>(null);
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

    public void ShowContextMenu(GlobalContextMenuData menuData) => showContextMenuSubject.OnNext(menuData);

    public IObservable<GlobalContextMenuData> GetShowContextMenu() => showContextMenuSubject.AsObservable();
}