using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientCore.Exceptions;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using DTAClient.Domain.Multiplayer.CnCNet.QuickMatch;
using Localization;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.QuickMatch
{
    public class QuickMatchLobbyPanel : QuickMatchPanel
    {
        private const int TAB_WIDTH = 133;

        private readonly QmService qmService;
        private readonly MapLoader mapLoader;

        public event EventHandler LogoutEvent;

        private QuickMatchMapList mapList;
        private XNAClientButton btnQuickMatch;
        private XNAClientButton btnLogout;
        private XNAClientButton btnExit;
        private XNAClientDropDown ddUserAccounts;
        private XNAClientDropDown ddNicknames;
        private XNAClientDropDown ddSides;
        private XNAPanel mapPreviewBox;
        private XNAPanel settingsPanel;
        private XNAClientTabControl tabPanel;

        private bool requestingMatchStatus;
        private readonly EnhancedSoundEffect matchFoundSoundEffect;
        private readonly QmSettings qmSettings;

        public QuickMatchLobbyPanel(WindowManager windowManager) : base(windowManager)
        {
            qmService = QmService.GetInstance();
            qmService.UserAccountsEvent += UserAccountsEvent;
            qmService.LadderMapsEvent += LadderMapsEvent;
            qmService.MatchedEvent += MatchedEvent;
            qmService.QmRequestEvent += QmRequestResponseEvent;

            mapLoader = MapLoader.GetInstance();

            qmSettings = QmSettingsService.GetInstance().GetSettings();
            matchFoundSoundEffect = new EnhancedSoundEffect(qmSettings.MatchFoundSoundFile);
        }

        public override void Initialize()
        {
            Name = nameof(QuickMatchLobbyPanel);

            base.Initialize();

            mapList = FindChild<QuickMatchMapList>(nameof(QuickMatchMapList));
            mapList.MapSelected += MapSelected;

            btnLogout = FindChild<XNAClientButton>(nameof(btnLogout));
            btnLogout.LeftClick += BtnLogout_LeftClick;

            btnExit = FindChild<XNAClientButton>(nameof(btnExit));
            btnExit.LeftClick += Exit_Click;

            btnQuickMatch = FindChild<XNAClientButton>(nameof(btnQuickMatch));
            btnQuickMatch.LeftClick += BtnQuickMatch_LeftClick;

            ddUserAccounts = FindChild<XNAClientDropDown>(nameof(ddUserAccounts));
            ddUserAccounts.SelectedIndexChanged += UserAccountSelected;

            ddNicknames = FindChild<XNAClientDropDown>(nameof(ddNicknames));

            ddSides = FindChild<XNAClientDropDown>(nameof(ddSides));

            mapPreviewBox = FindChild<XNAPanel>(nameof(mapPreviewBox));
            mapPreviewBox.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.CENTERED;

            settingsPanel = FindChild<XNAPanel>(nameof(settingsPanel));
            settingsPanel.Disable();

            tabPanel = FindChild<XNAClientTabControl>(nameof(tabPanel));
            tabPanel.AddTab("Map".L10N("QM:Tabs:Map"), TAB_WIDTH);
            tabPanel.AddTab("Settings".L10N("QM:Tabs:Settings"), TAB_WIDTH);
            tabPanel.SelectedIndexChanged += TabSelected;

            EnabledChanged += EnabledChangedEvent;
        }

        private void EnabledChangedEvent(object sender, EventArgs e)
        {
            if (!Enabled)
                return;

            FetchLaddersAndUserAccountsAsync();
        }

        private void FetchLaddersAndUserAccountsAsync()
        {
            qmService.ShowStatus("Fetching ladders and accounts...");
            Task.Run(async () =>
            {
                try
                {
                    var qmData = await qmService.FetchLaddersAndUserAccountsAsync();
                    qmService.ClearStatus();
                    UserAccountsUpdated(qmData.UserAccounts);
                }
                catch (Exception e)
                {
                    string error = (e as ClientException)?.Message ?? "Error fetching ladders and accounts...";
                    ShowError(error);
                    qmService.ClearStatus();
                }
            });
        }

        private void TabSelected(object sender, EventArgs eventArgs)
        {
            switch (tabPanel.SelectedTab)
            {
                case 0:
                    mapPreviewBox.Enable();
                    settingsPanel.Disable();
                    return;
                case 1:
                    mapPreviewBox.Disable();
                    settingsPanel.Enable();
                    return;
            }
        }

        private void BtnQuickMatch_LeftClick(object sender, EventArgs eventArgs)
            => RequestQuickMatch();

        private void RequestQuickMatch()
        {
            var matchRequest = GetMatchRequest();
            if (matchRequest == null)
                return;

            StartRequestingMatchStatus();
            Task.Run(async () =>
            {
                try
                {
                    HandleQuickMatchResponse(await qmService.QuickMatchAsync(matchRequest));
                }
                catch (Exception e)
                {
                    string message = (e as ClientException)?.Message ?? "Error requesting match";
                    requestingMatchStatus = false;
                    ShowError(message);
                    qmService.ClearStatus();
                }
            });
        }

        private void StartRequestingMatchStatus()
        {
            if (requestingMatchStatus)
                return;

            const string baseMessage = "Requesting match";
            string message = baseMessage;
            int maxLength = baseMessage.Length + 4;
            requestingMatchStatus = true;
            Task.Run(() =>
            {
                while (requestingMatchStatus)
                {
                    message = message.Length > maxLength ? message.Substring(0, baseMessage.Length) : message + ".";
                    qmService.ShowStatus(message);
                    Thread.Sleep(500);
                }
            });
        }

        private void HandleQuickMatchResponse(QmRequestResponse qmRequestResponse)
        {
            switch (true)
            {
                case true when qmRequestResponse.IsError:
                case true when qmRequestResponse.IsFatal:
                    HandleQuickMatchErrorResponse(qmRequestResponse);
                    return;
                case true when qmRequestResponse.IsUpdate:
                    HandleQuickMatchUpdateResponse(qmRequestResponse);
                    return;
                case true when qmRequestResponse.IsSpawn:
                    HandleQuickMatchSpawnResponse(qmRequestResponse);
                    return;
                case true when qmRequestResponse.IsQuit:
                    HandleQuickMatchQuitResponse(qmRequestResponse);
                    return;
                case true when qmRequestResponse.IsQuit:
                    HandleQuickMatchQuitResponse(qmRequestResponse);
                    return;
                case true when qmRequestResponse.IsWait:
                    HandleQuickMatchWaitResponse(qmRequestResponse);
                    return;
            }
        }

        private void HandleQuickMatchSpawnResponse(QmRequestResponse qmRequestResponse)
        {
            requestingMatchStatus = false;
            ShowError("qm spawn");
        }

        private void HandleQuickMatchUpdateResponse(QmRequestResponse qmRequestResponse)
        {
            requestingMatchStatus = false;
            ShowError("qm update");
        }

        private void HandleQuickMatchQuitResponse(QmRequestResponse qmRequestResponse)
        {
            requestingMatchStatus = false;
            ShowError("qm quit");
        }

        private void HandleQuickMatchWaitResponse(QmRequestResponse qmRequestResponse)
        {
            SoundPlayer.Play(matchFoundSoundEffect);
            Thread.Sleep(qmRequestResponse.CheckBack * 1000);
            RequestQuickMatch();
        }

        private void HandleQuickMatchErrorResponse(QmRequestResponse qmRequestResponse)
        {
            qmService.ClearStatus();
            ShowError(qmRequestResponse.Message ?? qmRequestResponse.Description);
            requestingMatchStatus = false;
        }

        private void QmRequestResponseEvent(object sender, QmRequestEventArgs args)
        {
            QmRequestResponse response = args.Response;
            if (!response.IsSuccessful)
            {
                XNAMessageBox.Show(WindowManager, "Error", response.Message ?? response.Description ?? "Unknown error occurred.");
                return;
            }

            XNAMessageBox.Show(WindowManager, "test", response.Type);
        }

        private QmRequest GetMatchRequest()
        {
            var userAccount = GetSelectedUserAccount();
            if (userAccount == null)
            {
                XNAMessageBox.Show(WindowManager, "Error", "No ladder selected");
                return null;
            }

            var side = GetSelectedSide();
            if (side == null)
            {
                XNAMessageBox.Show(WindowManager, "Error", "No side selected");
                return null;
            }

            return new QmRequest()
            {
                Ladder = userAccount.Ladder.Abbreviation,
                PlayerName = userAccount.Username,
                Side = side.Id
            };
        }

        private QmSide GetSelectedSide()
            => ddSides.SelectedItem?.Tag as QmSide;

        private QmUserAccount GetSelectedUserAccount()
            => ddUserAccounts.SelectedItem?.Tag as QmUserAccount;

        private void BtnLogout_LeftClick(object sender, EventArgs eventArgs)
        {
            XNAMessageBox.ShowYesNoDialog(WindowManager, "Confirmation", "Are you sure you want to log out?", box =>
            {
                qmService.Logout();
                LogoutEvent?.Invoke(this, null);
            });
        }

        private void UserAccountsUpdated(IEnumerable<QmUserAccount> userAccounts)
        {
            ddUserAccounts.Items.Clear();
            foreach (QmUserAccount userAccount in userAccounts)
            {
                ddUserAccounts.AddItem(new XNADropDownItem()
                {
                    Text = userAccount.Ladder.Name,
                    Tag = userAccount
                });
            }

            if (ddUserAccounts.Items.Count == 0)
                return;

            string cachedLadder = qmService.GetCachedLadder();
            if (!string.IsNullOrEmpty(cachedLadder))
                ddUserAccounts.SelectedIndex = ddUserAccounts.Items.FindIndex(i => (i.Tag as QmUserAccount)?.Ladder.Abbreviation == cachedLadder);

            if (ddUserAccounts.SelectedIndex < 0)
                ddUserAccounts.SelectedIndex = 0;
        }

        /// <summary>
        /// Called when the QM service has finished the login process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="qmUserAccountsEventArgs"></param>
        private void UserAccountsEvent(object sender, QmUserAccountsEventArgs qmUserAccountsEventArgs)
            => UserAccountsUpdated(qmUserAccountsEventArgs.UserAccounts);

        /// <summary>
        /// Called when the user has selected a UserAccount from the drop down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void UserAccountSelected(object sender, EventArgs eventArgs)
        {
            if (!(ddUserAccounts.SelectedItem?.Tag is QmUserAccount selectedUserAccount))
                return;

            UpdateNickames(selectedUserAccount);
            UpdateSides(selectedUserAccount);
            mapList.Clear();
            qmService.SetLadder(selectedUserAccount.Ladder.Abbreviation);

            FetchLadderMapsForAbbrAsync(selectedUserAccount.Ladder.Abbreviation);
        }

        private void FetchLadderMapsForAbbrAsync(string ladderAbbr)
        {
            qmService.ShowStatus("Fetching ladder maps...");
            Task.Run(async () =>
            {
                try
                {
                    await qmService.FetchLadderMapsForAbbrAsync(ladderAbbr);
                    qmService.ClearStatus();
                }
                catch (Exception e)
                {
                    string message = (e as ClientException)?.Message ?? "Error fetching ladder maps";
                    ShowError(message);
                }
            });
        }

        /// <summary>
        /// Update the nicknames drop down
        /// </summary>
        /// <param name="selectedUserAccount"></param>
        private void UpdateNickames(QmUserAccount selectedUserAccount)
        {
            ddNicknames.Items.Clear();

            ddNicknames.AddItem(new XNADropDownItem()
            {
                Text = selectedUserAccount.Username,
                Tag = selectedUserAccount
            });

            ddNicknames.SelectedIndex = 0;
        }

        /// <summary>
        /// Update the top Sides dropdown
        /// </summary>
        /// <param name="selectedUserAccount"></param>
        private void UpdateSides(QmUserAccount selectedUserAccount)
        {
            ddSides.Items.Clear();

            var ladder = qmService.GetLadderForId(selectedUserAccount.LadderId);

            foreach (QmSide side in ladder.Sides)
            {
                ddSides.AddItem(new XNADropDownItem
                {
                    Text = side.Name,
                    Tag = side
                });
            }

            if (ddSides.Items.Count > 0)
                ddSides.SelectedIndex = 0;
        }

        /// <summary>
        /// Called when the QM service has fetched new ladder maps for the ladder selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="qmLadderMapsEventArgs"></param>
        private void LadderMapsEvent(object sender, QmLadderMapsEventArgs qmLadderMapsEventArgs)
        {
            mapList.Clear();
            var ladderMaps = qmLadderMapsEventArgs?.Maps?.ToList() ?? new List<QmLadderMap>();
            if (!ladderMaps.Any())
                return;

            if (ddUserAccounts.SelectedItem?.Tag is not QmUserAccount selectedUserAccount)
                return;

            var ladder = qmService.GetLadderForId(selectedUserAccount.LadderId);

            mapList.AddItems(ladderMaps.Select(ladderMap => new QuickMatchMapListItem(WindowManager, ladderMap, ladder)));
        }

        private void MatchedEvent(object sender, EventArgs eventArgs)
        {
        }

        /// <summary>
        /// Called when the user selects a map in the list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="qmMap"></param>
        private void MapSelected(object sender, QmMapSelectedEventArgs qmMapSelectedEventArgs)
        {
            if (qmMapSelectedEventArgs?.Map == null)
                return;

            var map = mapLoader.GetMapForSHA(qmMapSelectedEventArgs.Map.Hash);

            mapPreviewBox.BackgroundTexture = map?.LoadPreviewTexture();
        }
    }
}