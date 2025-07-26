using BepInEx;
using BepInEx.Logging;
using LogManager.Controllers;
using LogManager.Interface;
using LogManager.Settings;
using LogUtils;
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

        public static LogsFolderController FolderController;
        public static BackupController BackupController;

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
                Initialize();
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

            //This code deletes the log directory on shutdown. It needs to only delete it if mod is disabled through options
            if (shouldCleanUpOnDisable)
                LogsFolder.RestoreFiles();

            RemoveHooks();
            hasInitialized = false;
        }

        public void Initialize()
        {
            Logger = base.Logger;
            FolderController = new LogsFolderController();
            BackupController = new BackupController(); 

            Logger.LogInfo("LogManager initialized");

            RefreshBackupController();

            if (!LogsFolder.Exists)
            {
            retry:
                try
                {
                    LogsFolder.Create();
                    LogsFolder.MoveFilesToFolder();
                }
                catch (DirectoryNotFoundException)
                {
                    //Current log directory is invalid - change it to a valid default path instead
                    LogsFolder.SetContainingPath(LogsFolder.AvailablePaths[0]);
                    goto retry;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Log directory failed to initialize");
                    Logger.LogError(ex);
                }
            }
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
            Logger.LogInfo("Refreshing settings");

            BackupController.Enabled = ConfigSettings.GetValue(nameof(ConfigSettings.cfgAllowBackups), false);
            BackupController.ProgressiveEnableMode = ConfigSettings.GetValue(nameof(ConfigSettings.cfgAllowProgressiveBackups), false);
            BackupController.AllowedBackupsPerFile = ConfigSettings.GetValue(nameof(ConfigSettings.cfgBackupsPerFile), BackupController.ALLOWED_BACKUPS_PER_FILE);

            Logger.LogInfo(string.Format("Backup system {0}", BackupController.Enabled ? "enabled" : "disabled"));
        }

        public static void RefreshBackupEntries()
        {
            BackupController.PopulateLists();
            BackupController.ProcessNewEntries();
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

            FolderController.RequestPathChange(pendingBasePath);
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
