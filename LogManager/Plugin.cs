using BepInEx;
using BepInEx.Logging;
using LogManager.Backup;
using LogManager.Helpers;
using LogManager.Interface;
using LogManager.Listeners;
using LogUtils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LogManager
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "fluffball.logmanager";
        public const string PLUGIN_NAME = "Log Manager";
        public const string PLUGIN_VERSION = "0.7.4";

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

        /// <summary>
        /// This is the primary method individual log files are moved
        /// </summary>
        public static LogFileSwitcher FileSwitcher;
        public static BackupController BackupManager;
        public static LoggerOptionInterface OptionInterface;

        public void Awake()
        {
            //Store path values that the mod uses
            int pluginDirIndex = ExecutingPath.LastIndexOf("plugin", StringComparison.InvariantCultureIgnoreCase);
            ModPath = Path.GetDirectoryName(ExecutingPath.Remove(pluginDirIndex, ExecutingPath.Length - pluginDirIndex));
            ConfigFilePath = Path.Combine(Application.persistentDataPath, "ModConfigs", PLUGIN_GUID + ".txt");

            try
            {
                LogManager.Config.Load();

                InitializeLogger();
                InitializeFileSwitcher();

                //This needs to be handled very early
                IL.RainWorld.HandleLog += RainWorld_HandleLog;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during initialization");
                Logger.LogError(ex);
            }
        }

        public void OnEnable()
        {
            try
            {
                On.RainWorld.OnModsInit += RainWorld_OnModsInit;
                On.RainWorld.Update += RainWorld_Update;
                On.Menu.ModdingMenu.Singal += ModdingMenu_Singal;
                On.Menu.Remix.MenuModList._ToggleMod += MenuModList_ToggleMod;
                On.Menu.ModdingMenu.ShutDownProcess += ModdingMenu_ShutDownProcess;
                IL.Expedition.ExpLog.Log += replaceLogPathHook_Expedition;
                IL.Expedition.ExpLog.LogOnce += replaceLogPathHook_Expedition;
                IL.Expedition.ExpLog.ClearLog += replaceLogPathHook_Expedition;
                IL.JollyCoop.JollyCustom.CreateJollyLog += replaceLogPathHook_JollyCoop;
                IL.JollyCoop.JollyCustom.Log += replaceLogPathHook_JollyCoop;
                IL.JollyCoop.JollyCustom.WriteToLog += replaceLogPathHook_JollyCoop;

                //Config processing hooks
                On.OptionInterface.ConfigHolder.Save += ConfigSaveHook;
                On.OptionInterface.ConfigHolder.Reload += ConfigLoadHook;
                hasInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while applying hooks");
                Logger.LogError(ex);
            }
        }

        private void ConfigSaveHook(On.OptionInterface.ConfigHolder.orig_Save orig, OptionInterface.ConfigHolder self)
        {
            if (hasInitialized && self.owner == OptionInterface)
            {
                Logger.LogInfo("Saving log manager config");
                LogManager.Config.SaveInProgress = true;
            }

            try
            {
                orig(self);
            }
            finally
            {
                if (LogManager.Config.SaveInProgress)
                {
                    LogManager.Config.HandleBackupEnabledChanges();
                    BackupManager.SaveListsToFile();
                }

                LogManager.Config.SaveInProgress = false;
            }
        }

        /// <summary>
        /// Ensures that the latest settings are retrieved from file
        /// </summary>
        private void ConfigLoadHook(On.OptionInterface.ConfigHolder.orig_Reload orig, OptionInterface.ConfigHolder self)
        {
            if (hasInitialized && self.owner == OptionInterface)
            {
                Logger.LogInfo("Loading log manager config");
                LogManager.Config.ReloadInProgress = true;

                try
                {
                    ManageExistingBackups();
                    LogManager.Config.HandleBackupEnabledChanges();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error occurred while handling backups");
                    Logger.LogError(ex);
                }
            }

            orig(self);

            if (LogManager.Config.ReloadInProgress && OptionInterface.HasInitialized)
                OptionInterface.ProcessBackupEnableOptions();

            LogManager.Config.ReloadInProgress = false;
        }

        private void ModdingMenu_Singal(On.Menu.ModdingMenu.orig_Singal orig, Menu.ModdingMenu self, Menu.MenuObject sender, string message)
        {
            if (message == "EXIT")
            {
                //Application is not shutting down. No cleanup required
                shouldCleanUpOnDisable = false;
            }
            else if (message == "APPLYMODS" && shouldCleanUpOnDisable)
            {
                //If shutdown hook doesn't work, signal here instead
                //Listener.CloseWriter();
                //Listener.Signal("MovePending");
            }

            orig(self, sender, message);
        }

        /*public const LogLevel SIGNAL_CODE = (LogLevel)612;

        public void CheckSignal(LogLevel level)
        {
            if (level == SIGNAL_CODE)
            {
                //This is a signal
            }
        }*/

        private void ModdingMenu_ShutDownProcess(On.Menu.ModdingMenu.orig_ShutDownProcess orig, Menu.ModdingMenu self)
        {
            Listener.CloseWriter();
            Listener.Signal("MovePending"); //No move will happen, but we need other mods to turn off their filestreams
            orig(self);
        }

        /// <summary>
        /// A flag that indicates whether log files should be disposed of on mod disable/game shutdown
        /// </summary>
        private bool shouldCleanUpOnDisable = false;
        private void MenuModList_ToggleMod(On.Menu.Remix.MenuModList.orig__ToggleMod orig, Menu.Remix.MenuModList self, Menu.Remix.MenuModList.ModButton btn)
        {
            orig(self, btn);

            if (btn.itf.mod.id == PLUGIN_GUID)
                shouldCleanUpOnDisable = !btn.selectEnabled;
        }

        private void RainWorld_HandleLog(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            //Replace all instances of exceptionLog.txt with a full path version
            int entriesToFind = 2;
            while (entriesToFind > 0 && cursor.TryGotoNext(MoveType.After, x => x.MatchLdstr("exceptionLog.txt")))
            {
                cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Ldstr, "exception.log");
                cursor.Emit(OpCodes.Ldc_I4_1); //Push a true value on the stack to satisfy second argument
                cursor.EmitDelegate(LogManager.Logger.ApplyLogPathToFilename);
                entriesToFind--;
            }

            if (entriesToFind > 0)
                Logger.LogError("IL hook couldn't find exceptionLog.txt");

            //Replace a single instance of consoleLog.txt with a full path version
            if (cursor.TryGotoNext(MoveType.After, x => x.MatchLdstr("consoleLog.txt")))
            {
                cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Ldstr, "console.log");
                cursor.Emit(OpCodes.Ldc_I4_1); //Push a true value on the stack to satisfy second argument
                cursor.EmitDelegate(LogManager.Logger.ApplyLogPathToFilename);
            }
            else
            {
                Logger.LogError("IL hook couldn't find consoleLog.txt");
            }
        }

        private void replaceLogPathHook_Expedition(ILContext il)
        {
            replaceLogPath(il, "expedition.log");
        }

        private void replaceLogPathHook_JollyCoop(ILContext il)
        {
            replaceLogPath(il, "jolly.log");
        }

        private void replaceLogPath(ILContext il, string newFilename)
        {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext(MoveType.After, x => x.MatchCall(typeof(RWCustom.Custom).GetMethod("RootFolderDirectory"))))
            {
                cursor.Emit(OpCodes.Pop); //Get method return value off the stack
                cursor.Emit(OpCodes.Ldsfld, typeof(Logger).GetField("BaseDirectory")); //Load new path onto stack
                cursor.GotoNext(MoveType.After, x => x.Match(OpCodes.Ldstr));
                cursor.Emit(OpCodes.Pop); //Replace filename extension with new one
                cursor.Emit(OpCodes.Ldstr, newFilename);
            }
            else
            {
                Logger.LogError("Expected directory IL could not be found");
            }
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

                string deletePath1 = LogManager.Logger.DefaultLogPath;
                string deletePath2 = LogManager.Logger.AlternativeLogPath;


                if (Path.GetFileName(deletePath1) == LogManager.Logger.LOGS_FOLDER_NAME)
                    FileSystemUtils.SafeDeleteDirectory(deletePath1, true);

                if (Path.GetFileName(deletePath2) == LogManager.Logger.LOGS_FOLDER_NAME)
                    FileSystemUtils.SafeDeleteDirectory(deletePath2, true);
            }

            unloadHooks();
            hasInitialized = false;
        }

        private void unloadHooks()
        {
            IL.RainWorld.HandleLog -= RainWorld_HandleLog;
            On.RainWorld.OnModsInit -= RainWorld_OnModsInit;
            On.RainWorld.Update -= RainWorld_Update;
            On.Menu.ModdingMenu.Singal -= ModdingMenu_Singal;
            On.Menu.Remix.MenuModList._ToggleMod -= MenuModList_ToggleMod;
            On.Menu.ModdingMenu.ShutDownProcess -= ModdingMenu_ShutDownProcess;
            IL.Expedition.ExpLog.Log -= replaceLogPathHook_Expedition;
            IL.Expedition.ExpLog.LogOnce -= replaceLogPathHook_Expedition;
            IL.Expedition.ExpLog.ClearLog -= replaceLogPathHook_Expedition;
            IL.JollyCoop.JollyCustom.CreateJollyLog -= replaceLogPathHook_JollyCoop;
            IL.JollyCoop.JollyCustom.Log -= replaceLogPathHook_JollyCoop;
            IL.JollyCoop.JollyCustom.WriteToLog -= replaceLogPathHook_JollyCoop;

            //Config processing hooks
            On.OptionInterface.ConfigHolder.Save -= ConfigSaveHook;
            On.OptionInterface.ConfigHolder.Reload -= ConfigLoadHook;
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            try
            {
                if (OptionInterface == null)
                {
                    OptionInterface = new LoggerOptionInterface();
                    LogManager.Config.Initialize();
                }

                MachineConnector.SetRegisteredOI(PLUGIN_GUID, OptionInterface);
            }
            catch (Exception ex)
            {
                Logger.LogError("Config did not initialize properly");
                Logger.LogError(ex);
            }
        }

        public void InitializeLogger()
        {
            Logger = base.Logger;

            LogManager.Logger.InitializeLogDirectory();

            ManageExistingLogs();

            //This code must be after log directory is established, and before listener is created, or data copy will fail
            transferBepInExLogData();

            Listener = new CustomizableDiskLogListener(LogManager.Logger.BaseDirectory, LogManager.Logger.OUTPUT_NAME, false);
            BepInEx.Logging.Logger.Listeners.Add(Listener);
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
            FileInfo BepInExLogFile = new FileInfo(Path.Combine(Paths.BepInExRootPath, "LogOutput.log"));

            if (BepInExLogFile.Exists)
            {
                string destPath = LogManager.Logger.ApplyLogPathToFilename(LogManager.Logger.OUTPUT_NAME);

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

        public void InitializeFileSwitcher()
        {
            FileSwitcher = new LogFileSwitcher(LogFileSwitcher.PathSwitchMode.Collective)
            {
                SwitchStartPosition = false
            };

            string[] logPathsOrig, logPathsCurrent;

            logPathsOrig = getStreamingAssetsLogPathsOrig();
            logPathsCurrent = getStreamingAssetsLogPathsCurrent();

            string expLogOrig = logPathsOrig[0];
            string expLogCurrent = logPathsCurrent[0];
            string jollyLogOrig = logPathsOrig[1];
            string jollyLogCurrent = logPathsCurrent[1];

            logPathsOrig = getRainWorldRootLogPathsOrig();
            logPathsCurrent = getRainWorldRootLogPathsCurrent();

            string consoleLogOrig = logPathsOrig[0];
            string consoleLogCurrent = logPathsCurrent[0];
            string exceptionLogOrig = logPathsOrig[1];
            string exceptionLogCurrent = logPathsCurrent[1];

            FileSwitcher.AddPaths(expLogOrig, expLogCurrent);
            FileSwitcher.AddPaths(jollyLogOrig, jollyLogCurrent);
            FileSwitcher.AddPaths(consoleLogOrig, consoleLogCurrent);
            FileSwitcher.AddPaths(exceptionLogOrig, exceptionLogCurrent);
        }

        /// <summary>
        /// Retrieves standard log paths for logs that write to Rain World Streaming Assets folder as an array
        /// </summary>
        private static string[] getStreamingAssetsLogPathsOrig()
        {
            string rootDir = Application.streamingAssetsPath;

            string expLog = Path.Combine(rootDir, "ExpLog.txt");
            string jollyLog = Path.Combine(rootDir, "jollyLog.txt");

            return new string[]
            {
                expLog,
                jollyLog,
            };
        }

        /// <summary>
        /// Retrieves expected log paths for logs that write to Rain World Streaming Assets folder with LogManager active as an array
        /// </summary>
        private static string[] getStreamingAssetsLogPathsCurrent()
        {
            string rootDir = LogManager.Logger.FindExistingLogsDirectory();

            string expLog = Path.Combine(rootDir, "expedition.log");
            string jollyLog = Path.Combine(rootDir, "jolly.log");

            return new string[]
            {
                expLog,
                jollyLog,
            };
        }

        /// <summary>
        /// Retrieves standard log paths for logs that write to Rain World root folder as an array
        /// </summary>
        private static string[] getRainWorldRootLogPathsOrig()
        {
            string rootDir = Path.GetDirectoryName(Application.dataPath);

            string consoleLog = Path.Combine(rootDir, "consoleLog.txt");
            string exceptionLog = Path.Combine(rootDir, "exceptionLog.txt");

            return new string[]
            {
                consoleLog,
                exceptionLog,
            };
        }

        /// <summary>
        /// Retrieves expected log paths for logs that write to Rain World root folder with LogManager active as an array
        /// </summary>
        private static string[] getRainWorldRootLogPathsCurrent()
        {
            string rootDir = LogManager.Logger.FindExistingLogsDirectory();

            string consoleLog = Path.Combine(rootDir, "console.log");
            string exceptionLog = Path.Combine(rootDir, "exception.log");

            return new string[]
            {
                consoleLog,
                exceptionLog,
            };
        }

        /// <summary>
        /// Handles existing logs on startup
        /// </summary>
        public void ManageExistingLogs()
        {
            string existingLogsDirectory = LogManager.Logger.BaseDirectory;

            BackupManager = new BackupController(existingLogsDirectory, "Backup");

            ManageExistingBackups();
            DeleteExistingLogs();
        }

        /// <summary>
        /// Manages the process of clearing existing backup entry lists, and then updating them with new backup information from file
        /// </summary>
        public void ManageExistingBackups()
        {
            BackupManager.Enabled = LogManager.Config.GetValue(nameof(LogManager.Config.cfgAllowBackups), false);
            BackupManager.ProgressiveEnableMode = LogManager.Config.GetValue(nameof(LogManager.Config.cfgAllowProgressiveBackups), false);
            BackupManager.AllowedBackupsPerFile = LogManager.Config.GetValue(nameof(LogManager.Config.cfgBackupsPerFile), 2);

            BackupManager.PopulateLists();

            string targetPath = LogManager.Logger.BaseDirectory;

            //The first time BackupManager is run, we either store a copy of the backups, or not. Internally
            //BackupInFolder() invokes ProcessFolder() either way. It is required in order for the Remix menu
            //to know which enable options to show. That process requires the Logs directory to be analyzed
            //whether or not Backups are enabled
            if (!BackupManager.HasRunOnce)
                BackupManager.BackupFromFolder(targetPath);
            else
                BackupManager.ProcessFolder(targetPath, false);
        }

        /// <summary>
        /// Preexisting logs are deleted each, and every startup
        /// </summary>
        internal void DeleteExistingLogs()
        {
            DateTime deleteBeforeTime = File.GetLastAccessTime(ExecutingPath);

            string[] logPaths = getStreamingAssetsLogPathsOrig();

            string existingExpLog = logPaths[0];
            string existingJollyLog = logPaths[1];

            string deleteFailureMsg = "Unable to delete existing log";

            //Clear old existing logs. Neither should contain any information not logged to the new directory
            FileSystemUtils.SafeDeleteFile(existingExpLog, deleteFailureMsg);
            FileSystemUtils.SafeDeleteFile(existingJollyLog, deleteFailureMsg);

            string existingLogsDirectory = LogManager.Logger.FindExistingLogsDirectory();

            if (existingLogsDirectory != null)
            {
                //This code targets files that existed before game was launched
                foreach (string logFile in Directory.GetFiles(existingLogsDirectory))
                {
                    try
                    {
                        if (File.GetLastAccessTime(logFile).CompareTo(deleteBeforeTime) < 0)
                            File.Delete(logFile);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Unable to delete log " + logFile);
                        Logger.LogError(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Takes logs managed by LogManager and move them back to their original locations with their original names
        /// </summary>
        public void RestoreManagedLogs()
        {
            string logPath = LogManager.Logger.BaseDirectory;

            if (Directory.Exists(logPath))
            {
                FileSwitcher.SwitchPaths();

                /*string rootPath = Application.streamingAssetsPath;

                //StreamingAssets logs
                string expLog = Path.Combine(logPath, "expedition.log");
                string jollyLog = Path.Combine(logPath, "jolly.log");

                //Rain World root logs
                string consoleLog = Path.Combine(logPath, "console.log");
                string exceptionLog = Path.Combine(logPath, "exception.log");

                LogManager.Logger.MoveLog(expLog, Path.Combine(rootPath, "ExpLog.txt"));
                LogManager.Logger.MoveLog(jollyLog, Path.Combine(rootPath, "jollyLog.txt"));

                rootPath = Path.GetDirectoryName(Application.dataPath);

                LogManager.Logger.MoveLog(consoleLog, Path.Combine(rootPath, "consoleLog.txt"));
                LogManager.Logger.MoveLog(exceptionLog, Path.Combine(rootPath, "exceptionLog.txt"));*/
            }
        }

        private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
        {
            if (PendingMoveUpdate)
            {
                if (moveAttempts < moveAttemptsAllowed)
                {
                    bool attemptCopyMethod = moveAttempts >= 5;

                    if (tryChangeDirectory(attemptCopyMethod))
                    {
                        Logger.LogInfo($"Move completed in {moveAttempts} attempts");

                        moveAttempts = 0;

                        LogManager.Logger.BaseDirectory = PendingLogPath;//Path.GetDirectoryName(Listener.LogFullPath);
                        FileSwitcher.UpdateTogglePath(PendingLogPath);

                        Listener.Signal("MoveComplete", PendingLogPath);
                        PendingLogPath = null;
                    }
                    else
                    {
                        moveAttempts++;
                    }
                }
                else //Nothing can be logged while a move is pending
                {
                    Logger.LogInfo("Move attempt failed");

                    Logger.LogInfo("Default Directory exists: " + Directory.Exists(LogManager.Logger.DefaultLogPath));
                    Logger.LogInfo("Alternative Directory exists: " + Directory.Exists(LogManager.Logger.AlternativeLogPath));
                    Logger.LogInfo("Current Directory exists: " + Directory.Exists(Listener.LogFullPath));

                    Listener.OpenFileStream(false);
                    Listener.Signal("MoveAborted");

                    moveAttempts = 0;
                    PendingLogPath = null;
                }
            }
            else if (Listener.GetSignal() != "Signal.None")
            {
                Listener.Signal("None");
            }

            if (PendingDeleteUpdate) //Delete is already attempted during a move
                ensureSingleLogsFolder();

            //ensureLogsFolderExists();
            orig(self);
        }

        private static void ensureSingleLogsFolder()
        {
            if (Listener.LogFullPath == null) return; //Something happened while handling Logs directory.

            string defaultLogPath = LogManager.Logger.DefaultLogPath;
            string alternativeLogPath = LogManager.Logger.AlternativeLogPath;

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

        private static void ensureLogsFolderExists()
        {
            return;
            string baseDirectory = LogManager.Logger.BaseDirectory;

            string path = Path.HasExtension(baseDirectory) ? Path.GetDirectoryName(baseDirectory) : baseDirectory;

            if (Directory.Exists(path)) return;

            Plugin.Logger.LogWarning("Logs folder doesn't exist. Creating folder");
            Directory.CreateDirectory(path);
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

            //Check if paths are the same
            if (LogPath.ComparePaths(currentBasePath, pendingBasePath))
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
            string defaultLogPath = LogManager.Logger.DefaultLogPath;
            string alternativeLogPath = LogManager.Logger.AlternativeLogPath;

            return LogManager.Config.cfgUseAlternativeDirectory.Value ? alternativeLogPath : defaultLogPath;
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
