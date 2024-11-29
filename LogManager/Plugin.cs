using BepInEx;
using BepInEx.Logging;
using LogManager.Components;
using LogManager.Interface;
using LogManager.Settings;
using LogUtils;
using LogUtils.Helpers.FileHandling;
using System;
using System.IO;
using UnityEngine;

namespace LogManager
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "fluffball.logmanager";
        public const string PLUGIN_NAME = "Log Manager";
        public const string PLUGIN_VERSION = "0.8.0";

        private bool hasInitialized;

        public static new ManualLogSource Logger;

        /// <summary>
        /// The path of the mod's executing DLL file
        /// </summary>
        public static string ExecutingPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

        /// <summary>
        /// The path that contains and includes this mod's folder
        /// </summary>
        public static string ModPath;

        /// <summary>
        /// The path that contains the mod-specific config settings managed by Remix menu
        /// </summary>
        public static string ConfigFilePath;

        public static Components.LogManager LogManager;
        public static BackupController BackupManager => LogManager.BackupManager;

        public static LoggerOptionInterface OptionInterface;

        public void Awake()
        {
            //Store path values that the mod uses
            int pluginDirIndex = ExecutingPath.LastIndexOf("plugin", StringComparison.InvariantCultureIgnoreCase);
            ModPath = Path.GetDirectoryName(ExecutingPath.Remove(pluginDirIndex, ExecutingPath.Length - pluginDirIndex));
            ConfigFilePath = Path.Combine(Application.persistentDataPath, "ModConfigs", PLUGIN_GUID + ".txt");

            try
            {
                ConfigSettings.Load();

                InitializeLogManager();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during initialization");
                Logger.LogError(ex);
            }
        }

        public void OnEnable()
        {
            if (hasInitialized) return;

            ApplyHooks();
            hasInitialized = true;
        }

        public void OnDisable()
        {
            if (!hasInitialized) return;

            /*
             * TODO: Code is bad - don't run
             */

            //This code deletes the log directory on shutdown. It needs to only delete it if mod is disabled through options
            if (shouldCleanUpOnDisable)
            {
                //TODO: Implement
                if (LogsFolder.IsLogsFolderPath(LogsFolder.Path))
                    LogsFolder.Restore();

                string deletePath1 = LogsFolder.DefaultPath;
                string deletePath2 = LogsFolder.AlternativePath;

                if (Path.GetFileName(deletePath1) == LogsFolder.LOGS_FOLDER_NAME)
                    DirectoryUtils.SafeDelete(deletePath1, true);

                if (Path.GetFileName(deletePath2) == LogsFolder.LOGS_FOLDER_NAME)
                    DirectoryUtils.SafeDelete(deletePath2, true);
            }

            RemoveHooks();
            hasInitialized = false;
        }

        public void InitializeLogManager()
        {
            Logger = base.Logger;
            LogManager = new Components.LogManager();

            RefreshBackupController();
            LogManager.ProcessFiles();
        }

        /// <summary>
        /// Manages the process of clearing existing backup entry lists, and then updating them with new backup information from file
        /// </summary>
        public static void RefreshBackupController()
        {
            RefreshBackupSettings();
            RefreshBackupEntries();
        }

        public static void RefreshBackupSettings()
        {
            BackupManager.Enabled = ConfigSettings.GetValue(nameof(ConfigSettings.cfgAllowBackups), false);
            BackupManager.ProgressiveEnableMode = ConfigSettings.GetValue(nameof(ConfigSettings.cfgAllowProgressiveBackups), false);
            BackupManager.AllowedBackupsPerFile = ConfigSettings.GetValue(nameof(ConfigSettings.cfgBackupsPerFile), 2);
        }

        public static void RefreshBackupEntries()
        {
            BackupManager.PopulateLists();
            BackupManager.ProcessNewEntries();
        }

        private static void ensureSingleLogsFolder()
        {
            if (LogsFolder.Path == null) return; //Something happened while handling Logs directory.

            string defaultLogPath = LogsFolder.DefaultPath;
            string alternativeLogPath = LogsFolder.AlternativePath;

            if (LogsFolder.Path == alternativeLogPath)
                alternativeLogPath = defaultLogPath;

            try
            {
                if (Directory.Exists(LogsFolder.Path) && Directory.Exists(alternativeLogPath))
                {
                    Logger.LogInfo("More than one Logs folder exists. Removing one");
                    Directory.Delete(alternativeLogPath, true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to delete unused Logs directory");
                Logger.LogError(ex);
            }
        }

        public static void UpdateLogDirectory()
        {
            Logger.LogInfo("Updating log directory");

            string currentBasePath = LogsFolder.Path;
            string pendingBasePath = getLogPathFromConfig();

            logDirectoryExistence(currentBasePath, pendingBasePath);

            LogManager.RequestPathChange(pendingBasePath);
        }

        private static string getLogPathFromConfig()
        {
            string defaultLogPath = LogsFolder.DefaultPath;
            string alternativeLogPath = LogsFolder.AlternativePath;

            return ConfigSettings.cfgUseAlternativeDirectory.Value ? alternativeLogPath : defaultLogPath;
        }

        /// <summary>
        /// Log some debug info. Not very important
        /// </summary>
        private static void logDirectoryExistence(string currentPath, string pendingPath)
        {
            if (!Directory.Exists(currentPath) && !Directory.Exists(pendingPath))
            {
                Logger.LogWarning("No directory exists");
            }
            else
            {
                Logger.LogInfo("Current Directory exists: " + Directory.Exists(currentPath));
                Logger.LogInfo("Pending Directory exists: " + Directory.Exists(pendingPath));
            }
        }
    }
}
