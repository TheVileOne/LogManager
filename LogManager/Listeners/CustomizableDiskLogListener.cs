using BepInEx;
using BepInEx.Logging;
using LogUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Debug = UnityEngine.Debug;
using LogManager.Helpers;
using LogUtils.Helpers;

namespace LogManager.Listeners
{
    public class CustomizableDiskLogListener : ILogListener
    {
        /// <summary>
        /// Filters which LogLevels are allowed to be logged
        /// </summary>
        public LogLevel EnabledLogLevels { get; set; }

        public TextWriter LogWriter { get; protected set; }

        public Timer FlushTimer { get; protected set; }

        /// <summary>
        /// The full path to the base log file (including the filename)
        /// </summary>
        public string LogFullPath;

        /// <summary>
        /// The name of the log file without file extension
        /// </summary>
        public string LogName { get; private set; }

        /// <summary>
        /// A string returned by ToString used to signal loggers from other mods
        /// </summary>
        private string signalString = "Signal.None";

        /// <summary>
        /// Instantiates an ILogListener that can be recognized by Unity/BepInEx
        /// </summary>
        /// <param name="rootPath">The path to, or including the Logs folder</param>
        /// <param name="localPathToLogFile">A specific path to the primary log file. Use a filename without an extension. It can have path data included.</param>
        /// <param name="overwrite">A flag that allows writing over existing data</param>
        /// <param name="enabledLogLevels">A flag filter that only displays messages with the specified LogLevel</param>
        public CustomizableDiskLogListener(string rootPath, string localPathToLogFile, bool overwrite = true, LogLevel enabledLogLevels = LogLevel.All)
        {
            EnabledLogLevels = enabledLogLevels;

            handlePathArgs(rootPath, localPathToLogFile);
            OpenFileStream(overwrite);
        }

