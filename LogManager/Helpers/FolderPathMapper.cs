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

            FolderPathNode result;
            if (PathUtils.PathsAreEqual(PathMap.CurrentPath, path))
            {
                result = PathMap;
            }
            else
            {
                /*
                 * There are three possible outcomes.
                 * 
                 * I.   No child of the root path is found, and we need to add a new child to the root.
                 * II.  A child of the root, or one of its children is found that exactly matches the path.
                 * III. A child of the root, or one of its children is found that does not exactly match the path.
                 */
                result = findChildMatch(PathMap, path);

                if (result == null || !PathUtils.PathsAreEqual(result.CurrentPath, path)) //Node found was a parent directory to the searched path
                {
                    FolderPathNode parent = result ?? PathMap,
                                   child = new FolderPathNode(path);

                    moveChildrenToNewParent(parent, child, node => PathUtils.ContainsOtherPath(node.CurrentPath, path));

                    parent.Children.Add(child);
                    result = child;
                }
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
