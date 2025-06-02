using LogUtils;
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
