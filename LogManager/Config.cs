using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogManager
{
    public static class Config
    {
        public static OptionInterface.ConfigHolder ConfigData => Plugin.OptionInterface.config;

        public static Configurable<bool> cfgUseAlternativeDirectory;
        public static Configurable<bool> cfgAllowBackups;
        public static Configurable<bool> cfgAllowProgressiveBackups;

        public static void Initialize()
        {
            ConfigData.configurables.Clear();

            //Define config options
            cfgUseAlternativeDirectory = ConfigData.Bind("cfgUseAlternativeDirectory", false, new ConfigInfo("Choose your Logs folder", new object[]
            {
                "Prefer StreamingAssets folder for Logs directory"
            }));

            cfgAllowBackups = ConfigData.Bind("cfgAllowBackups", false, new ConfigInfo("Allow log backups", new object[]
            {
                "Backup log files when Rain World starts"
            }));

            cfgAllowProgressiveBackups = ConfigData.Bind("cfgAllowProgressiveBackups", false, new ConfigInfo("Enable backups for newly detected log files on startup", new object[]
            {
                "Automatically enable backups for newly detected log files"
            }));

            Plugin.OptionInterface.OnConfigChanged += OptionInterface_OnConfigChanged;
        }

        /// <summary>
        /// Gets the string that appears on the label associated with a config option
        /// </summary>
        public static string GetDescription(Configurable<bool> option)
        {
            return option.info.description;
        }

        /// <summary>
        /// Gets the string that appears on the bottom of the screen and describes the function of the config option when hovered
        /// </summary>
        public static string GetTooltip(Configurable<bool> option)
        {
            return option.info.Tags[0] as string;
        }

        private static void OptionInterface_OnConfigChanged()
        {
            try
            {
                Plugin.UpdateLogDirectory();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
            }
        }

        public class ConfigInfo : ConfigurableInfo
        {
            public ConfigInfo(string description, params object[] tags) : base(description, null, string.Empty, tags)
            {
            }
        }
    }
}
