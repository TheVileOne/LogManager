using LogManager.Controllers;
using LogManager.Helpers;
using LogUtils;
using LogUtils.Enums;
using LogUtils.Helpers.FileHandling;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using Descriptions = LogManager.ModConsts.Config.Descriptions;
using OptionLabels = LogManager.ModConsts.Config.OptionLabels;
using BackupEntry = (LogUtils.Enums.LogID ID, bool Enabled);

namespace LogManager.Settings
{
    public static class ConfigSettings
    {
        /// <summary>
        /// Indicates that Config data is safe to be accessed from the OptionInterface (initialized OnModsInIt)  
        /// </summary>
        public static bool SafeToLoad;

        public static bool SaveInProgress;
        public static bool ReloadInProgress;

        /// <summary>
        /// Contains config values that are managed by the OptionInterface. This should only be interacted with after RainWorld has initialized to avoid errors.
        /// </summary>
        public static OptionInterface.ConfigHolder ConfigData => Plugin.OptionInterface?.config;

        /// <summary>
        /// Contains config values read directly from the mod config. This data may be accessed at any time in the mod load process.
        /// </summary>
        public static StringDictionary ConfigDataRaw;

        public static Configurable<string> cfgLogsFolderPath;
        public static Configurable<bool> cfgAllowBackups;
        public static Configurable<bool> cfgAllowProgressiveBackups;

        public static Configurable<int> cfgBackupsPerFile;

        public static List<Configurable<bool>> cfgBackupEntries;

        public static void Load()
        {
            ConfigDataRaw = ConfigReader.ReadFile(Plugin.ConfigFilePath);
        }

        public static void Initialize()
        {
            ConfigData.configurables.Clear();

            //Define config options

            cfgLogsFolderPath = ConfigData.Bind(nameof(cfgLogsFolderPath), LogsFolder.AvailablePaths[0],
                new ConfigInfo(Descriptions.DIRECTORY_SELECT, OptionLabels.DIRECTORY_SELECT));

            cfgAllowBackups = ConfigData.Bind(nameof(cfgAllowBackups), false,
                new ConfigInfo(Descriptions.ALLOW_BACKUPS_TOGGLE, OptionLabels.ALLOW_BACKUPS_TOGGLE));

            cfgAllowProgressiveBackups = ConfigData.Bind(nameof(cfgAllowProgressiveBackups), false,
                new ConfigInfo(Descriptions.PROGRESSIVE_BACKUPS_TOGGLE, OptionLabels.PROGRESSIVE_BACKUPS_TOGGLE));

            cfgBackupsPerFile = ConfigData.Bind(nameof(cfgBackupsPerFile), BackupController.ALLOWED_BACKUPS_PER_FILE, new ConfigAcceptableRange<int>(1, 5));
            cfgBackupsPerFile.info.Tags = new object[] { OptionLabels.BACKUPS_PER_FILE };

            cfgBackupEntries = new List<Configurable<bool>>();

            RefreshValues();
            SafeToLoad = true;
        }

        /// <summary>
        /// Assigns converted raw data into the respective config entries
        /// </summary>
        public static void RefreshValues()
        {
            Plugin.Logger.LogInfo("Setting config values");
            SetValue(cfgLogsFolderPath);
            SetValue(cfgAllowBackups);
            SetValue(cfgAllowProgressiveBackups);
            SetValue(cfgBackupsPerFile);

            cfgBackupEntries.ForEach(SetValue);
            Plugin.Logger.LogInfo("Setting complete");
        }

