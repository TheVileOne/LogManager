using LogManager.Helpers;
using LogUtils;
using LogUtils.Enums;
using LogUtils.Helpers.Comparers;
using LogUtils.Helpers.FileHandling;
using LogUtils.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LogManager.Controllers
{
    public class BackupController
    {
        /// <summary>
        /// The number of backups per file allowed by default 
        /// </summary>
        public const int ALLOWED_BACKUPS_PER_FILE = 2;

        /// <summary>
        /// The folder that will store backup files
        /// </summary>
        public const string BACKUP_FOLDER_NAME = "Backup";

        /// <summary>
        /// The current number of backups per file allowed
        /// </summary>
        public int AllowedBackupsPerFile = ALLOWED_BACKUPS_PER_FILE;

        /// <summary>
        /// All detected backup entries, and their enabled status
        /// </summary>
        public List<(LogID, bool)> BackupEntries = new List<(LogID, bool)>();

        /// <summary>
        /// A cache for filepaths pertaining to log backup files
        /// </summary>
        protected List<string> BackupFilesTemp;

        /// <summary>
        /// The path containing backup files
        /// </summary>
        public string BackupPath => Path.Combine(PathProvider.Invoke(), BACKUP_FOLDER_NAME);

        /// <summary>
        /// A flag that controls whether backups may be processed
        /// </summary>
        public bool Enabled;

        public List<LogID> EnabledList = new List<LogID>();
        public List<LogID> DisabledList = new List<LogID>();

        /// <summary>
        /// These logs default to being enabled except when the user adds them to the disabled list
        /// </summary>
        public LogID[] EnabledByDefault = new LogID[]
        {
            LogID.Unity,
            LogID.Exception,
            LogID.BepInEx,
        };

        private List<BackupListener.EventRecord> pendingBackups = new List<BackupListener.EventRecord>();

        /// <summary>
        /// When true, any backup candidate that hasn't been added to backup-blacklist.txt will be enabled by default.
        /// This feature only works when backups are enabled. This flag can still be true when Enabled is false.
        /// </summary>
        public bool ProgressiveEnableMode;

        protected Func<string> PathProvider;

        public BackupController(Func<string> pathProvider)
        {
            PathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

            Directory.CreateDirectory(BackupPath);
            BackupListener.Feed += createBackupEvent;
        }

        private void createBackupEvent(BackupListener.EventRecord backupEvent)
        {
            if (!Enabled || !IsBackupAllowed(backupEvent.LogFile))
            {
                //Check for an existing backup record - only one record should be stored per log file
                int index = pendingBackups.FindIndex(r => r.LogFile.Equals(backupEvent.LogFile));

                if (index != -1)
                    pendingBackups.RemoveAt(index);

                //We cannot handle backup events right now - it might be possible to handle them later
                pendingBackups.Add(backupEvent);
                return;
            }
            createBackup(backupEvent);
        }

        private void createBackup(BackupListener.EventRecord backupEvent)
        {
            string sourcePath = backupEvent.SourcePath;

            //The file not existing will generally happen for two reasons:
            // - Another mod may have moved the file
            // - It is too late to recover this backup, and LogUtils has deleted it
            if (!File.Exists(sourcePath))
            {
                //Check for a different source path where the path does exist
                sourcePath = backupEvent.BackupPaths.FirstOrDefault(File.Exists);
            }

            if (sourcePath == null)
            {
                Plugin.Logger.LogInfo($"Unable to backup log file {backupEvent.LogFile}");
                return;
            }

            string backupFilename = backupEvent.LogFile.Properties.CurrentFilename.WithExtension();

            manageExistingBackups(backupFilename);

            string destPath = Path.Combine(BackupPath, formatBackupPath(backupFilename, 1));

            if (!FileUtils.SafeCopy(sourcePath, destPath))
            {
                Plugin.Logger.LogInfo($"Unable to backup log file {backupEvent.LogFile}");
                return;
            }

            //Notify other mods that this extra backup source exists
            backupEvent.BackupPaths.Add(destPath);
        }

        private void createBackupsFromPendingEntries()
        {
            foreach (var entry in BackupEntries)
            {
                if (!entry.Item2) continue; //Entry is disabled

                var backupRecord = pendingBackups.Find(record => entry.Item1.Equals(record.LogFile));

                if (backupRecord != null)
                {
                    createBackup(backupRecord);
                    pendingBackups.Remove(backupRecord);
                }
            }
        }

        /// <summary>
        /// Updates the file cache
        /// </summary>
        public void BuildFileCache()
        {
            BackupFilesTemp = GetBackupFiles().ToList();
        }

        /// <summary>
        /// Retrieves all filenames (including the path) in the Backups directory containing a supported log file extension
        /// </summary>
        public IEnumerable<string> GetBackupFiles()
        {
            foreach (string path in Directory.EnumerateFiles(BackupPath))
            {
                if (FileUtils.IsSupportedExtension(path))
                    yield return path;
            }
            yield break;
        }

        /// <summary>
        /// Ensures there is space for a new backup by moving, or deleting existing backups for the specified log file
        /// </summary>
        /// <param name="backupFilename">The filename (with extension) to compare against to find backup matches</param>
        private void manageExistingBackups(string backupFilename)
        {
            List<string> existingBackups = FindExistingBackups(backupFilename);

            Plugin.Logger.LogInfo($"{existingBackups.Count} existing backups for {backupFilename} detected");

            //Handle existing backups
            if (existingBackups.Count > 0)
            {
                int backupCountOverMaximum = Math.Max(0, existingBackups.Count - AllowedBackupsPerFile);

                for (int i = existingBackups.Count; i > 0; i--)
                {
                    string backup = existingBackups[i - 1];

                    if (backupCountOverMaximum > 0)
                    {
                        FileUtils.SafeDelete(backup);
                        backupCountOverMaximum--;
                        continue;
                    }

                    if (i < AllowedBackupsPerFile) //Renames existing backup by changing its number by one 
                        FileUtils.SafeMove(backup, formatBackupPath(backupFilename, i + 1), 3);
                    else
                        FileUtils.SafeDelete(backup); //The backup at the max count simply gets removed
                }
            }
        }

        /// <summary>
        /// Formats a valid backup path using a base with file extension as a reference
        /// </summary>
        private string formatBackupPath(string filenameBase, int backupNumber)
        {
            return Path.Combine(BackupPath, $"{Path.GetFileNameWithoutExtension(filenameBase)}_bkp[{backupNumber}]{Path.GetExtension(filenameBase)}");
        }

        /// <summary>
        /// Find backups associated with a LogID
        /// </summary>
        public List<string> FindExistingBackups(LogID logFile, out string backupFilename)
        {
            backupFilename = logFile.Properties.CurrentFilename;
            return FindExistingBackups(backupFilename);
        }

        /// <summary>
        /// Find backups associated with a filename
        /// </summary>
        /// <param name="backupName">
        /// A filename (without path)
        /// <br>File extension will be removed if present</br>
        /// </param>
        public List<string> FindExistingBackups(string backupName)
        {
            if (BackupFilesTemp == null)
                BuildFileCache();

            backupName = Path.GetFileNameWithoutExtension(backupName);

            List<string> existingBackups = new List<string>(AllowedBackupsPerFile);
            List<int> existingBackupIndexes = new List<int>(AllowedBackupsPerFile);

            foreach (string backupPath in BackupFilesTemp)
            {
                string backupFile = Path.GetFileNameWithoutExtension(backupPath);

                if (backupFile.StartsWith(backupName + "_bkp")) //Look for the format '<file>_<number>'
                {
                    int backupNumber = parseBackupNumber(backupFile); //Not zero-based
                    if (backupNumber != -1) //Leave malformatted backups alone
                    {
                        //Sort the list by backupNumber

                        //First, check if backup is the highest recorded value
                        if (existingBackups.Count == 0 || backupNumber > existingBackupIndexes[existingBackupIndexes.Count - 1])
                        {
                            existingBackups.Add(backupPath);
                            existingBackupIndexes.Add(backupNumber);

                            continue;
                        }

                        //Next, check from highest index to lowest until backupNumber is greater than compared value
                        int compareIndex = existingBackupIndexes.Count - 2;
                        while (compareIndex > 0 && backupNumber < existingBackupIndexes[compareIndex])
                        {
                            compareIndex--;
                        }

                        //compareIndex is greater than zero if we found a valid place for this backup.
                        //Since we do not check the first index, check that too
                        if (compareIndex > 0 || backupNumber > existingBackupIndexes[0])
                        {
                            compareIndex++; //Actual insertion index

                            existingBackups.Insert(compareIndex, backupPath);
                            existingBackupIndexes.Insert(compareIndex, backupNumber);
                        }
                        else
                        {
                            //New lowest value - This is unlikely to trigger
                            existingBackups.Insert(0, backupPath);
                            existingBackupIndexes.Insert(0, backupNumber);
                        }
                    }
                }
            }

            return existingBackups;
        }

        public bool IsBackupAllowed(LogID logFile)
        {
            var entry = BackupEntries.Find(entry => entry.Item1.Equals(logFile));
            return entry.Item2;
        }

        private int parseBackupNumber(string backupFilename)
        {
            int parseIndexStart = backupFilename.LastIndexOf('[');
            int parseIndexEnd = 1;//backupFilename.LastIndexOf(']');

            if (parseIndexStart < 0)
                return -1;

            string parseSubstring = backupFilename.Substring(parseIndexStart + 1, parseIndexEnd);

            int foundIndex;
            if (int.TryParse(parseSubstring, out foundIndex))
                return foundIndex;
            return -1;
        }

        /// <summary>
        /// Updates all lists with new enabled state values
        /// </summary>
        public void ProcessChanges(List<(LogID, bool)> changedEntries)
        {
            bool shouldSort = false;
            foreach (var backupEntry in changedEntries)
            {
                LogID backupID = backupEntry.Item1;
                bool backupEnabled = backupEntry.Item2;

                Plugin.Logger.LogInfo("Processing entry: " + backupID);
                Plugin.Logger.LogInfo("Enabled: " + backupEnabled);

                //A changed entry means that the entry has been changed from enabled to disabled, or vice versa,
                //or a new entry has been detected that is not part of any of the lists
                if (backupEnabled)
                {
                    if (DisabledList.Remove(backupID))
                        EnabledList.Add(backupID);
                    else if (!EnabledList.Contains(backupID)) //Maybe it is a new entry 
                    {
                        EnabledList.Add(backupID);

                        int entryIndex = BackupEntries.FindIndex(b => b.Item1 == backupID);
                        if (entryIndex != -1) //Replace original backup entry with changed one
                            BackupEntries[entryIndex] = backupEntry;
                        else
                        {
                            BackupEntries.Add(backupEntry);
                            shouldSort = true;
                        }
                    }
                }
                else
                {
                    if (EnabledList.Remove(backupID))
                        DisabledList.Add(backupID);
                    else if (!DisabledList.Contains(backupID)) //Maybe it is a new entry 
                    {
                        DisabledList.Add(backupID);

                        int entryIndex = BackupEntries.FindIndex(b => b.Item1 == backupID);
                        if (entryIndex != -1) //Replace original backup entry with changed one
                            BackupEntries[entryIndex] = backupEntry;
                        else
                        {
                            BackupEntries.Add(backupEntry);
                            shouldSort = true;
                        }
                    }
                }
            }

            if (shouldSort)
                BackupEntries.Sort();
        }

        public void PopulateLists()
        {
            PopulateAllowList();
            PopulateDisallowList();

            applyEnabledDefaults();
        }

        public void PopulateAllowList()
        {
            EnabledList.Clear();

            string whitelistPath = Path.Combine(Plugin.ModPath, ModConsts.Files.BACKUP_WHITELIST);

            if (!File.Exists(whitelistPath)) return;

            //Populates enabled backup candidates from backup-whitelist.txt
            IEnumerator<string> whitelist = File.ReadLines(whitelistPath).GetEnumerator();
            while (whitelist.MoveNext())
            {
                string entry = whitelist.Current.Trim();

                if (entry.StartsWith("//") || entry.StartsWith("#") || entry == string.Empty) //Comment symbols
                    continue;

                LogID logID = LogID.Find(entry);
                //entry = Path.GetFileNameWithoutExtension(entry); //Ensure string comparison is more reliable

                if (logID != null && !DisabledList.Contains(logID))
                    EnabledList.Add(logID);
            }
        }

        public void PopulateDisallowList()
        {
            DisabledList.Clear();

            string blacklistPath = Path.Combine(Plugin.ModPath, ModConsts.Files.BACKUP_BLACKLIST);

            if (!File.Exists(blacklistPath)) return;

            //Populates disabled backup candidates from backup-blacklist.txt
            IEnumerator<string> blacklist = File.ReadLines(blacklistPath).GetEnumerator();
            while (blacklist.MoveNext())
            {
                string entry = blacklist.Current.Trim();

                if (entry.StartsWith("//") || entry.StartsWith("#") || entry == string.Empty) //Comment symbols
                    continue;

                LogID logID = LogID.Find(entry);
                //entry = Path.GetFileNameWithoutExtension(entry); //Ensure string comparison is more reliable

                if (logID != null)
                {
                    EnabledList.Remove(logID);
                    DisabledList.Add(logID);
                }
            }
        }

        public void ProcessNewEntries()
        {
            BackupEntries.Clear();

            List<LogID> enabledList = new List<LogID>(EnabledList);
            List<LogID> disabledList = new List<LogID>(DisabledList);

            foreach (LogID logID in LogProperties.PropertyManager.Properties.Select(p => p.ID))
            {
                bool backupEnabled, backupDisabled;

                backupEnabled = enabledList.Contains(logID);
                backupDisabled = !backupEnabled;

                if (!backupEnabled)
                {
                    backupDisabled = disabledList.Contains(logID) || !ProgressiveEnableMode;

                    if (!backupDisabled) //ProgressiveEnableMode acts like an enable all, except if user disabled function
                    {
                        EnabledList.Add(logID);
                        backupEnabled = true;
                    }
                }

                Plugin.Logger.LogInfo("Target " + logID);

                if (backupEnabled) //Only allowed files will be available for backup
                {
                    enabledList.Remove(logID);
                    BackupEntries.Add((logID, true));
                }
                else if (backupDisabled)
                {
                    disabledList.Remove(logID);
                    BackupEntries.Add((logID, false));
                }
                else
                    throw new InvalidStateException("Backup entry must be enabled or disabled");
            }

            //Include the remaining objects in both lists. These entries are not contained within the target path, but have been recorded to file
            BackupEntries.AddRange(enabledList.Select<LogID, (LogID, bool)>(entry => (entry, true)));
            BackupEntries.AddRange(disabledList.Select<LogID, (LogID, bool)>(entry => (entry, false)));

            Finish();
        }

        /// <summary>
        /// Common log sources, while other logs require a setting, or user interaction to enable
        /// </summary>
        private void applyEnabledDefaults()
        {
            foreach (LogID defaultEntry in EnabledByDefault)
            {
                if (!EnabledList.Contains(defaultEntry) && !DisabledList.Contains(defaultEntry))
                    EnabledList.Add(defaultEntry);
            }
        }

        /// <summary>
        /// Creates blacklist, and whitelist text files based on current lists
        /// </summary>
        public void SaveListsToFile()
        {
            Plugin.Logger.LogInfo("Writing list data to file");

            string blacklistPath, whitelistPath;

            blacklistPath = Path.Combine(Plugin.ModPath, ModConsts.Files.BACKUP_BLACKLIST);
            whitelistPath = Path.Combine(Plugin.ModPath, ModConsts.Files.BACKUP_WHITELIST);

            //Create or overwrite existing files
            FileUtils.SafeWriteToFile(blacklistPath, DisabledList.Select(logID => logID.Value));
            FileUtils.SafeWriteToFile(whitelistPath, EnabledList.Select(logID => logID.Value));
        }

        /// <summary>
        /// Call this when the backup process is finished to release temp references used during the backup process
        /// </summary>
        public void Finish()
        {
            LogIDComparer idComparer = new LogIDComparer(CompareOptions.CurrentFilename);

            BackupEntries.Sort(compareEntriesByCurrentFilename);

            createBackupsFromPendingEntries();
            BackupFilesTemp = null;

            int compareEntriesByCurrentFilename((LogID, bool) entry, (LogID, bool) entryOther)
            {
                int compareValue = idComparer.Compare(entry.Item1, entryOther.Item1);

                //Due to the way LogID are created, properties shouldn't be null here
                if (compareValue == 0)
                    compareValue = new FolderNameComparer().Compare(entry.Item1.Properties.CurrentFolderPath, entryOther.Item1.Properties.CurrentFolderPath);
                return compareValue;
            }
        }

        private class InvalidStateException : Exception
        {
            public InvalidStateException(string message) : base(message)
            {
            }
        }
    }
}
