using LogUtils;
using LogUtils.Enums;
using LogUtils.Helpers;
using LogUtils.Helpers.FileHandling;
using LogUtils.Properties;
using System;

namespace LogManager.Components
{
    public class LogManager
    {
        public BackupController BackupManager;

        public LogManager()
        {
            LogsFolder.Initialize();
            BackupManager = new BackupController();
        }

        public void ProcessFiles()
        {
            foreach (LogProperties properties in LogProperties.PropertyManager.Properties)
            {
                LogID logFile = properties.ID;

                if (!properties.LogsFolderEligible)
                {
                    Plugin.Logger.LogInfo($"{logFile} is currently ineligible to be moved to Logs folder");
                    continue;
                }

                if (!properties.FileExists)
                {
                    properties.ChangePath(LogsFolder.Path);
                    continue;
                }

                bool isMoveRequired = !PathUtils.PathsAreEqual(properties.CurrentFolderPath, LogsFolder.Path);

                if (isMoveRequired)
                {
                    Plugin.Logger.LogInfo($"Moving {logFile} to Logs folder");
                    LogFile.Move(logFile, LogsFolder.Path);
                }
            }
        }

        public bool RequestPathChange(string path)
        {
            try
            {
                //Path can only be one of two valid paths
                if (!LogsFolder.IsLogsFolderPath(path))
                    throw new NotSupportedException("LogManager does not support custom paths");

                UpdateLogsFolderPath(path);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
                return false;
            }
        }

        public void UpdateLogsFolderPath(string newFolderPath)
        {
            if (LogsFolder.IsCurrentPath(newFolderPath))
            {
                Plugin.Logger.LogInfo("Folder path change not needed");
                return;
            }

            LogsFolderAccessToken accessToken = new LogsFolderAccessToken(FolderAccess.Strict, LogsFolder.DefaultPath, LogsFolder.AlternativePath);

            try
            {
                LogsFolder.Cycle(accessToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                //Invalid access exception
            }
            catch (Exception ex)
            {
                //Actual error trying to move folder
            }
        }
    }
}
