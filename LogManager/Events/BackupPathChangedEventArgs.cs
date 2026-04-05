using System;

namespace LogManager.Events
{
    public class BackupPathChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new path that contains, or will contain the Backup folder
        /// </summary>
        public string NewBasePath { get; set; }

        public BackupPathChangedEventArgs(string newBasePath)
        {
            NewBasePath = newBasePath;
        }
    }
}
