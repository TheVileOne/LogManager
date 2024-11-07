using BepInEx;
using BepInEx.Logging;
using LogManager.Components;
using LogManager.Interface;
using LogManager.Listeners;
using LogManager.Settings;
using LogUtils;
using LogUtils.Helpers.FileHandling;
using System;
using System.Collections;
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

        /// <summary>
        /// This is used by BepInEx to log messages it receives to file
        /// </summary>
        public static CustomizableDiskLogListener Listener;

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

            //This code deletes the log directory on shutdown. It needs to only delete it if mod is disabled through options
            if (shouldCleanUpOnDisable)
            {
                Listener.Signal("None");
                Listener.Dispose();

                BepInEx.Logging.Logger.Listeners.Remove(Listener);

                RestoreManagedLogs();

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
            return;

            //This code must be after log directory is established, and before listener is created, or data copy will fail
            transferBepInExLogData();

            Listener = new CustomizableDiskLogListener(LogsFolder.Path, UtilityConsts.LogNames.BepInEx, false);
            BepInEx.Logging.Logger.Listeners.Add(Listener);
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

        /// <summary>
        /// Copies messages that were logged to LogOutput.log before LogManager was able to initialize to new log file
        /// </summary>
        private void transferBepInExLogData()
        {
            //Find the listener responsible for logging to LogOutput.log
            DiskLogListener BepInExListener = null;
            IEnumerator enumerator = BepInEx.Logging.Logger.Listeners.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if ((BepInExListener = enumerator.Current as DiskLogListener) != null)
                    break;
            }

            //Write messages from the buffer to file. Without this, an empty log will be copied
            BepInExListener?.LogWriter.Flush();

            //Handle file copy actions
            FileInfo BepInExLogFile = new FileInfo(Path.Combine(BepInEx.Paths.BepInExRootPath, UtilityConsts.LogNames.BepInEx + ".log"));

            if (BepInExLogFile.Exists)
            {
                string destPath = LogManager.Logger.ApplyLogPathToFilename(UtilityConsts.LogNames.BepInEx);

                try
                {
                    File.Delete(destPath);
                    BepInExLogFile.CopyTo(destPath);

                    if (!File.Exists(destPath))
                        throw new FileNotFoundException();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Unable to copy BepInEx log");
                    Logger.LogError(ex);
                }
            }
        }

        /// <summary>
        /// Takes logs managed by LogManager and move them back to their original locations with their original names
        /// </summary>
        public void RestoreManagedLogs()
        {
            string logPath = LogsFolder.Path;

            if (Directory.Exists(logPath))
            {
                //TODO: Implement
            }
        }

        private static void ensureSingleLogsFolder()
        {
            if (Listener.LogFullPath == null) return; //Something happened while handling Logs directory.

            string defaultLogPath = LogsFolder.DefaultPath;
            string alternativeLogPath = LogsFolder.AlternativePath;

            if (Listener.LogFullPath == alternativeLogPath)
                alternativeLogPath = defaultLogPath;

            try
            {
                if (Directory.Exists(Listener.LogFullPath) && Directory.Exists(alternativeLogPath))
                {
                    Logger.LogInfo("More than one Logs folder exists. Removing one");
                    Directory.Delete(alternativeLogPath, true);
                }

                PendingDeleteUpdate = false;
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to delete unused Logs directory");
                Logger.LogError(ex);
            }
        }

        public static int moveAttempts = 0;
        public static int moveAttemptsAllowed = 20;

        /// <summary>
        /// A path that will become the current logging path if a move attempt is successful
        /// </summary>
        public static string PendingLogPath = null;

        /// <summary>
        /// Tells the game to attempt to change the logging path on RainWorld.Update
        /// </summary>
        public static bool PendingMoveUpdate => PendingLogPath != null;

        /// <summary>
        /// A flag that runs after a successful move update to make sure that folders aren't left behind
        /// </summary>
        public static bool PendingDeleteUpdate;

        public static void UpdateLogDirectory()
        {
            if (PendingMoveUpdate) return;

            Logger.LogInfo("Updating log directory");

            //ensureLogsFolderExists();

            string currentBasePath = Path.GetDirectoryName(Listener.LogFullPath); //= LogManager.Logger.BaseDirectory;
            string pendingBasePath = getLogPathFromConfig();

            logDirectoryExistence(currentBasePath, pendingBasePath);

            if (PathUtils.PathsAreEqual(currentBasePath, pendingBasePath))
            {
                Directory.CreateDirectory(pendingBasePath);
                Logger.LogInfo("Path hasn't changed");
                return;
            }

            //Make sure an existing Logs directory isn't deleted because it was unable to be move
            //This code only logs now, because Directory.Exists is returning false even though the directory path seems to exist for some reason
            if (!Directory.Exists(currentBasePath))
            {
                Logger.LogInfo("No directory to move");
                //Directory.CreateDirectory(pendingBasePath);
                //return;
            }

            //Logger.LogInfo("Path is dirty");

            Listener.Signal("MovePending");

            //Tell RainWorld.Update the logging path needs to change
            //Note: The move is not handled here due to problems with the move being handled directly after the LogWriter is disposed.
            // It needs at least an extra frame to release resources.
            PendingLogPath = pendingBasePath;

            //In order to change directory, all FileStreams attached to log files need to be closed.
            Listener.CloseWriter();
        }

        private bool tryChangeDirectory(bool tryCopy)
        {
            try
            {
                if (tryCopy)
                    Listener.ChangeDirectoryCopy(PendingLogPath);
                else
                    Listener.ChangeDirectory(PendingLogPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to change log directory");
                Logger.LogError(ex);
                return false;
            }

            PendingDeleteUpdate = true;
            return true;
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
