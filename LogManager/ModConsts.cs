namespace LogManager
{
    public static class ModConsts
    {
        public static class Config
        {
            public static class Descriptions
            {
                public const string DIRECTORY_SELECT = "Choose your Logs folder";
                public const string ALLOW_BACKUPS_TOGGLE = "Allow log backups";
                public const string PROGRESSIVE_BACKUPS_TOGGLE = "Enable backups for newly detected log files on startup";
                public const string BACKUPS_PER_FILE = "Backups per file";
                public const string DELETE_OPTION = "Removes Backup folder and its contents";
                public const string BACKUPS_ENABLED_LIST = "Allow backups for this log";
            }

            public static class OptionLabels
            {
                public const string DIRECTORY_SELECT = "Log directory path";
                public const string ALLOW_BACKUPS_TOGGLE = "Backup log files when Rain World starts";
                public const string PROGRESSIVE_BACKUPS_TOGGLE = "Automatically enable backups for newly detected log files";
                public const string BACKUPS_PER_FILE = "Allowed backups per file";
                public const string DELETE_OPTION = "Delete Backups";
            }

            public static class Headers
            {
                public const string PRIMARY = "Logging Tools";
                public const string BACKUPS = "Backup Management";
                public const string BACKUPS_ENABLED_LIST = "Backup Allow List";
            }
        }

        public static class Files
        {
            public const string BACKUP_BLACKLIST = "backup-blacklist.txt";
            public const string BACKUP_WHITELIST = "backup-whitelist.txt";
        }
    }
}
