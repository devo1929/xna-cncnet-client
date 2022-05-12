﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ClientCore.Exceptions;
using DTAClient.Domain.Multiplayer.CnCNet.QuickMatch.Models;
using DTAClient.Domain.Multiplayer.CnCNet.QuickMatch.Models.Events;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch;

public class QmService : IDisposable
{
    private readonly QmUserSettingsService userSettingsService;
    private readonly QmApiService apiService;
    private readonly QmUserSettings qmUserSettings;
    private readonly QmData qmData;

    private static QmService _instance;
    private readonly Timer retryRequestmatchTimer;
    private QmMatchRequest qmMatchRequest;

    private QmService()
    {
        userSettingsService = QmUserSettingsService.GetInstance();
        apiService = QmApiService.GetInstance();
        qmUserSettings = userSettingsService.GetSettings();
        qmData = new QmData();

        retryRequestmatchTimer = new Timer();
        retryRequestmatchTimer.AutoReset = false;
        retryRequestmatchTimer.Elapsed += (_, _) => RetryRequestMatchAsync();

        qmMatchRequest = new QmMatchRequest();
    }

    public event EventHandler<QmEvent> QmEvent;

    public static QmService GetInstance() => _instance ??= new QmService();

    public IEnumerable<QmUserAccount> GetUserAccounts() => qmData?.UserAccounts;

    public QmLadder GetLadderForId(int ladderId) => qmData.Ladders.FirstOrDefault(l => l.Id == ladderId);

    public string GetCachedEmail() => qmUserSettings.Email;

    public string GetCachedLadder() => qmUserSettings.Ladder;

    public bool IsServerAvailable() => apiService.IsServerAvailable();

    /// <summary>
    /// Login process to cncnet.
    /// </summary>
    /// <param name="email">The email for login.</param>
    /// <param name="password">The password for login.</param>
    public void LoginAsync(string email, string password) =>
        ExecuteLoginRequest(async () =>
        {
            QmAuthData authData = await apiService.LoginAsync(email, password);
            FinishLogin(authData, email);
        });

    /// <summary>
    /// Attempts to refresh an existing auth tokenl.
    /// </summary>
    public void RefreshAsync() =>
        ExecuteLoginRequest(async () =>
        {
            QmAuthData authData = await apiService.RefreshAsync();
            FinishLogin(authData);
        });

    /// <summary>
    /// Simply clear all auth data from our settings.
    /// </summary>
    public void Logout()
    {
        ClearAuthData();
        QmEvent?.Invoke(this, new QmLogoutEvent());
    }

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
            Logger.Log(QmStrings.TokenExpiredError);
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

    public void SetUserAccount(QmUserAccount userAccount)
    {
        string laddAbbr = userAccount?.Ladder?.Abbreviation;
        qmUserSettings.Ladder = laddAbbr;
        qmMatchRequest.Ladder = laddAbbr;
        qmMatchRequest.PlayerName = userAccount?.Username;
        userSettingsService.SaveSettings();
        QmEvent?.Invoke(this, new QmUserAccountSelectedEvent(userAccount));
    }

    public void SetMasterSide(QmSide side)
    {
        qmUserSettings.SideId = side?.LocalId;
        qmMatchRequest.Side = side?.LocalId ?? -1;
        userSettingsService.SaveSettings();
        QmEvent?.Invoke(this, new QmMasterSideSelected(side));
    }

    public void SetMapSides(string[] mapSides)
    {
        // TODO call this from the lobby panel
        qmMatchRequest.MapSides = mapSides;
    }

    public void LoadLaddersAndUserAccountsAsync() =>
        ExecuteRequest(new QmLoadingLaddersAndUserAccountsEvent(), async () =>
        {
            Task<IEnumerable<QmLadder>> loadLaddersTask = apiService.LoadLaddersAsync();
            Task<IEnumerable<QmUserAccount>> loadUserAccountsTask = apiService.LoadUserAccountsAsync();

            await Task.WhenAll(loadLaddersTask, loadUserAccountsTask);
            qmData.Ladders = loadLaddersTask.Result.ToList();
            qmData.UserAccounts = loadUserAccountsTask.Result.ToList();

            if (!qmData.Ladders.Any())
            {
                QmEvent?.Invoke(this, new QmErrorMessageEvent(QmStrings.NoLaddersFoundError));
                return;
            }

            if (!qmData.UserAccounts.Any())
            {
                QmEvent?.Invoke(this, new QmErrorMessageEvent(QmStrings.NoUserAccountsFoundError));
                return;
            }

            QmEvent?.Invoke(this, new QmLaddersAndUserAccountsEvent(qmData.Ladders, qmData.UserAccounts));
        });

