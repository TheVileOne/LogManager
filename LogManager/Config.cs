using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogManager
{
    public static class Config
    {
        public static Configurable<bool> cfgUseAlternativeDirectory;
        public static OptionInterface.ConfigHolder ConfigData => Plugin.OptionInterface.config;


        public static void Initialize()
        {
            ConfigData.configurables.Clear();

            //Define config options
            cfgUseAlternativeDirectory = ConfigData.Bind("cfgUseAlternativeDirectory", false, new ConfigInfo("Choose your Logs folder", new object[]
            {
                "Prefer StreamingAssets folder for Logs directory"
            }));

            Plugin.OptionInterface.OnConfigChanged += OptionInterface_OnConfigChanged;
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
