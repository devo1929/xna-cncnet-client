using System.IO;
using ClientCore;
using Rampastring.Tools;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmSettingsService
    {
        private static QmSettingsService Instance;
        private static readonly string SettingsFile = ClientConfiguration.Instance.QuickMatchPath;

        private const string BasicSectionKey = "Basic";
        private const string SoundsSectionKey = "Sounds";

        private const string BaseUrlKey = "BaseUrl";
        private const string LoginUrlKey = "LoginUrl";
        private const string RefreshUrlKey = "RefreshUrl";
        private const string ServerStatusUrlKey = "ServerStatusUrl";
        private const string GetUserAccountsUrlKey = "GetUserAccountsUrl";
        private const string GetLaddersUrlKey = "GetLaddersUrl";
        private const string GetLadderMapsUrlKey = "GetLadderMapsUrl";

        private const string MatchFoundSoundFileKey = "MatchFoundSoundFile";

        private QmSettings qmSettings;

        private QmSettingsService()
        {
        }

        public static QmSettingsService GetInstance() => Instance ?? (Instance = new QmSettingsService());

        public QmSettings GetSettings() => qmSettings ?? LoadSettings();

        private QmSettings LoadSettings()
        {
            qmSettings = new QmSettings();
            if (!File.Exists(SettingsFile))
                SaveSettings(); // init the settings file

            var iniFile = new IniFile(SettingsFile);
            var basicSection = iniFile.GetSection(BasicSectionKey);
            if (basicSection == null)
                return qmSettings;

            qmSettings.BaseUrl = basicSection.GetStringValue(BaseUrlKey, QmSettings.DefaultBaseUrl);
            qmSettings.LoginUrl = basicSection.GetStringValue(LoginUrlKey, QmSettings.DefaultLoginUrl);
            qmSettings.RefreshUrl = basicSection.GetStringValue(RefreshUrlKey, QmSettings.DefaultRefreshUrl);
            qmSettings.ServerStatusUrl = basicSection.GetStringValue(ServerStatusUrlKey, QmSettings.DefaultServerStatusUrl);
            qmSettings.GetUserAccountsUrl = basicSection.GetStringValue(GetUserAccountsUrlKey, QmSettings.DefaultGetUserAccountsUrl);
            qmSettings.GetLaddersUrl = basicSection.GetStringValue(GetLaddersUrlKey, QmSettings.DefaultGetLaddersUrl);
            qmSettings.GetLadderMapsUrl = basicSection.GetStringValue(GetLadderMapsUrlKey, QmSettings.DefaultGetLadderMapsUrl);

            var soundsSection = iniFile.GetSection(SoundsSectionKey);
            if (soundsSection == null)
                return qmSettings;

            string matchFoundSoundFile = soundsSection.GetStringValue(MatchFoundSoundFileKey, null);
            if (File.Exists(matchFoundSoundFile))
                qmSettings.MatchFoundSoundFile = matchFoundSoundFile;

            return qmSettings;
        }


        public void SaveSettings()
        {
            var iniFile = new IniFile();
            var basicSection = new IniSection(BasicSectionKey);
            basicSection.AddKey(BaseUrlKey, qmSettings.BaseUrl);
            basicSection.AddKey(LoginUrlKey, qmSettings.LoginUrl);
            basicSection.AddKey(RefreshUrlKey, qmSettings.RefreshUrl);
            basicSection.AddKey(ServerStatusUrlKey, qmSettings.ServerStatusUrl);
            basicSection.AddKey(GetUserAccountsUrlKey, qmSettings.GetUserAccountsUrl);
            basicSection.AddKey(GetLaddersUrlKey, qmSettings.GetLaddersUrl);
            basicSection.AddKey(GetLadderMapsUrlKey, qmSettings.GetLadderMapsUrl);

            iniFile.AddSection(basicSection);
            iniFile.WriteIniFile(SettingsFile);
        }
    }
}
