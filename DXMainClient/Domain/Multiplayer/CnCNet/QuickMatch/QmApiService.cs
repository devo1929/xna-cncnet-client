using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ClientCore.Exceptions;
using Newtonsoft.Json;
using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmApiService
    {
        private HttpClient _httpClient;
        private readonly QmSettings qmSettings;
        private string _token;

        public QmApiService()
        {
            qmSettings = QmSettingsService.GetInstance().GetSettings();
        }

        public void SetToken(string token)
        {
            _token = token;
        }

        public async Task<IEnumerable<QmLadderMap>> FetchLadderMapsForAbbrAsync(string ladderAbbreviation)
        {
            var httpClient = GetAuthenticatedClient();
            string url = string.Format(qmSettings.GetLadderMapsUrl, ladderAbbreviation);
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new ClientException($"Error fetching ladder maps: {response.ReasonPhrase}");

            return JsonConvert.DeserializeObject<IEnumerable<QmLadderMap>>(await response.Content.ReadAsStringAsync());
        }

        public async Task<IEnumerable<QmUserAccount>> FetchUserAccountsAsync()
        {
            var httpClient = GetAuthenticatedClient();
            var response = await httpClient.GetAsync(qmSettings.GetUserAccountsUrl);
            if (!response.IsSuccessStatusCode)
                throw new ClientException($"Error fetching user accounts: {response.ReasonPhrase}");

            return JsonConvert.DeserializeObject<IEnumerable<QmUserAccount>>(await response.Content.ReadAsStringAsync());
        }

        public async Task<IEnumerable<QmLadder>> FetchLaddersAsync()
        {
            var httpClient = GetAuthenticatedClient();
            var response = await httpClient.GetAsync(qmSettings.GetLaddersUrl);
            if (!response.IsSuccessStatusCode)
                throw new ClientException($"Error fetching ladders: {response.ReasonPhrase}");

            return JsonConvert.DeserializeObject<IEnumerable<QmLadder>>(await response.Content.ReadAsStringAsync());
        }

        public async Task<QmAuthData> LoginAsync(string email, string password)
        {
            var httpClient = GetHttpClient();
            var postBodyContent = new StringContent(JsonConvert.SerializeObject(new QMLoginRequest()
            {
                Email = email,
                Password = password
            }), Encoding.Default, "application/json");
            var response = await httpClient.PostAsync(qmSettings.LoginUrl, postBodyContent);

            return await HandleLoginResponse(response, "Error logging in: {0}, {1}");
        }

        public async Task<QmAuthData> RefreshAsync()
        {
            var httpClient = GetAuthenticatedClient();
            var response = await httpClient.GetAsync(qmSettings.RefreshUrl);

            return await HandleLoginResponse(response, "Error refreshing token: {0}, {1}");
        }

        private async Task<QmAuthData> HandleLoginResponse(HttpResponseMessage response, string unknownErrorFormat)
        {
            if (!response.IsSuccessStatusCode)
                return await HandleFailedLoginResponse(response, unknownErrorFormat);

            string responseBody = await response.Content.ReadAsStringAsync();
            var authData = JsonConvert.DeserializeObject<QmAuthData>(responseBody);
            if (authData == null)
                throw new ClientException(responseBody);

            _token = authData.Token;
            return authData;
        }

        private async Task<QmAuthData> HandleFailedLoginResponse(HttpResponseMessage response, string unknownErrorFormat)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            string message;
            switch (response.StatusCode)
            {
                case HttpStatusCode.BadGateway:
                    message = "Server unreachable";
                    break;
                case HttpStatusCode.Unauthorized:
                    message = "Invalid username/password";
                    break;
                default:
                    message = string.Format(unknownErrorFormat, response.ReasonPhrase, responseBody);
                    break;
            }

            throw new ClientRequestException(message, response.StatusCode);
        }


        public bool IsServerAvailable()
        {
            var httpClient = GetAuthenticatedClient();
            var response = httpClient.GetAsync(qmSettings.ServerStatusUrl).Result;
            return response.IsSuccessStatusCode;
        }

        private HttpClient GetHttpClient() =>
            _httpClient ?? (_httpClient = new HttpClient
            {
                BaseAddress = new Uri(qmSettings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            });

        private HttpClient GetAuthenticatedClient()
        {
            var httpClient = GetHttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
            return httpClient;
        }

        public async Task<QmRequestResponse> QuickMatchAsync(QmRequest qmRequest)
        {
            var httpClient = GetAuthenticatedClient();
            string url = string.Format(qmSettings.QuickMatchUrl, qmRequest.Ladder, qmRequest.PlayerName);
            var response = await httpClient.PostAsync(url, new StringContent(JsonConvert.SerializeObject(qmRequest), Encoding.Default, "application/json"));

            string responseBody = await response.Content.ReadAsStringAsync();
            Logger.Log(responseBody);
            var matchRequestResponse = JsonConvert.DeserializeObject<QmRequestResponse>(responseBody);

            if (!response.IsSuccessStatusCode)
                throw new ClientException($"Error requesting match: {response.ReasonPhrase}");

            if (!matchRequestResponse.IsSuccessful)
                throw new ClientException($"Error requesting match: {matchRequestResponse.Message ?? matchRequestResponse.Description ?? "unknown"}");

            return matchRequestResponse;
        }
    }
}
