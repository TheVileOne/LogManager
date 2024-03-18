using LogManager.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using UnityEngine;
using Descriptions = LogManager.ModConsts.Config.Descriptions;
using OptionLabels = LogManager.ModConsts.Config.OptionLabels;

namespace LogManager
{
    public static class Config
    {
        /// <summary>
        /// Indicates that Config data is safe to be accessed from the OptionInterface (initialized OnModsInIt)  
        /// </summary>
        public static bool SafeToLoad;

        /// <summary>
        /// Contains config values that are managed by the OptionInterface. This should only be interacted with after RainWorld has initialized to avoid errors.
        /// </summary>
        public static OptionInterface.ConfigHolder ConfigData
        {
            get
            {
                if (SafeToLoad)
                    return Plugin.OptionInterface.config;
                return null;
            }
        }

        /// <summary>
        /// Contains config values read directly from the mod config. This data may be accessed at any time in the mod load process.
        /// </summary>
        public static StringDictionary ConfigDataRaw;

        public static Configurable<bool> cfgUseAlternativeDirectory;
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
            SafeToLoad = true;
            ConfigData.configurables.Clear();

            //Define config options
            cfgUseAlternativeDirectory = ConfigData.Bind(nameof(cfgUseAlternativeDirectory), false,
                new ConfigInfo(Descriptions.ALT_DIRECTORY_TOGGLE, new object[]
            {
                OptionLabels.ALT_DIRECTORY_TOGGLE
            }));

            cfgAllowBackups = ConfigData.Bind(nameof(cfgAllowBackups), false,
                new ConfigInfo(Descriptions.ALLOW_BACKUPS_TOGGLE, new object[]
            {
                OptionLabels.ALLOW_BACKUPS_TOGGLE
            }));

            cfgAllowProgressiveBackups = ConfigData.Bind(nameof(cfgAllowProgressiveBackups), false,
                new ConfigInfo(Descriptions.PROGRESSIVE_BACKUPS_TOGGLE, new object[]
            {
                OptionLabels.PROGRESSIVE_BACKUPS_TOGGLE
            }));

            cfgBackupsPerFile = ConfigData.Bind(nameof(cfgBackupsPerFile), 2, new ConfigAcceptableRange<int>(1, 5));
            cfgBackupsPerFile.info.Tags = new object[] { OptionLabels.BACKUPS_PER_FILE };

            cfgBackupEntries = new List<Configurable<bool>>();

            Plugin.OptionInterface.OnConfigChanged += OptionInterface_OnConfigChanged;
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
                    Configurable<T> configSetting = (Configurable<T>)typeof(Config).GetField(settingName).GetValue(null);
                    return configSetting.Value;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error occurred while retrieving config settings");
                Debug.LogError(ex);
            }
            return expectedDefault;
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
        public static string GetTooltip(ConfigurableBase option)
        {
            return option.info.Tags[0] as string;
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

        private static void OptionInterface_OnConfigChanged()
        {
            try
            {
                Plugin.UpdateLogDirectory();
            }
            catch (Exception ex)
            {
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
