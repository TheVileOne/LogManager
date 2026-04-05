using LogUtils.Helpers.FileHandling;
using System;
using System.Collections.Generic;

namespace LogManager.Helpers
{
    public class FolderPathMapper
    {
        /// <summary>
        /// A map of the currently used folders within the Backups directory
        /// </summary>
        public FolderPathNode PathMap;

        /// <summary>
        /// Component that translates a path to one located within the Backups directory
        /// </summary>
        public PathResolver Resolver;

        public FolderPathMapper(string folderPath)
        {
            PathMap = new FolderPathNode(folderPath);
            Resolver = new TempPathResolver(folderPath);
        }

        internal FolderPathMapper(string folderPath, FolderPathNode staleMap) : this(folderPath)
        {
            copyChildrenRecursive(PathMap, staleMap);

            void copyChildrenRecursive(FolderPathNode currentParent, FolderPathNode oldParent)
            {
                foreach (FolderPathNode oldChild in oldParent.Children)
                {
                    string currentPath = PathUtils.Rebase(oldChild.CurrentPath, staleMap.CurrentPath, folderPath);
                    FolderPathNode currentChild = new FolderPathNode(currentPath);

                    currentParent.Children.Add(currentChild);
                    copyChildrenRecursive(currentChild, oldChild);
                }
            }
        }

        public FolderPathNode Resolve(string path)
        {
            path = Resolver.Resolve(PathUtils.PathWithoutFilename(path));
            if (PathUtils.PathsAreEqual(PathMap.CurrentPath, path))
                return PathMap;

            FolderPathNode result = findChildMatch(PathMap, path);
            if (result == null)
            {
                result = PathMap;
            }
            else if (!PathUtils.PathsAreEqual(result.CurrentPath, path)) //Node found was a parent directory to the searched path
            {
                FolderPathNode parent = result,
                               child = new FolderPathNode(path);

                moveChildrenToNewParent(parent, child, node => PathUtils.ContainsOtherPath(node.CurrentPath, path));
                parent.Children.Add(child);
            }
            return result;
        }

        private void moveChildrenToNewParent(FolderPathNode oldParent, FolderPathNode newParent, Predicate<FolderPathNode> predicate)
        {
            newParent.Children.AddRange(oldParent.Children.FindAll(predicate));
            oldParent.Children.RemoveAll(predicate);
        }

        private FolderPathNode findChildMatch(FolderPathNode parent, string path)
        {
            FolderPathNode result = parent.Children.Find(node => PathUtils.ContainsOtherPath(path, node.CurrentPath));

            if (result != null)
            {
                FolderPathNode childResult = findChildMatch(result, path);
                if (childResult != null)
                    result = childResult;
            }
            return result;
        }
    }

    public class FolderPathNode
    {
        public readonly string CurrentPath;

        public readonly List<FolderPathNode> Children = new List<FolderPathNode>();

        public FolderPathNode(string folderPath)
        {
            CurrentPath = folderPath;
        }
    }
}
