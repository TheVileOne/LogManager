using LogManager.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogManager.Backup
{
    public class BackupController
    {
        public int AllowedBackupsPerFile = 5;

        public bool IsManagingLogFiles = true;

        /// <summary>
        /// The path containing backup files
        /// </summary>
        public string BackupPath;

        public bool HasRunOnce;

        /// <summary>
        /// A flag that controls whether backups may be processed
        /// </summary>
        public bool Enabled;

        public List<string> EnabledList = new List<string>();
        public List<string> DisabledList = new List<string>();

        /// <summary>
        /// These logs default to being enabled except when the user adds them to the disabled list
        /// </summary>
        public string[] EnabledByDefault = new string[]
        {
            "console",
            "exception",
            "mods",
        };

        /// <summary>
        /// When true, any backup candidate that hasn't been added to backup-blacklist.txt will be enabled by default.
        /// This feature only works when backups are enabled. This flag can still be true when Enabled is false.
        /// </summary>
        public bool ProgressiveEnableMode;

        /// <summary>
        /// All detected backup entries, and their enabled status
        /// </summary>
        public List<(string, bool)> BackupEntries = new List<(string, bool)>();

        protected string[] BackupFilesTemp;

        public BackupController(string containingFolderPath, string backupFolderName)
        {
            BackupPath = Path.Combine(containingFolderPath, backupFolderName);
            Directory.CreateDirectory(BackupPath);
        }

        public void BackupFromFolder(string targetPath)
        {
            ProcessFolder(targetPath, Enabled);
            HasRunOnce = true;
        }

        /// <summary>
        /// Logic necessary for storing backup file data
        /// </summary>
        public void BackupFile(string backupSourcePath)
        {
            if (BackupFilesTemp == null)
                BackupFilesTemp = Directory.GetFiles(BackupPath);

            string sourceFilename = Path.GetFileName(backupSourcePath);
            string backupTargetPath = formatBackupPath(sourceFilename, 1); //Formats the path that backup file will be copied to 

            //After this runs, if there are no issues, the target path will be free
            manageExistingBackups(sourceFilename);

            //Create backup file
            FileSystemUtils.SafeCopyFile(backupSourcePath, backupTargetPath, 3);
        }

        public void ProcessFolder(string targetPath, bool backupFiles)
        {
            BackupEntries.Clear();

            List<string> enabledList = new List<string>(EnabledList);
            List<string> disabledList = new List<string>(DisabledList);

            foreach (string file in Directory.GetFiles(targetPath))
            {
                string backupFileEntry = Path.GetFileNameWithoutExtension(file);

                bool backupEnabled = enabledList.Contains(backupFileEntry);

                if (!backupEnabled)
                {
                    bool backupDisabled = disabledList.Contains(backupFileEntry);

                    //Entries are removed in order to narrow down the lists of entries to those not present in the target path
                    if (backupDisabled)
                    {
                        disabledList.Remove(backupFileEntry);
                        BackupEntries.Add((backupFileEntry, false));
                    }
                    else if (ProgressiveEnableMode) //ProgressiveEnableMode acts like an enable all, except if user disabled function
                    {
                        EnabledList.Add(backupFileEntry);
                        backupEnabled = true;
                    }
                }

                Plugin.Logger.LogInfo("Target " + backupFileEntry);

                if (backupEnabled) //Only allowed files will be available for backup
                {
                    enabledList.Remove(backupFileEntry);
                    BackupEntries.Add((backupFileEntry, true));

                    if (backupFiles)
                        BackupFile(file);
                }
            }

            //Include the remaining objects in both lists. These entries are not contained within the target path, but have been recorded to file
            BackupEntries.AddRange(enabledList.Select<string, (string, bool)>(entry => (entry, true)));
            BackupEntries.AddRange(disabledList.Select<string, (string, bool)>(entry => (entry, false)));
            Finish();
        }

        /// <summary>
        /// Finds all existing backups with valid backup formatting for a given file, either deleting, or moving the file to a higher backup number
        /// </summary>
        /// <param name="sourceFilename">The filename (with extension) of the source file containing backups</param>
        private void manageExistingBackups(string sourceFilename)
        {
            List<string> existingBackups = FindExistingBackups(Path.GetFileNameWithoutExtension(sourceFilename));

            Plugin.Logger.LogInfo($"{existingBackups.Count} existing backups for {sourceFilename} detected");

            //Handle existing backups
            if (existingBackups.Count > 0)
            {
                int backupCountOverMaximum = Math.Max(0, existingBackups.Count - AllowedBackupsPerFile);

                for (int i = existingBackups.Count - 1; i >= 0; i--)
                {
                    string backup = existingBackups[i];

                    if (backupCountOverMaximum > 0)
                    {
                        FileSystemUtils.SafeDeleteFile(backup);
                        backupCountOverMaximum--;
                        continue;
                    }

                    if (i < AllowedBackupsPerFile) //Renames existing backup by changing its number by one 
                        FileSystemUtils.SafeMoveFile(backup, formatBackupPath(sourceFilename, i + 2), 3);
                    else
                        FileSystemUtils.SafeDeleteFile(backup); //The backup at the max count simply gets removed
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

        public List<string> FindExistingBackups(string backupName)
        {
            List<string> existingBackups = new List<string>(AllowedBackupsPerFile);
            List<int> existingBackupIndexes = new List<int>(AllowedBackupsPerFile);

            for (int i = 0; i < BackupFilesTemp.Length; i++)
            {
                string backupPath = BackupFilesTemp[i];
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

        /// <summary>
        /// Updates all lists with new enabled state values
        /// </summary>
        public void ProcessChanges(List<(string, bool)> changedEntries)
        {
            bool shouldSort = false;
            foreach (var backupEntry in changedEntries)
            {
                string backupName = backupEntry.Item1;
                bool backupEnabled = backupEntry.Item2;

                Plugin.Logger.LogInfo("Processing entry: " + backupName);

                //A changed entry means that the entry has been changed from enabled to disabled, or vice versa,
                //or a new entry has been detected that is not part of any of the lists
                if (backupEnabled)
                {
                    if (DisabledList.Remove(backupName))
                        EnabledList.Add(backupName);
                    else if (!EnabledList.Contains(backupName)) //Maybe, it is a new entry 
                    {
                        EnabledList.Add(backupName);

                        int entryIndex = BackupEntries.FindIndex(b => b.Item1 == backupName);
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
                    if (EnabledList.Remove(backupName))
                        DisabledList.Add(backupName);
                    else if (!DisabledList.Contains(backupName)) //Maybe, it is a new entry 
                    {
                        DisabledList.Add(backupName);

                        int entryIndex = BackupEntries.FindIndex(b => b.Item1 == backupName);
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

                entry = Path.GetFileNameWithoutExtension(entry); //Ensure string comparison is more reliable

                if (!DisabledList.Contains(entry))
                    EnabledList.Add(entry);
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

                entry = Path.GetFileNameWithoutExtension(entry); //Ensure string comparison is more reliable

                EnabledList.Remove(entry);
                DisabledList.Add(entry);
            }
        }

        /// <summary>
        /// Common log sources, while other logs require a setting, or user interaction to enable
        /// </summary>
        private void applyEnabledDefaults()
        {
            foreach (string defaultEntry in EnabledByDefault)
            {
                if (!EnabledList.Contains(defaultEntry) && !DisabledList.Contains(defaultEntry))
                    EnabledList.Add(defaultEntry);
            }
        }

        /// <summary>
        /// Creates blacklist, and whitelist txt files based on current lists
        /// </summary>
        public void SaveListsToFile(bool forceSave)
        {
            if (!Enabled && !forceSave) return;

            Plugin.Logger.LogInfo("Writing list data to file");

            string blacklistPath, whitelistPath;

            blacklistPath = Path.Combine(Plugin.ModPath, ModConsts.Files.BACKUP_BLACKLIST);
            whitelistPath = Path.Combine(Plugin.ModPath, ModConsts.Files.BACKUP_WHITELIST);

            //Create or overwrite existing files
            FileSystemUtils.SafeWriteToFile(blacklistPath, DisabledList);
            FileSystemUtils.SafeWriteToFile(whitelistPath, EnabledList);
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
        /// Call this when the backup process is finished to release temp references used during the backup process
        /// </summary>
        public void Finish()
        {
            SaveListsToFile(false); //Make sure files are up to date - list data may have been incomplete
            BackupEntries = BackupEntries.OrderBy(entry => //TODO Check if default sort is alphabetical
            {
                return entry.Item1;
            }).ToList();
            BackupFilesTemp = null;
        }
    }
}
