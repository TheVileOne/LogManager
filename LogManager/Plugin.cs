using BepInEx;
using LogManager.Controllers;
using LogManager.Interface;
using LogManager.Settings;
using LogUtils;
using System;
using System.IO;
using UnityEngine;
using Logger = LogUtils.Logger;

namespace LogManager
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "fluffball.logmanager";
        public const string PLUGIN_NAME = "Log Manager";
        public const string PLUGIN_VERSION = "0.9.0";

        private bool hasInitialized;

        public static new Logger Logger;

        /// <summary>
        /// The path that contains the mod-specific config settings managed by Remix menu
        /// </summary>
        public static string ConfigFilePath;

        /// <summary>
        /// The root path for Rain World
        /// </summary>
        public static string GameRootPath = Paths.GameRootPath;

        /// <summary>
        /// The path that contains and includes this mod's root folder
        /// </summary>
        public static string ModPath;

        public static BackupController BackupController;

        public static LoggerOptionInterface OptionInterface;

        public void Awake()
        {
            string executingPath = Path.GetDirectoryName(Info.Location);
            ModPath = BepInEx.MultiFolderLoader.ModManager.Mods.Find(mod => mod.PluginsPath == executingPath).ModDir;
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
            Logger = new Logger(base.Logger);
            BackupController = new BackupController(() => LogsFolder.CurrentPath);

            Logger.LogInfo("LogManager initialized");

            PopulateLogsFolder();
            RefreshBackupController();
        }

        /// <summary>
        /// Attempts to create directory for storing log files and moves eligible files to the directory
        /// </summary>
        public static void PopulateLogsFolder()
        {
            if (LogsFolder.Exists)
            {
                LogsFolder.MoveFilesToFolder();
                return;
            }

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
    }
}
