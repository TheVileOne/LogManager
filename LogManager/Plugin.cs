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
        public const string PLUGIN_VERSION = "0.8.0";

        private bool hasInitialized;

        public static new Logger Logger;

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
            Logger = new Logger(base.Logger);
            BackupController = new BackupController(() => LogsFolder.CurrentPath);

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
    }
}
