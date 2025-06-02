using LogManager.Interface;
using LogManager.Settings;
using LogUtils;
using LogUtils.Helpers;
using System;
using System.Collections.Generic;

namespace LogManager
{
    public partial class Plugin
    {
        internal void ApplyHooks()
        {
            try
            {
                On.RainWorld.OnModsInit += RainWorld_OnModsInit;
                On.Menu.ModdingMenu.Singal += ModdingMenu_Singal;
                On.Menu.Remix.MenuModList._ToggleMod += MenuModList_ToggleMod;
                On.Menu.ModdingMenu.ShutDownProcess += ModdingMenu_ShutDownProcess;

                //Config processing hooks
                On.OptionInterface.ConfigHolder.Save += ConfigSaveHook;
                On.OptionInterface.ConfigHolder.Reload += ConfigLoadHook;
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
            On.Menu.ModdingMenu.ShutDownProcess -= ModdingMenu_ShutDownProcess;

            //Config processing hooks
            On.OptionInterface.ConfigHolder.Save -= ConfigSaveHook;
            On.OptionInterface.ConfigHolder.Reload -= ConfigLoadHook;
        }

        private void ConfigSaveHook(On.OptionInterface.ConfigHolder.orig_Save orig, OptionInterface.ConfigHolder self)
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

        private void ModdingMenu_ShutDownProcess(On.Menu.ModdingMenu.orig_ShutDownProcess orig, Menu.ModdingMenu self)
        {
            //TODO: Investigate why FileStreams needed to be closed here under the legacy system
            List<StreamResumer> resumeList = new List<StreamResumer>();
            foreach (PersistentLogFileHandle handle in LogFile.GetPersistentLogFiles())
                resumeList.Add(handle.InterruptStream());

            resumeList.ForEach(stream => stream.Resume());
            orig(self);
        }

        /// <summary>
        /// A flag that indicates whether log files should be disposed of on mod disable/game shutdown
        /// </summary>
        private bool shouldCleanUpOnDisable = false;
        private void MenuModList_ToggleMod(On.Menu.Remix.MenuModList.orig__ToggleMod orig, Menu.Remix.MenuModList self, Menu.Remix.MenuModList.ModButton btn)
        {
            orig(self, btn);

            if (btn.itf.mod.id == PLUGIN_GUID)
                shouldCleanUpOnDisable = !btn.selectEnabled;
        }
    }
}
