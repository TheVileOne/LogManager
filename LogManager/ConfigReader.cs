using LogManager.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using UnityEngine;

namespace LogManager
{
    public static class ConfigReader
    {
        public static StringDictionary ReadFile(string configPath)
        {
            if (!File.Exists(configPath))
                return new StringDictionary();

            StringDictionary configEntries = new StringDictionary();

            IEnumerator<string> configData = File.ReadLines(Plugin.ConfigFilePath).GetEnumerator();

            while (configData.MoveNext())
            {
                string entry = configData.Current.Trim();

                if (entry.StartsWith("#") || entry == string.Empty) //The setting this is looking for will not start with a # symbol
                    continue;

                int sepIndex = entry.IndexOf('=');

                if (sepIndex != -1)
                {
                    string entryKey = entry.Substring(0, sepIndex).Trim();
                    string entryValue = entry.Substring(sepIndex + 1).Trim();

                    configEntries.Add(entryKey, entryValue);
                }
            }

            return configEntries;
        }

        /// <summary>
        /// Searches the mod config file for a config setting and returns the stored setting value if one exists, expectedDefault otherwise
        /// </summary>
        public static T ReadFromDisk<T>(string configPath, string settingName, T expectedDefault) where T : IConvertible
        {
            IEnumerator<string> configData = File.ReadLines(configPath).GetEnumerator();

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
                    string rawData = configData.Current; //Formatted line containing the data
                    string dataFromString = rawData.Substring(rawData.LastIndexOf(' ') + 1);

                    //Parse the data into the specified data type
                    data = dataFromString.ConvertParse<T>();
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
    }
}
