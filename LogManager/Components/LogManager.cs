using LogUtils;
using LogUtils.Properties;

namespace LogManager.Components
{
    public class LogManager
    {
        public BackupController BackupManager;

        public LogManager()
        {
            LogsFolder.Initialize();
            BackupManager = new BackupController(LogsFolder.Path, "Backup");
        }

        public void ProcessFiles()
        {
            foreach (LogProperties properties in LogProperties.PropertyManager.Properties)
            {
                if (BackupManager.Enabled)
                {
                    BackupManager.CreateBackupCopy(properties.ID);
                    BackupManager.HasRunOnce = true;
                }
            }
        }
    }
}
