using LogManager.Interface;
using LogManager.Settings;
using LogUtils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.IO;

namespace LogManager
{
    public partial class Plugin
    {
        internal void ApplyHooks()
        {
            try
            {
                On.RainWorld.OnModsInit += RainWorld_OnModsInit;
                On.RainWorld.Update += RainWorld_Update;
                On.Menu.ModdingMenu.Singal += ModdingMenu_Singal;
                On.Menu.Remix.MenuModList._ToggleMod += MenuModList_ToggleMod;
                On.Menu.ModdingMenu.ShutDownProcess += ModdingMenu_ShutDownProcess;
                IL.Expedition.ExpLog.Log += replaceLogPathHook_Expedition;
                IL.Expedition.ExpLog.LogOnce += replaceLogPathHook_Expedition;
                IL.Expedition.ExpLog.ClearLog += replaceLogPathHook_Expedition;
                IL.JollyCoop.JollyCustom.CreateJollyLog += replaceLogPathHook_JollyCoop;
                IL.JollyCoop.JollyCustom.Log += replaceLogPathHook_JollyCoop;
                IL.JollyCoop.JollyCustom.WriteToLog += replaceLogPathHook_JollyCoop;

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
            IL.RainWorld.HandleLog -= RainWorld_HandleLog;
            On.RainWorld.OnModsInit -= RainWorld_OnModsInit;
            On.RainWorld.Update -= RainWorld_Update;
            On.Menu.ModdingMenu.Singal -= ModdingMenu_Singal;
            On.Menu.Remix.MenuModList._ToggleMod -= MenuModList_ToggleMod;
            On.Menu.ModdingMenu.ShutDownProcess -= ModdingMenu_ShutDownProcess;
            IL.Expedition.ExpLog.Log -= replaceLogPathHook_Expedition;
            IL.Expedition.ExpLog.LogOnce -= replaceLogPathHook_Expedition;
            IL.Expedition.ExpLog.ClearLog -= replaceLogPathHook_Expedition;
            IL.JollyCoop.JollyCustom.CreateJollyLog -= replaceLogPathHook_JollyCoop;
            IL.JollyCoop.JollyCustom.Log -= replaceLogPathHook_JollyCoop;
            IL.JollyCoop.JollyCustom.WriteToLog -= replaceLogPathHook_JollyCoop;

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
                    BackupManager.SaveListsToFile();
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

        private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
        {
            if (PendingMoveUpdate)
            {
                if (moveAttempts < moveAttemptsAllowed)
                {
                    bool attemptCopyMethod = moveAttempts >= 5;

                    if (tryChangeDirectory(attemptCopyMethod))
                    {
                        Logger.LogInfo($"Move completed in {moveAttempts} attempts");

                        moveAttempts = 0;

                        LogsFolder.Path = PendingLogPath;//Path.GetDirectoryName(Listener.LogFullPath);
                        FileSwitcher.UpdateTogglePath(PendingLogPath);

                        Listener.Signal("MoveComplete", PendingLogPath);
                        PendingLogPath = null;
                    }
                    else
                    {
                        moveAttempts++;
                    }
                }
                else //Nothing can be logged while a move is pending
                {
                    Logger.LogInfo("Move attempt failed");

                    Logger.LogInfo("Default Directory exists: " + Directory.Exists(LogsFolder.DefaultPath));
                    Logger.LogInfo("Alternative Directory exists: " + Directory.Exists(LogsFolder.AlternativePath));
                    Logger.LogInfo("Current Directory exists: " + Directory.Exists(Listener.LogFullPath));

                    Listener.OpenFileStream(false);
                    Listener.Signal("MoveAborted");

                    moveAttempts = 0;
                    PendingLogPath = null;
                }
            }
            else if (Listener.GetSignal() != "Signal.None")
            {
                Listener.Signal("None");
            }

            if (PendingDeleteUpdate) //Delete is already attempted during a move
                ensureSingleLogsFolder();

            //ensureLogsFolderExists();
            orig(self);
        }

        private void ModdingMenu_ShutDownProcess(On.Menu.ModdingMenu.orig_ShutDownProcess orig, Menu.ModdingMenu self)
        {
            Listener.CloseWriter();
            Listener.Signal("MovePending"); //No move will happen, but we need other mods to turn off their filestreams
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

        private void RainWorld_HandleLog(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            //Replace all instances of exceptionLog.txt with a full path version
            int entriesToFind = 2;
            while (entriesToFind > 0 && cursor.TryGotoNext(MoveType.After, x => x.MatchLdstr("exceptionLog.txt")))
            {
                cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Ldstr, "exception.log");
                cursor.Emit(OpCodes.Ldc_I4_1); //Push a true value on the stack to satisfy second argument
                cursor.EmitDelegate(LogManager.Logger.ApplyLogPathToFilename);
                entriesToFind--;
            }

            if (entriesToFind > 0)
                Logger.LogError("IL hook couldn't find exceptionLog.txt");

            //Replace a single instance of consoleLog.txt with a full path version
            if (cursor.TryGotoNext(MoveType.After, x => x.MatchLdstr("consoleLog.txt")))
            {
                cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Ldstr, "console.log");
                cursor.Emit(OpCodes.Ldc_I4_1); //Push a true value on the stack to satisfy second argument
                cursor.EmitDelegate(LogManager.Logger.ApplyLogPathToFilename);
            }
            else
            {
                Logger.LogError("IL hook couldn't find consoleLog.txt");
            }
        }

        private void replaceLogPathHook_Expedition(ILContext il)
        {
            replaceLogPath(il, "expedition.log");
        }

        private void replaceLogPathHook_JollyCoop(ILContext il)
        {
            replaceLogPath(il, "jolly.log");
        }

        private void replaceLogPath(ILContext il, string newFilename)
        {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext(MoveType.After, x => x.MatchCall(typeof(RWCustom.Custom).GetMethod("RootFolderDirectory"))))
            {
                cursor.Emit(OpCodes.Pop); //Get method return value off the stack
                cursor.Emit(OpCodes.Ldsfld, typeof(Logger).GetField("BaseDirectory")); //Load new path onto stack
                cursor.GotoNext(MoveType.After, x => x.Match(OpCodes.Ldstr));
                cursor.Emit(OpCodes.Pop); //Replace filename extension with new one
                cursor.Emit(OpCodes.Ldstr, newFilename);
            }
            else
            {
                Logger.LogError("Expected directory IL could not be found");
            }
        }
    }
}
