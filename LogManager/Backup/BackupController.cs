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
        public List<string> EnabledForBackup = new List<string>();

        protected string[] BackupFilesTemp;

        public BackupController(string containingFolderPath, string backupFolderName)
        {
            BackupPath = Path.Combine(containingFolderPath, backupFolderName);
            Directory.CreateDirectory(BackupPath);
        }

        public void BackupFromFolder(string targetPath)
        {
            foreach (string file in Directory.GetFiles(targetPath))
            {
                string filenameNoExt = Path.GetFileNameWithoutExtension(file);

                if (EnabledForBackup.Contains(filenameNoExt)) //Only allowed files will be available for backup
                    BackupFile(file);
            }

            Finish();
        }

        /// <summary>
        /// Logic necessary for storing backup file data
        /// </summary>
        public void BackupFile(string filepath)
        {
            if (BackupFilesTemp == null)
                BackupFilesTemp = Directory.GetFiles(BackupPath);

            string sourceFileExt = Path.GetExtension(filepath);
            string sourceFilename = Path.GetFileNameWithoutExtension(filepath);

            List<string> existingBackups = FindExistingBackups(sourceFilename);

            //Handle existing backups
            if (existingBackups.Count > 0)
            {
                int backupCountOverMaximum = Math.Max(0, existingBackups.Count - AllowedBackupsPerFile);

                for (int i = 0; i < existingBackups.Count; i++)
                {
                    string backup = existingBackups[i];

                    if (backupCountOverMaximum > 0)
                    {
                        Helpers.FileSystemUtils.SafeDeleteFile(backup);
                        backupCountOverMaximum--;
                        continue;
                    }

                    if (i < AllowedBackupsPerFile) //Renames existing backup by changing its number by one 
                        Helpers.FileSystemUtils.SafeMoveFile(backup, formatBackupPath(sourceFilename, sourceFileExt, i + 1), 3);
                    else
                        Helpers.FileSystemUtils.SafeDeleteFile(backup); //The backup at the max count simply gets removed
                }
            }
            else //Create the first backup
            {
                Helpers.FileSystemUtils.SafeMoveFile(filepath, formatBackupPath(sourceFilename, sourceFileExt, 1), 3);
            }
        }

        private string formatBackupPath(string sourceFilename, string sourceExtension, int backupNumber)
        {
            return Path.Combine(BackupPath, sourceFilename + "_" + backupNumber + sourceExtension);
        }

        public List<string> FindExistingBackups(string backupName)
        {
            List<string> existingBackups = new List<string>(AllowedBackupsPerFile);
            List<int> existingBackupIndexes = new List<int>(AllowedBackupsPerFile);

            for (int i = 0; i < BackupFilesTemp.Length; i++)
            {
                string backupPath = BackupFilesTemp[i];
                string backupFile = Path.GetFileNameWithoutExtension(backupPath);

                if (backupFile.StartsWith(backupName + "_")) //Look for the format '<file>_<number>'
                {
                    int backupNumber; //Not zero-based
                    if (int.TryParse(backupFile.Last().ToString(), out backupNumber)) //Leave malformatted backups alone
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
        /// Call this when the backup process is finished to release temp references used during the backup process
        /// </summary>
        public void Finish()
        {
            BackupFilesTemp = null;
        }

        private string formatBackupName(string backupName, int value)
        {
            return backupName + "_" + value;
        }
    }

    public class Backup
    {
        public string Filename;
        public int MoveOffset;
    }
}
