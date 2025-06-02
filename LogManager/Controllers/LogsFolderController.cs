using LogUtils;
using System;

namespace LogManager.Controllers
{
    public class LogsFolderController
    {
        public readonly string InitialPath;
        public readonly string AlternatePath;

        public string CurrentPath { get; private set; }

        public bool IsActive;

        /// <summary>
        /// Constructs a LogsFolderController
        /// </summary>
        /// <param name="initialPath">The default path for storing log files</param>
        /// <param name="alternatePath">The alternative path for storing log files</param>
        public LogsFolderController(string initialPath, string alternatePath)
        {
            InitialPath = initialPath;
            AlternatePath = alternatePath;
        }

        public LogsFolderController()
        {
            LogsFolder.Initialize();
        }

        public void SetPath(PathOption option)
        {
            string pathWanted = GetPath(option);

            LogsFolder.SetPath(pathWanted);
        }

        public string GetPath(PathOption option)
        {
            switch (option)
            {
                case PathOption.Initial:
                    return InitialPath;
                case PathOption.Alternative:
                    return AlternatePath;
                case PathOption.Current:
                    return CurrentPath;
                default:
                case PathOption.Next:
                    throw new NotImplementedException();
            }
        }

        private void OnPathChanged(string newPath)
        {
            if (CurrentPath != newPath)
                Plugin.Logger.LogInfo("Logs folder path changed");

            CurrentPath = newPath;
        }

        /// <summary>
        /// Token used to cycle to the next valid logs path directory
        /// </summary>
        public LogsFolderAccessToken AccessToken = new LogsFolderAccessToken(FolderAccess.Strict, LogsFolder.DefaultPath, LogsFolder.AlternativePath);

        public BackupController BackupManager;

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

            try
            {
                LogsFolder.Cycle(AccessToken);
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

    public enum PathOption
    {
        Initial,
        Alternative,
        Current,
        Next
    }
}
