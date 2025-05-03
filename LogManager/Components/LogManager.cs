using LogUtils;
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
            //TODO: This is a remnant of the old backup system
            if (BackupManager.Enabled)
                BackupManager.HasRunOnce = true;

            foreach (LogProperties properties in LogProperties.PropertyManager.Properties)
            {
            }
        } 

        public bool RequestPathChange(string path)
        {
            try
            {
                //Path can only be one of two valid paths
                if (!LogsFolder.IsLogsFolderPath(path))
                    throw new NotSupportedException("LogManager does not support custom paths");

                ChangePath(path);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public void ChangePath(string logPath)
        {
            //Path change is unnecessary
            if (LogsFolder.IsCurrentPath(logPath))
            {
                //TODO: Log
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