        private void handlePathArgs(string rootPath, string localPathToLogFile)
        {
            //We still need to make sure this path contains a proper Logs directory
            string pathToCheck = Path.Combine(rootPath, Logger.FormatLogFile(localPathToLogFile));

            LogFullPath = ensurePathContainsLogsFolder(pathToCheck);
            LogName = Path.GetFileNameWithoutExtension(LogFullPath);

            try
            {
                ensureFolderStructureExists(LogFullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("There was a problem constructing log directory");
                Debug.LogError(ex);

                LogFullPath = null;
            }
        }

        private string ensurePathContainsLogsFolder(string path)
        {
            string filename = Path.GetFileName(path);
            string directoryWithoutFilename, directoryName;

            bool hasFilename = Path.HasExtension(filename);

            if (hasFilename)
            {
                directoryWithoutFilename = Path.GetDirectoryName(path);
                directoryName = new DirectoryInfo(directoryWithoutFilename).Name;
            }
            else
            {
                directoryWithoutFilename = path;
                directoryName = filename; //Path.GetFileName returns anything after the last DirectorySeparatorChar

                filename = string.Empty; //This would interfere with Path.Combine
            }

            return directoryName != LogsFolder.LOGS_FOLDER_NAME ?
                Path.Combine(directoryWithoutFilename, LogsFolder.LOGS_FOLDER_NAME, filename) : path;
        }

        private void ensureFolderStructureExists(string path)
        {
            if (!Directory.Exists(path = Path.GetDirectoryName(path)))
            {
                ensureFolderStructureExists(path);
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Opens a threadsafe filestream for writing messages
        /// </summary>
        /// <param name="appendLog">Whether the file should be appended, or created</param>
        public virtual void OpenFileStream(bool overwrite)
        {
            if (LogFullPath == null)
            {
                Debug.LogError("Couldn't open a log file for writing. Skipping log file creation");
                return;
            }

            //The last LogWriter needs to be replaced
            CloseWriter();

            //string backupPath = LogFullPath;
            //bool hasBackup = FileSystemUtils.BackupFile(backupPath);

            FileMode fileMode = overwrite || !File.Exists(LogFullPath) ? FileMode.Create : FileMode.Append;

            string logPath = Path.GetDirectoryName(LogFullPath); //Strip the filename

            bool backupRequired = false;

            int num = 1;
            const int max_open_attempts = 5;
            FileStream fileStream;
            while (!Utility.TryOpenFileStream(LogFullPath, fileMode, out fileStream, FileAccess.Write))
            {
                if (num == max_open_attempts)
                {
                    //if (!hasBackup)
                    {
                        Debug.LogError("Couldn't open a log file for writing. Skipping log file creation");
                        return;
                    }

                    backupRequired = true;
                    break;
                }

                Debug.LogWarning("Couldn't open log file '" + LogName + "' for writing, trying another...");
                string logName = $"{LogName}.log.{num++}";
                LogFullPath = Path.Combine(logPath, logName);
            }

            if (!backupRequired)
            {
                LogWriter = TextWriter.Synchronized(new StreamWriter(fileStream, Utility.UTF8NoBom));
                FlushTimer = new Timer(delegate
                {
                    LogWriter?.Flush();
                }, null, 2000, 2000);

                /*
                if (hasBackup)
                {
                    bool fileStreamChangedFromBackup = FileSystemUtils.CompareFileToFileStream(backupPath, fileStream);
                    backupRequired = fileStreamChangedFromBackup;
                }
                */
            }

            if (backupRequired)
            {
                /*
                CloseWriter();

                //If backup fails, fallback to TextWriter method that worked
                if (num < max_open_attempts && !FileSystemUtils.RestoreBackup(backupPath))
                {
                    LogWriter = TextWriter.Synchronized(new StreamWriter(fileStream, Utility.UTF8NoBom));
                    FlushTimer = new Timer(delegate
                    {
                        LogWriter?.Flush();
                    }, null, 2000, 2000);
                }
                */
            }
        }

        public void ChangeDirectory(string rootPath)
        {
            if (LogFullPath != null)
            {
                string currentPath = Path.GetDirectoryName(LogFullPath); //Strips the log filename from the path

                if (PathUtils.PathsAreEqual(currentPath, rootPath))
                {
                    ensureFolderStructureExists(LogFullPath); //Check just in case something happened to the directory
                    return;
                }
            }

            Plugin.Logger.LogInfo("Logging path change pending");

            string oldLogPath = LogFullPath;

            moveExistingDirectory(rootPath);

            if (!PathUtils.PathsAreEqual(LogFullPath, oldLogPath)) //Only open a new filestream if move was successful
            {
                OpenFileStream(false);
                Plugin.Logger.LogInfo("Logging path change complete");
            }
            else
                Plugin.Logger.LogInfo("Logging path couldn't be changed");
        }

        public void ChangeDirectoryCopy(string rootPath)
        {
            if (LogFullPath != null)
            {
                string currentPath = Path.GetDirectoryName(LogFullPath); //Strips the log filename from the path

                if (PathUtils.PathsAreEqual(currentPath, rootPath))
                {
                    ensureFolderStructureExists(LogFullPath); //Check just in case something happened to the directory
                    return;
                }
            }

            Plugin.Logger.LogInfo("Logging copy pending");

            string oldLogPath = LogFullPath;

            copyExistingDirectory(rootPath);

            if (!PathUtils.PathsAreEqual(LogFullPath, oldLogPath)) //Only open a new filestream if move was successful
            {
                OpenFileStream(false);
                Plugin.Logger.LogInfo("Logging path change complete");
            }
            else
                Plugin.Logger.LogInfo("Logging path couldn't be changed");
        }

        public void CloseWriter()
        {
            if (LogWriter != null)
            {
                LogWriter.Flush(); //Finish writing any messages in the stream
                LogWriter.Close();
                LogWriter = null;
            }
        }

        private void moveExistingDirectory(string destPath)
        {
            try
            {
                //Dest path must contain the exact folder destination, and not be a parent directory.
                //This ensures the Logs folder is only moved to a validly named folder.
                destPath = ensurePathContainsLogsFolder(destPath);

                //This does not include path information beyond the last directory separator character.
                //In other words, it will not attempt to creates the Logs directory.
                ensureFolderStructureExists(destPath);

                //Directory move operation will fail if a directory already exists at the destination path
                if (Directory.Exists(destPath))
                {
                    Plugin.Logger.LogInfo("Logging folder already exists at destination. Replacing folder");
                    Directory.Delete(destPath, true);
                }

                if (LogFullPath != null)
                    Directory.Move(Path.GetDirectoryName(LogFullPath), destPath);

                validateFolderStructure(destPath);

                LogFullPath = Path.Combine(destPath, Logger.FormatLogFile(LogName));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Unable to move log directory");
                Plugin.Logger.LogError(ex);
            }

            //OpenFileStream(true);
        }

        private void copyExistingDirectory(string destPath)
        {
            try
            {
                //Dest path must contain the exact folder destination, and not be a parent directory.
                //This ensures the Logs folder is only moved to a validly named folder.
                destPath = ensurePathContainsLogsFolder(destPath);

                //Directory move operation will fail if a directory already exists at the destination path
                if (Directory.Exists(destPath))
                {
                    Plugin.Logger.LogInfo("Logging folder already exists at destination. Replacing folder");
                    Directory.Delete(destPath, true);
                }

                if (LogFullPath != null)
                    FileSystemUtils.CopyDirectory(Path.GetDirectoryName(LogFullPath), destPath, true);

                validateFolderStructure(destPath);

                LogFullPath = Path.Combine(destPath, Logger.FormatLogFile(LogName));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Unable to move Logs directory");
                Plugin.Logger.LogError(ex);
            }
        }

        /// <summary>
        /// Throw an exception if path doesn't exists
        /// </summary>
        private void validateFolderStructure(string pathToCheck)
        {
            if (!Directory.Exists(pathToCheck))
                throw new DirectoryNotFoundException(pathToCheck);
        }

        /// <summary>
        /// This flag makes sure that LogEvent can log to file using the BepInEx log without continually failing to log to the custom logger.
        /// </summary>
        private bool selfErrorLogged = false;

        public void LogEvent(object sender, LogEventArgs logEvent)
        {
            if (LogFullPath == null || LogWriter == null || logEvent.Source is UnityLogSource || (logEvent.Level & EnabledLogLevels) == 0) return;

            try
            {
                if ((logEvent.Level & LogLevel.Error) == 0)
                    selfErrorLogged = false;

                LogWriter.WriteLine(logEvent.ToString());
            }
            catch (Exception ex)
            {
                if (!selfErrorLogged)
                {
                    selfErrorLogged = true;
                    Plugin.Logger.LogError(ex);
                }
            }
        }

        public void Dispose()
        {
            FlushTimer?.Dispose();
            CloseWriter();
        }

        public void Signal(string signalWord, string extraInfo = null)
        {
            signalString = "Signal." + signalWord;

            //Debug.Log("Sending signal: " + signalString);

            if (extraInfo != null)
                signalString += '.' + extraInfo;
        }

        public override string ToString()
        {
            return signalString;
        }
    }
}
