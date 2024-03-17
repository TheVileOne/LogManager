using System;
using System.Collections.Generic;
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


        public static Configurable<bool> cfgUseAlternativeDirectory;
        public static Configurable<bool> cfgAllowBackups;
        public static Configurable<bool> cfgAllowProgressiveBackups;

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

            IEnumerator<string> configData = File.ReadLines(Plugin.ConfigFilePath).GetEnumerator();

            IConvertible data = expectedDefault;
            bool dataFound = false;
            while (!dataFound && configData.MoveNext())
            {
                if (configData.Current.StartsWith("#")) //The setting this is looking for will not start with a # symbol
                    continue;

                dataFound = configData.Current.StartsWith(settingName); //This will likely be matching a setting name like this: cfgSetting
            }

            if (dataFound)
            {
                try
                {
                    Type dataType = typeof(T);
                    string rawData = configData.Current; //Formatted line containing the data
                    string dataFromString = rawData.Substring(rawData.LastIndexOf(' ') + 1);

                    //Parse the data into the specified data type
                    if (dataType == typeof(bool))
                        data = bool.Parse(dataFromString);
                    else if (dataType == typeof(int))
                        data = int.Parse(dataFromString);
                    else if (dataType == typeof(string))
                        data = dataFromString;
                    else
                        throw new NotSupportedException(dataType + " is not able to be converted");
                }
                catch (FormatException)
                {
                    Debug.LogError("Config setting is malformed, or not in the expected format");
                }
                catch (NotSupportedException ex)
                {
                    Debug.LogError(ex);
                }
            }
            return (T)data;
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
