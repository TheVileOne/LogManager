using LogManager.Helpers;
using System;
using System.Collections.Specialized;
using System.IO;
using UnityEngine;

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

        public static void Load()
        {
            ConfigDataRaw = ConfigReader.ReadFile(Plugin.ConfigFilePath);
        }

        public static void Initialize()
        {
            SafeToLoad = true;
            ConfigData.configurables.Clear();

            //Define config options
            cfgUseAlternativeDirectory = ConfigData.Bind("cfgUseAlternativeDirectory", false, new ConfigInfo("Choose your Logs folder", new object[]
            {
                "Prefer StreamingAssets folder for Logs directory"
            }));

            cfgAllowBackups = ConfigData.Bind("cfgAllowBackups", false, new ConfigInfo("Allow log backups", new object[]
            {
                "Backup log files when Rain World starts"
            }));

            cfgAllowProgressiveBackups = ConfigData.Bind("cfgAllowProgressiveBackups", false, new ConfigInfo("Enable backups for newly detected log files on startup", new object[]
            {
                "Automatically enable backups for newly detected log files"
            }));

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
                Debug.LogError("Exception occurred while retrieving config settings");
                Debug.LogError(ex);
            }
            return expectedDefault;
        }

        /// <summary>
        /// Gets the string that appears on the label associated with a config option
        /// </summary>
        public static string GetDescription(Configurable<bool> option)
        {
            return option.info.description;
        }

        /// <summary>
        /// Gets the string that appears on the bottom of the screen and describes the function of the config option when hovered
        /// </summary>
        public static string GetTooltip(Configurable<bool> option)
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