    public void LoadLadderMapsForAbbrAsync(string ladderAbbr) =>
        ExecuteRequest(new QmLoadingLadderMapsEvent(), async () =>
        {
            IEnumerable<QmLadderMap> ladderMaps = await apiService.LoadLadderMapsForAbbrAsync(ladderAbbr);
            QmEvent?.Invoke(this, new QmLadderMapsEvent(ladderMaps));
        });

    public void LoadLadderStatsForAbbrAsync(string ladderAbbr) =>
        ExecuteRequest(new QmLoadingLadderStatsEvent(), async () =>
        {
            QmLadderStats ladderStats = await apiService.LoadLadderStatsForAbbrAsync(ladderAbbr);
            QmEvent?.Invoke(this, new QmLadderStatsEvent(ladderStats));
        });

    public void RequestMatchAsync() =>
        ExecuteRequest(new QmRequestingMatchEvent(CancelRequestMatchAsync), async () =>
        {
            QmRequestResponse response = await apiService.QuickMatchRequestAsync(qmMatchRequest);
            HandleQuickMatchResponse(response);
        });

    public void AcceptMatchAsync()
    {
    }

    private void RetryRequestMatchAsync() =>
        RequestMatchAsync();

    public void Dispose()
    {
        apiService.Dispose();
    }

    private void HandleQuickMatchResponse(QmRequestResponse qmRequestResponse)
    {
        switch (true)
        {
            case true when qmRequestResponse is QmRequestWaitResponse waitResponse:
                retryRequestmatchTimer.Interval = waitResponse.CheckBack * 1000;
                retryRequestmatchTimer.Start();
                break;
        }

        QmEvent?.Invoke(this, new QmRequestResponseEvent(qmRequestResponse));
    }

    /// <summary>
    /// We only need to verify the expiration date of the token so that we can refresh or request a new one if it is expired.
    /// We do not need to worry about the signature. The API will handle that validation when the token is used.
    /// </summary>
    /// <param name="token">The token to be decoded.</param>
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

        decoder.Decode(token, "nokey", verify: true);
    }

    private void ClearAuthData()
    {
        userSettingsService.ClearAuthData();
        userSettingsService.SaveSettings();
        apiService.SetToken(null);
    }

    private void CancelRequestMatchAsync() =>
        ExecuteRequest(new QmCancelingRequestMatchEvent(), async () =>
        {
            retryRequestmatchTimer.Stop();
            qmMatchRequest.Type = QmRequestTypes.Quit;
            QmRequestResponse response = await apiService.QuickMatchRequestAsync(new QmQuitRequest());
            QmEvent?.Invoke(this, new QmRequestResponseEvent(response));
        });

    private void ExecuteLoginRequest(Func<Task> func) =>
        ExecuteRequest(new QmLoggingInEvent(), async () =>
        {
            await func();
            QmEvent?.Invoke(this, new QmLoginEvent());
        });

    private void ExecuteRequest(QmEvent qmEvent, Func<Task> requestAction)
    {
        QmEvent?.Invoke(this, qmEvent);
        Task.Run(async () =>
        {
            try
            {
                await requestAction();
            }
            catch (Exception e)
            {
                QmEvent?.Invoke(this, new QmErrorMessageEvent((e as ClientException)?.Message ?? QmStrings.UnknownError));
            }
        });
    }

    private void FinishLogin(QmAuthData authData, string email = null)
    {
        qmUserSettings.AuthData = authData;
        qmUserSettings.Email = email ?? qmUserSettings.Email;
        userSettingsService.SaveSettings();

        apiService.SetToken(authData.Token);
    }
}