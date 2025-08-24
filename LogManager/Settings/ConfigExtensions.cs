using System;
using System.Linq;

namespace LogManager.Settings
{
    internal static class ConfigExtensions
    {
        public static bool ContainsTag(this ConfigurableInfo info, object tag)
        {
            return info != null && info.Tags.Contains(tag);
        }

        /// <summary>
        /// Gets the LogManager assigned data tag for this configurable
        /// </summary>
        /// <remarks>Extension method is safe to access without a null check</remarks>
        /// <returns>Returns the tag entry if found, null in all other cases</returns>
        public static object GetDataTag(this ConfigurableInfo info)
        {
            if (info == null || info.Tags.Length == 0)
                return null;

            //Find the first tag that does not match the mod identity tag
            return Array.Find(info.Tags, tag => !tag.Equals(Plugin.PLUGIN_GUID));
        }
    }
}
