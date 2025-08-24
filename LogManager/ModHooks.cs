using LogManager.Interface;
using LogManager.Settings;
using LogUtils;
using Menu.Remix;
using Menu.Remix.MixedUI;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using ConfigHolder = OptionInterface.ConfigHolder;

namespace LogManager
{
    public partial class Plugin
    {
        private static Hook cosmeticFlagHook;

        internal void ApplyHooks()
        {
            try
            {
                On.RainWorld.OnModsInit += RainWorld_OnModsInit;
                On.Menu.ModdingMenu.Singal += ModdingMenu_Singal;
                On.Menu.Remix.MenuModList._ToggleMod += MenuModList_ToggleMod;
                On.Menu.Remix.MixedUI.UIconfig.ShowConfig += UIconfig_ShowConfig;
                On.Menu.Remix.ConfigContainer.NotifyConfigChange += ConfigContainer_NotifyConfigChange;

                //Config processing hooks
                On.OptionInterface.ConfigHolder.Save += ConfigSaveHook;
                On.OptionInterface.ConfigHolder.Reload += ConfigLoadHook;

                MethodInfo method = typeof(ConfigurableBase).GetProperty(nameof(ConfigurableBase.IsCosmetic)).GetGetMethod();
                cosmeticFlagHook = new Hook(method, ignoreCosmeticFlag);
                cosmeticFlagHook.Apply();

                static bool ignoreCosmeticFlag(Func<ConfigurableBase, bool> orig, ConfigurableBase self)
                {
                    //LogManager doesn't use cosmetic configurables. Ignore any naming conventions forcing an unwanted cosmetic status
                    if (self.info.ContainsTag(PLUGIN_GUID))
                        return false;

                    return orig(self);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while applying hooks");
                Logger.LogError(ex);
            }
        }

        internal void RemoveHooks()
        {
            On.RainWorld.OnModsInit -= RainWorld_OnModsInit;
            On.Menu.ModdingMenu.Singal -= ModdingMenu_Singal;
            On.Menu.Remix.MenuModList._ToggleMod -= MenuModList_ToggleMod;
            On.Menu.Remix.MixedUI.UIconfig.ShowConfig -= UIconfig_ShowConfig;
            On.Menu.Remix.ConfigContainer.NotifyConfigChange -= ConfigContainer_NotifyConfigChange;

            //Config processing hooks
            On.OptionInterface.ConfigHolder.Save -= ConfigSaveHook;
            On.OptionInterface.ConfigHolder.Reload -= ConfigLoadHook;

            cosmeticFlagHook.Free();
        }

        private void ConfigSaveHook(On.OptionInterface.ConfigHolder.orig_Save orig, ConfigHolder self)
        {
            if (hasInitialized && self.owner == OptionInterface)
            {
                Logger.LogInfo("Saving log manager config");
                ConfigSettings.SaveInProgress = true;
            }

            try
            {
                orig(self);
            }
            finally
            {
                if (ConfigSettings.SaveInProgress)
                {
                    ConfigSettings.HandleBackupEnabledChanges();
                    BackupController.SaveListsToFile();
                }

                ConfigSettings.SaveInProgress = false;
            }
        }

        /// <summary>
        /// Ensures that the latest settings are retrieved from file
        /// </summary>
        private void ConfigLoadHook(On.OptionInterface.ConfigHolder.orig_Reload orig, OptionInterface.ConfigHolder self)
        {
            if (hasInitialized && self.owner == OptionInterface)
            {
                Logger.LogInfo("Loading log manager config");
                ConfigSettings.ReloadInProgress = true;

                try
                {
                    RefreshBackupController();
                    ConfigSettings.HandleBackupEnabledChanges();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error occurred while handling backups");
                    Logger.LogError(ex);
                }
            }

            orig(self);

            if (ConfigSettings.ReloadInProgress && OptionInterface.HasInitialized)
                OptionInterface.ProcessBackupEnableOptions();

            ConfigSettings.ReloadInProgress = false;
        }

        private void ModdingMenu_Singal(On.Menu.ModdingMenu.orig_Singal orig, Menu.ModdingMenu self, Menu.MenuObject sender, string message)
        {
            if (message == "EXIT")
            {
                //Application is not shutting down. No cleanup required
                shouldCleanUpOnDisable = false;
            }
            else if (message == "APPLYMODS" && shouldCleanUpOnDisable)
            {
                //If shutdown hook doesn't work, signal here instead
                //Listener.CloseWriter();
                //Listener.Signal("MovePending");
            }

            orig(self, sender, message);
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            try
            {
                if (OptionInterface == null)
                {
                    OptionInterface = new LoggerOptionInterface();
                    ConfigSettings.Initialize();
                }

                MachineConnector.SetRegisteredOI(PLUGIN_GUID, OptionInterface);
            }
            catch (Exception ex)
            {
                Logger.LogError("Config did not initialize properly");
                Logger.LogError(ex);
            }
        }

        /// <summary>
        /// A flag that indicates whether log files should be disposed of on mod disable/game shutdown
        /// </summary>
        private bool shouldCleanUpOnDisable = false;
        private void MenuModList_ToggleMod(On.Menu.Remix.MenuModList.orig__ToggleMod orig, MenuModList self, MenuModList.ModButton btn)
        {
            orig(self, btn);

            if (btn.itf.mod.id == PLUGIN_GUID)
                shouldCleanUpOnDisable = !btn.selectEnabled;
        }

        private void ConfigContainer_NotifyConfigChange(On.Menu.Remix.ConfigContainer.orig_NotifyConfigChange orig, ConfigContainer self, UIconfig config, string oldValue, string value)
        {
            if (!OptionInterface.AllowConfigHistoryUpdates && config.cfgEntry.OI == OptionInterface)
                return;

            //Make this config option immune to premature history changes
            if (config.cfgEntry == ConfigSettings.cfgLogsFolderPath)
            {
                var history = self._history;
                history.Push(new ConfigContainer.ConfigHistory
                {
                    config = config,
                    origValue = oldValue
                });
                return;
            }

            orig(self, config, oldValue, value);
        }

        private void UIconfig_ShowConfig(On.Menu.Remix.MixedUI.UIconfig.orig_ShowConfig orig, UIconfig self)
        {
            if (self.cfgEntry == ConfigSettings.cfgLogsFolderPath)
            {
                self.OnValueChanged -= LoggerOptionInterface.ConfirmPathChange;
                OptionInterface.AllowConfigHistoryUpdates = false;
            }

            orig(self);

            if (self.cfgEntry == ConfigSettings.cfgLogsFolderPath)
            {
                self.OnValueChanged += LoggerOptionInterface.ConfirmPathChange;
                OptionInterface.AllowConfigHistoryUpdates = true;

                OpComboBox optionsBox = (OpComboBox)self;

                //Make sure that the option is listed in the item list
                int optionIndex = optionsBox.GetIndex(LogsFolder.ContainingPath);

                //Use the value used in the list when it exists, or create a display friendly name using the containing path when it doesn't
                string selectedOption = optionIndex >= 0 ? optionsBox.GetItemList()[optionIndex].name : ConfigSettings.GetPathOptionName(LogsFolder.ContainingPath);

                self.ForceValue(selectedOption);
                self.lastValue = selectedOption;
            }
        }
    }
}
