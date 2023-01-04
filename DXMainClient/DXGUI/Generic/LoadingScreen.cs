using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientCore;
using ClientCore.CnCNet5;
using ClientCore.Extensions;
using ClientGUI;
using ClientUpdater;
using DTAClient.Domain.Multiplayer;
using DTAClient.Online;
using DTAClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;

namespace DTAClient.DXGUI.Generic
{
    internal class LoadingScreen : XNAWindow
    {
        public LoadingScreen(
            CnCNetManager cncnetManager,
            WindowManager windowManager,
            IServiceProvider serviceProvider,
            MapLoaderService mapLoaderService
        ) : base(windowManager)
        {
            this.cncnetManager = cncnetManager;
            this.serviceProvider = serviceProvider;
            this.mapLoaderService = mapLoaderService;
        }

        private MapLoaderService mapLoaderService;
        private bool visibleSpriteCursor;
        private bool loadingFinished;
        private readonly CnCNetManager cncnetManager;
        private readonly IServiceProvider serviceProvider;

        public override void Initialize()
        {
            ClientRectangle = new Rectangle(0, 0, 800, 600);
            Name = "LoadingScreen";

            BackgroundTexture = AssetLoader.LoadTexture("loadingscreen.png");

            base.Initialize();

            CenterOnParent();

            if (Cursor.Visible)
            {
                Cursor.Visible = false;
                visibleSpriteCursor = true;
            }

            // Combine individual tasks to single observable - only resolve when all have completed (zip)
            Observable.Zip(
                InitUpdater(),
                InitMapLoader()
            ).Subscribe(_ => loadingFinished = true);
        }

        private static IObservable<Unit> InitUpdater() =>
            Observable.FromAsync(() => Task.Run(() =>
            {
                if (ClientConfiguration.Instance.ModMode)
                    return;

                Updater.OnLocalFileVersionsChecked += () =>
                {
                    Logger.Log($"Game Client Version: {ClientConfiguration.Instance.LocalGame} {Updater.GameVersion}");
                };
                Updater.CheckLocalFileVersions();
            }));

        private IObservable<Unit> InitMapLoader()
            => Observable.FromAsync(() => Task.Run(mapLoaderService.LoadMapsAsync));

        private void Finish()
        {
            ProgramConstants.GAME_VERSION = ClientConfiguration.Instance.ModMode ? "N/A" : Updater.GameVersion;

            MainMenu mainMenu = serviceProvider.GetService<MainMenu>();

            WindowManager.AddAndInitializeControl(mainMenu);
            mainMenu.PostInit();

            if (UserINISettings.Instance.AutomaticCnCNetLogin &&
                NameValidator.IsNameValid(ProgramConstants.PLAYERNAME) == null)
            {
                cncnetManager.Connect();
            }

            if (!UserINISettings.Instance.PrivacyPolicyAccepted)
            {
                WindowManager.AddAndInitializeControl(new PrivacyNotification(WindowManager));
            }

            WindowManager.RemoveControl(this);

            Cursor.Visible = visibleSpriteCursor;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (loadingFinished)
                Finish();
        }
    }
}