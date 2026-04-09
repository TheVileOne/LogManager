using LogManager.Events;
using LogManager.Helpers;
using LogUtils;
using LogUtils.Enums;
using LogUtils.Events;
using LogUtils.Helpers.Comparers;
using LogUtils.Helpers.FileHandling;
using LogUtils.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BackupEntry = (LogUtils.Enums.LogID ID, bool Enabled);

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

        public const string BACKUP_FILE_MAP = BACKUP_FOLDER_NAME + ".map";

        /// <summary>
        /// The current number of backups per file allowed
        /// </summary>
        public int AllowedBackupsPerFile = ALLOWED_BACKUPS_PER_FILE;

        /// <summary>
        /// All detected backup entries, and their enabled status
        /// </summary>
        public List<BackupEntry> BackupEntries = new List<BackupEntry>();

        /// <summary>
        /// A cache for filepaths pertaining to log backup files
        /// </summary>
        protected List<string> BackupFilesTemp;

        public event EventHandler<BackupPathChangedEventArgs> OnBackupPathChanged;

        private object pathLock = new object();
        private string backupBasePath;
        /// <summary>
        /// The path containing backup files
        /// </summary>
        public string BackupPath
        {
            get
            {
                lock (pathLock)
                {
                    string basePath = PathProvider.Invoke();
                    string currentPath = Path.Combine(basePath, BACKUP_FOLDER_NAME);

                    if (backupBasePath == null)
                    {
                        backupBasePath = basePath;
                        return currentPath;
                    }

                    if (!PathUtils.PathsAreEqual(basePath, backupBasePath))
                    {
                        string oldPath = Path.Combine(backupBasePath, BACKUP_FOLDER_NAME);
                        if (!Directory.Exists(oldPath) || Directory.Exists(currentPath)) //Prefer the new path when it exists
                        {
                            Plugin.Logger.LogInfo("Backup files have changed to a new location");
                            backupBasePath = basePath;
                            OnBackupPathChanged?.Invoke(this, new BackupPathChangedEventArgs(backupBasePath));
                            return currentPath;
                        }
                        return oldPath;
                    }
                    return currentPath;
                }
            }
        }

        protected FolderPathMapper BackupPathMapper;

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

        protected Dictionary<string, string> BackupHistory;

        public BackupController(Func<string> pathProvider)
        {
            PathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            BackupPathMapper = new FolderPathMapper(BackupPath);

            Directory.CreateDirectory(BackupPath);

            initializeBackupHistory();

            OnBackupPathChanged += backupPathChanged;
            UtilityEvents.OnProcessShutdown += saveBackupHistory;
            BackupListener.Feed += createBackupEvent;
        }

        private void initializeBackupHistory()
        {
            BackupHistory = new Dictionary<string, string>();
            try
            {
                foreach (string line in File.ReadAllLines(Path.Combine(BackupPath, BACKUP_FILE_MAP)))
                {
                    int valueIndex = line.IndexOf(':');

                    if (valueIndex == -1) continue;

                    string backupHash = line.Substring(0, valueIndex);
                    string lastKnownBackupPath = line.Substring(valueIndex + 1);

                    if (PathUtils.IsEmpty(lastKnownBackupPath))
                    {
                        Plugin.Logger.LogWarning("Backup history has corrupted entries");
                        continue;
                    }
                    BackupHistory[backupHash] = lastKnownBackupPath;
                }
            }
            catch (FileNotFoundException)
            {
                //Ignore
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Unable to read backup history file");
                Plugin.Logger.LogError(LogID.Exception | LogID.BepInEx, ex);
            }
        }

        private void backupPathChanged(object sender, BackupPathChangedEventArgs e)
        {
            BackupPathMapper = new FolderPathMapper(BackupPath, BackupPathMapper.PathMap);
        }

        private void saveBackupHistory()
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                foreach (var entry in BackupHistory)
                {
                    builder.AppendLine(entry.Key + ':' + entry.Value);
                }
                File.WriteAllText(Path.Combine(BackupPath, BACKUP_FILE_MAP), builder.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Unable to save backup history");
                Plugin.Logger.LogError(LogID.Exception | LogID.BepInEx, ex);
            }
        }

        private void createBackupEvent(BackupListener.EventRecord backupEvent)
        {
            Plugin.Logger.LogInfo($"Received backup event {backupEvent.LogFile}");

            if (!Enabled || !IsBackupAllowed(backupEvent.LogFile))
            {
                Plugin.Logger.LogDebug("File not eligible for backup");
                //Check for an existing backup record - only one record should be stored per log file
                int index = pendingBackups.FindIndex(record => record.LogFile.Equals(backupEvent.LogFile));

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

            LogFilename backupFilename = backupEvent.LogFile.Properties.CurrentFilename;
            string backupHash = backupEvent.LogFile.Properties.GetHashCode().ToString();
            string backupFolderPath = BackupPath;

            bool isGroupFile = backupEvent.LogFile.Properties.Group != null;
            if (isGroupFile)
            {
                Plugin.Logger.LogInfo("Group file detected");
                LogGroupProperties groupProperties = backupEvent.LogFile.Properties.Group.Properties;

                /*
                 * Group members that support path mapping must be associated with a group path, and not be registered.
                 * Registered files are excluded, because of concerns that the backup files will get orphaned if the group path is changed by the user.
                 */
                bool shouldUsePathMap = groupProperties.IsFolderGroup && !backupEvent.LogFile.Registered;
                if (shouldUsePathMap)
                {
                    Plugin.Logger.LogInfo("Resolving path");

                    //Build up a path for the group folder targeting the backup path
                    backupFolderPath = BackupPathMapper.Resolve(backupEvent.LogFile.Properties.CurrentFolderPath).CurrentPath;
                    Directory.CreateDirectory(backupFolderPath);
                }
            }

            manageExistingBackups(backupHash, backupFilename, backupFolderPath);

            string destPath = Path.Combine(backupFolderPath, formatBackupFilename(backupFilename, 1));

            if (!FileUtils.TryCopy(sourcePath, destPath))
            {
                Plugin.Logger.LogInfo($"Unable to backup log file {backupEvent.LogFile}");
                return;
            }

            //Notify other mods that this extra backup source exists
            backupEvent.BackupPaths.Add(destPath);
            addHistoryEntry(backupHash, destPath);
        }

        private void createBackupsFromPendingEntries()
        {
            foreach (var entry in BackupEntries)
            {
                if (!entry.Enabled) continue;

                var backupRecord = pendingBackups.Find(record => entry.ID.Equals(record.LogFile) || (entry.ID is LogGroupID group && group.Properties.Members.Contains(record.LogFile)));

                if (backupRecord != null)
                {
                    createBackup(backupRecord);
                    pendingBackups.Remove(backupRecord);
                }
            }
        }

        private void addHistoryEntry(string backupHash, string backupPath)
        {
            int suffixIndex = backupPath.LastIndexOf("_bkp");

            if (suffixIndex == -1)
                throw new InvalidOperationException("Backup path is malformatted"); //Should not be possible

            string firstPart, secondPart;

            firstPart = backupPath.Substring(0, suffixIndex);
            secondPart = backupPath.Substring(suffixIndex + "_bkp".Length);
            backupPath = firstPart + secondPart;

            //Excluded backup number, and base path information
            BackupHistory[backupHash] = FileUtils.RemoveBracketInfo(PathUtils.TrimCommonRoot(backupPath, BackupPath));
        }

        /// <summary>
        /// Updates the file cache
        /// </summary>
        public void BuildFileCache()
        {
            BackupFilesTemp = null;
            BackupFilesTemp = GetBackupFiles().ToList();
        }

        /// <summary>
        /// Retrieves all filenames (including the path) in the Backups directory containing a supported log file extension
        /// </summary>
        public IEnumerable<string> GetBackupFiles()
        {
            return GetBackupFiles(BackupPath);
        }

        /// <summary>
        /// Retrieves all filenames (including the path) in a specified Backups directory containing a supported log file extension
        /// </summary>
        public IEnumerable<string> GetBackupFiles(string backupPath)
        {
            if (BackupFilesTemp == null)
                return getFiles();

            return BackupFilesTemp.Where(path => PathUtils.PathsAreEqual(path, backupPath));

            IEnumerable<string> getFiles()
            {
                foreach (string path in Directory.EnumerateFiles(backupPath, "*", SearchOption.AllDirectories))
                {
                    if (FileExtension.IsSupported(path))
                        yield return path;
                }
                yield break;
            }
        }

        private string getBackupPathFromHistory(string backupHash, out string backupFilename)
        {
            backupFilename = null;
            if (BackupHistory.TryGetValue(backupHash, out string backupLocation))
                return PathUtils.PathWithoutFilename(Path.Combine(BackupPath, backupLocation), out backupFilename);
            return null;
        }

        /// <summary>
        /// Ensures there is space for a new backup by moving, or deleting existing backups for the specified log file
        /// </summary>
        /// <param name="backupFilename">The filename (with extension) to compare against to find backup matches</param>
        private void manageExistingBackups(string backupHash, LogFilename backupFilename, string currentBackupPath)
        {
            Plugin.Logger.LogInfo("Getting backup history for " + backupFilename);
            string lastBackupPath = getBackupPathFromHistory(backupHash, out string lastBackupFilename);

            bool filenameChanged, pathChanged;
            string filenamePattern;

            List<string> existingBackups;
            if (PathUtils.IsEmpty(lastBackupPath))
            {
                Plugin.Logger.LogDebug("No backup history found - checking backup path");

                filenamePattern = backupFilename + "_bkp";
                existingBackups = FindExistingBackups(filenamePattern, currentBackupPath);

                filenameChanged = pathChanged = false;
            }
            else
            {
                filenamePattern = FileExtension.Remove(lastBackupFilename) + "_bkp";
                existingBackups = FindExistingBackups(filenamePattern, lastBackupPath);

                filenameChanged = !ComparerUtils.FilenameComparer.Equals(backupFilename.WithExtension(), lastBackupFilename);
                pathChanged = !PathUtils.PathsAreEqual(currentBackupPath, lastBackupPath);
            }

            Plugin.Logger.LogInfo($"{existingBackups.Count} existing backups detected");
            if (filenameChanged || pathChanged)
            {
                Plugin.Logger.LogDebug("History entry path info does not match");
                foreach (string backup in existingBackups)
                {
                    string targetPath = PathUtils.PathWithoutFilename(backup, out string targetFilename);

                    if (filenameChanged)
                    {
                        //Transfer the old bracket info to the new filename
                        targetFilename = formatBackupFilename(backupFilename, parseBackupNumber(targetFilename));
                    }

                    if (pathChanged)
                        targetPath = currentBackupPath;

                    targetPath = Path.Combine(targetPath, targetFilename);
                    FileUtils.TryMove(backup, targetPath);
                }
                DirectoryUtils.TryDelete(lastBackupPath, DirectoryDeletionScope.OnlyIfEmpty, DirectoryDeletionMode.Permanent);

                Plugin.Logger.LogInfo("Rebuilding file cache");
                BuildFileCache();
                filenamePattern = backupFilename + "_bkp";
                existingBackups = FindExistingBackups(filenamePattern, currentBackupPath);
            }

            //Handle existing backups
            if (existingBackups.Count > 0)
            {
                int backupCountOverMaximum = Math.Max(0, existingBackups.Count - AllowedBackupsPerFile);

                for (int i = existingBackups.Count; i > 0; i--)
                {
                    string backup = existingBackups[i - 1];

                    if (backupCountOverMaximum > 0)
                    {
                        FileUtils.TryDelete(backup);
                        backupCountOverMaximum--;
                        continue;
                    }

                    if (i < AllowedBackupsPerFile) //Renames existing backup by changing its number by one 
                        FileUtils.TryMove(backup, Path.Combine(currentBackupPath, formatBackupFilename(backupFilename, i + 1)), 3);
                    else
                        FileUtils.TryDelete(backup); //The backup at the max count simply gets removed
                }
            }
        }

        /// <summary>
        /// Formats a valid backup path using a <see cref="LogFilename"/> as a reference
        /// </summary>
        private string formatBackupFilename(LogFilename filenameBase, int backupNumber)
        {
            return $"{filenameBase}_bkp[{backupNumber}]{filenameBase.Extension}";
        }

        /// <summary>
        /// Find backups associated with a LogID
        /// </summary>
        public List<string> FindExistingBackups(LogID logFile)
        {
            return FindExistingBackups(filenamePattern: logFile.Properties.CurrentFilename + "_bkp", BackupPath);
        }

        /// <summary>
        /// Find backups associated with a filename
        /// </summary>
        /// <param name="filenamePattern">A search pattern to use to find file matches</param>
        /// <param name="backupFolderPath">The exact folder to check for file matches</param>
        public List<string> FindExistingBackups(string filenamePattern, string backupFolderPath)
        {
            if (BackupFilesTemp == null)
                BuildFileCache();

            List<string> existingBackups = new List<string>(AllowedBackupsPerFile);
            List<int> existingBackupIndexes = new List<int>(AllowedBackupsPerFile);

            foreach (string backupPath in GetBackupFiles(backupFolderPath))
            {
                string backupFile = Path.GetFileNameWithoutExtension(backupPath);

                if (backupFile.StartsWith(filenamePattern)) //Look for the format '<file>_<number>'
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
            var entry = BackupEntries.Find(entry => entry.ID.Equals(logFile) || (entry.ID is LogGroupID group && group.Properties.Members.Contains(logFile)));
            return entry.Enabled;
        }

        private int parseBackupNumber(string backupFilename)
        {
            string info = FileUtils.GetBracketInfo(backupFilename);

            int value;
            if (info == null || !int.TryParse(info, out value))
                return -1;
            return value;
        }

        /// <summary>
        /// Updates all lists with new enabled state values
        /// </summary>
        public void ProcessChanges(List<BackupEntry> changedEntries)
        {
            bool shouldSort = false;
            foreach (var backupEntry in changedEntries)
            {
                LogID backupID = backupEntry.ID;
                bool backupEnabled = backupEntry.Enabled;

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

                        int entryIndex = BackupEntries.FindIndex(entry => entry.ID == backupID);
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

                        int entryIndex = BackupEntries.FindIndex(entry => entry.ID == backupID);
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

            foreach (LogID logID in LogProperties.PropertyManager.AllProperties.Select(p => p.ID))
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
                    BackupEntries.Add(new BackupEntry(logID, true));
                }
                else if (backupDisabled)
                {
                    disabledList.Remove(logID);
                    BackupEntries.Add(new BackupEntry(logID, false));
                }
                else
                    throw new InvalidStateException("Backup entry must be enabled or disabled");
            }

            //Include the remaining objects in both lists. These entries are not contained within the target path, but have been recorded to file
            BackupEntries.AddRange(enabledList.Select(entry => new BackupEntry(entry, true)));
            BackupEntries.AddRange(disabledList.Select(entry => new BackupEntry(entry, false)));

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
            FileUtils.TryWrite(blacklistPath, DisabledList.Select(logID => logID.Value));
            FileUtils.TryWrite(whitelistPath, EnabledList.Select(logID => logID.Value));
        }

        /// <summary>
        /// Call this when the backup process is finished to release temp references used during the backup process
        /// </summary>
        public void Finish()
        {
            LogIDComparer idComparer = new LogIDComparer(CompareOptions.Filename);

            BackupEntries.Sort(compareEntriesByFilename);

            createBackupsFromPendingEntries();
            BackupFilesTemp = null;

            int compareEntriesByFilename(BackupEntry entry, BackupEntry entryOther)
            {
                int compareValue = idComparer.Compare(entry.ID, entryOther.ID);

                //Due to the way LogIDs are created, properties shouldn't be null here
                if (compareValue == 0)
                    compareValue = new FolderNameComparer().Compare(entry.ID.Properties.CurrentFolderPath, entryOther.ID.Properties.CurrentFolderPath);
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
