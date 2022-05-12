using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClientCore.Exceptions;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmService
    {
        private readonly QmUserSettingsService userSettingsService;
        private readonly QmApiService apiService;
        private readonly QmUserSettings qmUserSettings;
        private readonly QmData qmData;

        private static QmService Instance;

        public event EventHandler<QmStatusMessageEventArgs> StatusMessageEvent;

        public event EventHandler FetchDataEvent;

        public event EventHandler<QmLadderMapsEventArgs> LadderMapsEvent;

        public event EventHandler<EventArgs> MatchedEvent;

        public event EventHandler<QmUserAccountsEventArgs> UserAccountsEvent;

        public event EventHandler<QmLaddersEventArgs> LaddersEvent;

        public event EventHandler<QmRequestEventArgs> QmRequestEvent;

        private QmService()
        {
            userSettingsService = new QmUserSettingsService();
            apiService = new QmApiService();
            qmUserSettings = userSettingsService.LoadSettings();
            qmData = new QmData();
        }

        public static QmService GetInstance() => Instance ?? (Instance = new QmService());

        /// <summary>
        /// Login process to cncnet
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        public async Task LoginAsync(string email, string password)
        {
            var authData = await apiService.LoginAsync(email, password);
            FinishLogin(authData, false, email);
        }

        /// <summary>
        /// Attempts to refresh an existing auth token
        /// </summary>
        public async Task RefreshAsync()
        {
            try
            {
                var authData = await apiService.RefreshAsync();
                FinishLogin(authData, false);
            }
            catch (Exception)
            {
                ClearAuthData();
                throw;
            }
        }

        /// <summary>
        /// Simply clear all auth data from our settings
        /// </summary>
        public void Logout() => ClearAuthData();

        private void ClearAuthData()
        {
            userSettingsService.ClearAuthData();
            userSettingsService.SaveSettings();
        }

        public IEnumerable<QmUserAccount> GetUserAccounts() => qmData?.UserAccounts;

        public QmLadder GetLadderForId(int ladderId) => qmData.Ladders.FirstOrDefault(l => l.Id == ladderId);

        public string GetCachedEmail() => qmUserSettings.Email;

        public string GetCachedLadder() => qmUserSettings.Ladder;

        public bool IsServerAvailable() => apiService.IsServerAvailable();

        public bool IsLoggedIn()
        {
            if (qmUserSettings.AuthData == null)
                return false;

            try
            {
                DecodeToken(qmUserSettings.AuthData.Token);
            }
            catch (TokenExpiredException)
            {
                Logger.Log("QuickMatch token is expired");
                return false;
            }
            catch (Exception e)
            {
                Logger.Log(e.StackTrace);
                return false;
            }

            apiService.SetToken(qmUserSettings.AuthData.Token);

            return true;
        }

        public void SetLadder(string ladder)
        {
            qmUserSettings.Ladder = ladder;
            userSettingsService.SaveSettings();
        }

        public async Task<QmData> FetchLaddersAndUserAccountsAsync()
        {
            var fetchLaddersTask = apiService.FetchLaddersAsync();
            var fetchUserAccountsTask = apiService.FetchUserAccountsAsync();

            await Task.WhenAll(fetchLaddersTask, fetchUserAccountsTask);
            qmData.Ladders = fetchLaddersTask.Result.ToList();
            qmData.UserAccounts = fetchUserAccountsTask.Result.ToList();

            if (!qmData.Ladders.Any())
                throw new ClientException("No quick match ladders currently found.");

            if (!qmData.UserAccounts.Any())
                throw new ClientException("No user accounts found in quick match. Are you registered for this month?");

            return qmData;
        }

        public async Task FetchLadderMapsForAbbrAsync(string ladderAbbr)
        {
            var ladderMaps = await apiService.FetchLadderMapsForAbbrAsync(ladderAbbr);

            LadderMapsEvent?.Invoke(this, new QmLadderMapsEventArgs(ladderMaps));
        }

        private void FinishLogin(QmAuthData authData, bool refresh, string email = null)
        {
            qmUserSettings.AuthData = authData;
            qmUserSettings.Email = email ?? qmUserSettings.Email;
            userSettingsService.SaveSettings();
        }

        /// <summary>
        /// We only need to verify the expiration date of the token so that we can refresh or request a new one if it is expired.
        /// We do not need to worry about the signature. The API will handle that validation when the token is used.
        /// </summary>
        /// <param name="token"></param>
        private static void DecodeToken(string token)
        {
            IJsonSerializer serializer = new JsonNetSerializer();
            IDateTimeProvider provider = new UtcDateTimeProvider();
            ValidationParameters validationParameters = ValidationParameters.Default;
            validationParameters.ValidateSignature = false;
            IJwtValidator validator = new JwtValidator(serializer, provider, validationParameters);
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtAlgorithm algorithm = new HMACSHA256Algorithm(); // symmetric
            IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder, algorithm);

            decoder.Decode(token, "nosecret", verify: true);
        }

        public async Task<QmRequestResponse> QuickMatchAsync(QmRequest qmRequest)
            => await apiService.QuickMatchAsync(qmRequest);

        public void ShowStatus(string statusMessage, string buttonText = null, Action buttonAction = null)
            => StatusMessageEvent?.Invoke(this, new QmStatusMessageEventArgs(statusMessage, buttonText, buttonAction));

        public void ClearStatus()
            => StatusMessageEvent?.Invoke(this, null);
    }
}