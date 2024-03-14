using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogManager.Interface
{
    public static class Config
    {
        public static Configurable<bool> cfgUseAlternativeDirectory;

        public static bool cfgUseAlternativeDirectoryModified = false;

        public static void Initialize()
        {
            Plugin.OptionInterface.config.configurables.Clear();

            //Define config options
            cfgUseAlternativeDirectory = Plugin.OptionInterface.config.Bind("cfgUseAlternativeDirectory", false, new ConfigurableInfo("Choose your Logs folder", null, string.Empty, new object[]
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
    }
}