        public static T GetValue<T>(string settingName, T expectedDefault) where T : IConvertible
        {
            try
            {
                if (!SafeToLoad)
                {
                    if (ConfigDataRaw.ContainsKey(settingName))
                        return ConfigDataRaw[settingName].ConvertParse<T>();
                }
                else
                {
                    //Use reflection to get the correct configurable and return its value
                    Configurable<T> configSetting = (Configurable<T>)typeof(ConfigSettings).GetField(settingName).GetValue(null);
                    return configSetting.Value;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while retrieving config settings");
                Plugin.Logger.LogError(ex);
            }
            return expectedDefault;
        }

        public static void SetValue<T>(Configurable<T> configurable) where T : IConvertible
        {
            configurable.Value = GetValue(configurable.key, configurable.defaultValue.ConvertParse<T>());
        }

        /// <summary>
        /// Gets the string representing a path selection option
        /// </summary>
        public static string GetPathOptionName(string path)
        {
            //The root path gets a more descriptive name
            if (PathUtils.PathsAreEqual(path, Plugin.GameRootPath))
            {
                TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                return textInfo.ToTitleCase(UtilityConsts.PathKeywords.ROOT);
            }
            return Path.GetFileName(path);
        }

        /// <summary>
        /// Gets the string that appears on the label associated with a config option
        /// </summary>
        public static string GetDescription(ConfigurableBase option)
        {
            return option.info.description;
        }

        /// <summary>
        /// Gets the string that appears on the bottom of the screen and describes the function of the config option when hovered
        /// </summary>
        public static string GetOptionLabel(ConfigurableBase option)
        {
            if (option.info.Tags.Length == 0)
                return string.Empty;

            return option.info.Tags[0]?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Searches the mod config file for a config setting and returns the stored setting value if one exists, expectedDefault otherwise
        /// </summary>
        public static T ReadFromDisk<T>(string settingName, T expectedDefault) where T : IConvertible
        {
            if (!File.Exists(Plugin.ConfigFilePath))
                return expectedDefault;

            return ConfigReader.ReadFromDisk(Plugin.ConfigFilePath, settingName, expectedDefault);
        }

        public static List<(LogID, bool)> GetBackupEnabledChanges()
        {
            int backupEntryCount = Plugin.BackupController.BackupEntries.Count;
            int configEntryCount = cfgBackupEntries.Count;

            if (configEntryCount != backupEntryCount)
            {
                Plugin.Logger.LogInfo("Config Entry Count " + configEntryCount);
                Plugin.Logger.LogInfo("Backup Entry Count " + backupEntryCount);

                //This shouldn't log under normal circumstances
                Plugin.Logger.LogWarning("Backup entry count detected does not match managed entry count");
                return new List<BackupEntry>();
            }

            List<BackupEntry> detectedChanges = new List<BackupEntry>();

            //Cycle through both lists until one of the entries doesn't match. The list order should be the same here.
            for (int i = 0; i < backupEntryCount; i++)
            {
                var backupEntry = Plugin.BackupController.BackupEntries[i];
                var backupConfigurable = cfgBackupEntries[i];

                //Check that enabled bool matches
                if (backupConfigurable.Value != backupEntry.Enabled)
                    detectedChanges.Add((backupEntry.ID, !SaveInProgress ? backupEntry.Enabled : backupConfigurable.Value));
            }
            return detectedChanges;
        }

        public static void HandleBackupEnabledChanges()
        {
            if (!Plugin.OptionInterface.HasInitialized) return;

            try
            {
                if (!SaveInProgress || cfgBackupEntries.Count == 0)
                {
                    Plugin.OptionInterface.ProcessBackupEnableOptions();
                }
                else //Until config entries are processed once, we shouldn't run this code yet
                {
                    List<BackupEntry> detectedChanges = GetBackupEnabledChanges();

                    if (detectedChanges.Count > 0)
                    {
                        Plugin.Logger.LogInfo("Backup enable state changes detected");
                        Plugin.BackupController.ProcessChanges(detectedChanges);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while processing backup options");
                Plugin.Logger.LogError(ex);
            }
        }

        public class ConfigInfo : ConfigurableInfo
        {
            public ConfigInfo(string description, params object[] tags) : base(description, null, string.Empty, tags)
            {
            }
        }
    }
}
