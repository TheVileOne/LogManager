﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace LogManager.Helpers
{
    public static class FileSystemUtils
    {
        public static void SafeDeleteFile(string path, string customErrorMsg = null)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogError(customErrorMsg ?? "Unable to delete file");
                Debug.LogError(ex);
            }
        }

        public static void SafeDeleteDirectory(string path, bool deleteOnlyIfEmpty, string customErrorMsg = null)
        {
            try
            {
                if (Directory.Exists(path) && (!deleteOnlyIfEmpty || Directory.GetFiles(path).Length == 0))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(customErrorMsg ?? "Unable to delete directory");
                Debug.LogError(ex);
            }
        }

        public static void SafeDeleteDirectory(string path, string customErrorMsg = null)
        {
            SafeDeleteDirectory(path, false, customErrorMsg);
        }

        public static bool SafeCopyFile(string sourcePath, string destPath, int attemptsAllowed = 1)
        {
            string sourceFilename = Path.GetFileName(sourcePath);
            string destFilename = Path.GetFileName(destPath);

            Plugin.Logger.LogInfo($"Copying {sourceFilename} to {destFilename}");

            bool destEmpty = !File.Exists(destPath);
            bool exceptionLogged = false;
            while (attemptsAllowed > 0)
            {
                try
                {
                    //Make sure destination is clear
                    /*if (!destEmpty)
                    {
                        SafeDeleteFile(destPath);

                        if (File.Exists(destPath)) //File removal failed
                        {
                            attemptsAllowed--;
                            continue;
                        }
                        destEmpty = true;
                    }*/

                    File.Copy(sourcePath, destPath, true);
                    return true;
                }
                catch (FileNotFoundException)
                {
                    Plugin.Logger.LogError($"ERROR: Copy target file {sourceFilename} could not be found");
                    return false;
                }
                catch (IOException ioex)
                {
                    if (ioex.Message.StartsWith("Sharing violation"))
                        Plugin.Logger.LogError($"ERROR: Copy target file {sourceFilename} is currently in use");
                    handleException(ioex);
                }
                catch (Exception ex)
                {
                    handleException(ex);
                }
            }

            void handleException(Exception ex)
            {
                attemptsAllowed--;
                if (!exceptionLogged)
                {
                    Plugin.Logger.LogError(ex);
                    exceptionLogged = true;
                }
            }

            return false;
        }

        public static bool SafeMoveFile(string sourcePath, string destPath, int attemptsAllowed = 1)
        {
            string sourceFilename = Path.GetFileName(sourcePath);
            string destFilename = Path.GetFileName(destPath);

            Plugin.Logger.LogInfo($"Moving {sourceFilename} to {destFilename}");

            if (sourcePath == destPath)
            {
                Plugin.Logger.LogInfo($"Same filepath for {sourceFilename}");
                return true;
            }

            bool destEmpty = !File.Exists(destPath);
            bool exceptionLogged = false;
            while (attemptsAllowed > 0)
            {
                try
                {
                    //Make sure destination is clear
                    if (!destEmpty)
                    {
                        SafeDeleteFile(destPath);

                        if (File.Exists(destPath)) //File removal failed
                        {
                            attemptsAllowed--;
                            continue;
                        }
                        destEmpty = true;
                    }

                    File.Move(sourcePath, destPath);
                    return true;
                }
                catch (FileNotFoundException)
                {
                    Plugin.Logger.LogError($"ERROR: Move target file {sourceFilename} could not be found");
                    return false;
                }
                catch (IOException ioex)
                {
                    if (ioex.Message.StartsWith("Sharing violation"))
                        Plugin.Logger.LogError($"ERROR: Move target file {sourceFilename} is currently in use");
                    handleException(ioex);
                }
                catch (Exception ex)
                {
                    handleException(ex);
                }
            }

            void handleException(Exception ex)
            {
                attemptsAllowed--;
                if (!exceptionLogged)
                {
                    Plugin.Logger.LogError(ex);
                    exceptionLogged = true;
                }
            }

            return false;
        }

        public static void SafeWriteToFile(string filePath, List<string> values)
        {
            bool fileWriteSuccess = false;
            Exception fileWriteError = null;

            try
            {
                using (TextWriter writer = File.CreateText(filePath))
                {
                    foreach (string entry in values)
                        writer.WriteLine(entry);
                    writer.Close();
                }

                fileWriteSuccess = File.Exists(filePath);
            }
            catch (Exception ex)
            {
                fileWriteError = ex;
            }

            if (!fileWriteSuccess)
            {
                Plugin.Logger.LogError("Unable to write to file " + filePath);

                if (fileWriteError != null)
                    Plugin.Logger.LogError(fileWriteError);
            }
        }

        public static int DirectoryFileCount(string path)
        {
            return Directory.Exists(path) ? Directory.GetFiles(path).Length : 0;
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            List<string> failedToCopy = new List<string>();

            // Get information about the source directory
            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            //Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            //Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            //Create the destination directory
            Directory.CreateDirectory(destinationDir);

            //Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                try
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetFilePath);
                }
                catch
                {
                    failedToCopy.Add(file.Name);
                }
            }

            //If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }

            Plugin.Logger.LogInfo(failedToCopy + " failed to copy");

            foreach (string file in failedToCopy)
                Plugin.Logger.LogInfo(file);
        }

        public static bool BackupFile(string path)
        {
            //Find our backup path
            string backupPath = Path.ChangeExtension(path, ".bkp");

            try
            {
                File.Copy(path, backupPath, true);
                Debug.Log("Log Backup successful");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("Log Backup failed");
                Debug.LogError(ex);
                return false;
            }
        }

        public static bool RestoreBackup(string path)
        {
            //Find our backup path
            string backupPath = Path.ChangeExtension(path, ".bkp");

            try
            {
                File.Copy(backupPath, path, true);
                Plugin.Logger.LogInfo("Log Backup restored");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Log Backup restore failed");
                Plugin.Logger.LogError(ex);
                return false;
            }
        }
    }
}
