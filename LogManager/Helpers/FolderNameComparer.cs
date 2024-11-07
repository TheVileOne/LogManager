using LogUtils.Helpers.Comparers;
using LogUtils.Helpers.FileHandling;
using System;

namespace LogManager.Helpers
{
    /// <summary>
    /// Class used to help sort options in the Remix menu by folder name when necessary 
    /// </summary>
    internal class FolderNameComparer : FilenameComparer
    {
        public const string CUSTOM_ROOT_FOLDER_NAME = "StreamingAssets";
        public const string ROOT_FOLDER_NAME = "Rain World";

        public FolderNameComparer() : base()
        {
            InnerComparer = StringComparer.OrdinalIgnoreCase;
            InnerEqualityComparer = StringComparer.OrdinalIgnoreCase;
        }

        public override int Compare(string path, string pathOther)
        {
            path = PathUtils.GetPathFromKeyword(path);
            pathOther = PathUtils.GetPathFromKeyword(pathOther);

            //Hacky solution for making the custom root compare lower than any other string
            //LogUtils associates null with the custom root anyways, so returning a match with other nulls isn't an issue
            if (PathUtils.ContainsDirectory(path, CUSTOM_ROOT_FOLDER_NAME, 1))
                path = null;
            else if (PathUtils.ContainsDirectory(path, ROOT_FOLDER_NAME, 1))
                path = "root";

            if (PathUtils.ContainsDirectory(pathOther, CUSTOM_ROOT_FOLDER_NAME, 1))
                pathOther = null;
            else if (PathUtils.ContainsDirectory(pathOther, ROOT_FOLDER_NAME, 1))
                pathOther = "root";

            return base.Compare(path, pathOther);
        }
    }
}
