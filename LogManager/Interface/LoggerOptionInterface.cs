using LogManager.Settings;
using LogUtils;
using LogUtils.Helpers.FileHandling;
using Menu.Remix;
using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using BackupEntry = (LogUtils.Enums.LogID ID, bool Enabled);
using Headers = LogManager.ModConsts.Config.Headers;
using Vector2 = UnityEngine.Vector2;

namespace LogManager.Interface
{
    public class LoggerOptionInterface : OptionInterface
    {
        public bool HasInitialized;

        /// <summary>
        /// The initial y-value by which all other controls are positioned from 
        /// </summary>
        private const float y_offset = 560f;

        /// <summary>
        /// The position by which controls are aligned along the x-axis on the left side
        /// </summary>
        private const float x_left_align = 20f;

        /// <summary>
        /// The position by which controls are aligned along the x-axis on the right side
        /// </summary>
        private const float x_right_align = 440f;

        public bool AllowConfigHistoryUpdates = true;

        public List<(OpCheckBox, OpLabel)> BackupElements = new List<(OpCheckBox, OpLabel)>();

        /// <summary>
        /// The tab that handles Backup related UIelements
        /// </summary>
        private OpTab backupElementsTab
        {
            get
            {
                if (Tabs != null)
                    return Tabs[0];
                return null;
            }
        }

        private OpLabel backupsAllowedHeader;

        public override void Initialize()
        {
            base.Initialize();

            Tabs = new OpTab[]
            {
                new OpTab(this, Translate("Options"))
            };

            OpTab tab = Tabs[0];

            List<UIelement> tabElements = new List<UIelement>();

            initializePrimaryOptions(tabElements);
            initializeBackupOptions(tabElements);

            tabElements.Reverse(); //Reverse the drawing order of all elements - elements at top of page should draw over elements lower on the page
            tab.AddItems(tabElements.ToArray());

            HasInitialized = true;
        }

        private void initializePrimaryOptions(List<UIelement> tabElements)
        {
            //Create elements
            OpLabel tabHeader = new OpLabel(new Vector2(150f, y_offset - 40f), new Vector2(300f, 30f), Translate(Headers.PRIMARY), FLabelAlignment.Center, true, null);

            OpComboBox logsFolderOptionsBox = new ComboBox(ConfigSettings.cfgLogsFolderPath, new Vector2(x_left_align, y_offset - 90f), 200f, createPathOptions())
            {
                description = Translate(ConfigSettings.GetDescription(ConfigSettings.cfgLogsFolderPath))
            };
            OpLabel logsFolderOptionsLabel = createOptionLabel(logsFolderOptionsBox, new Vector2(x_left_align, logsFolderOptionsBox.ScreenPos.y + 30f));

            logsFolderOptionsBox.OnValueChanged += ConfirmPathChange;

            //Add elements to container
            tabElements.AddRange(new UIelement[]
            {
                tabHeader,
                logsFolderOptionsBox,
                logsFolderOptionsLabel,
            });
        }

        internal static void ConfirmPathChange(UIconfig optionsBox, string selectedPath, string lastSelectedPath)
        {
            if (string.IsNullOrEmpty(selectedPath))
            {
                Plugin.Logger.LogWarning("Invalid path selected");
                CancelPathChange();
                return;
            }

            string displayPath = PathUtils.GetRelativePath(selectedPath, Plugin.GameRootPath, true);
            ConfigConnector.CreateDialogBoxYesNo(
                "Logs folder path will be changed.\n\n" +
               $"Selected Path: {displayPath}\n\n" +
                "Do you accept this change?", AcceptPathChange, CancelPathChange);

            void AcceptPathChange()
            {
                LogsFolder.SetContainingPath(selectedPath);

                //Check that the path has actually changed, and if the process fails cancel the action
                if (!PathUtils.PathsAreEqual(selectedPath, LogsFolder.ContainingPath))
                {
                    Plugin.Logger.LogWarning("Failed to change logs folder path");
                    CancelPathChange();
                }
            }

            void CancelPathChange()
            {
                Plugin.Logger.Log("Cancelling path change");
                optionsBox.OnValueChanged -= ConfirmPathChange; //Undo will activate events, and we do not want to show the dialog a second time
                undoLastChange();
                optionsBox.OnValueChanged += ConfirmPathChange;
            }
        }

        private void initializeBackupOptions(List<UIelement> tabElements)
        {
            float headerOffsetY = y_offset - 150f;

            //Upper left section
            OpLabel backupsHeader = new OpLabel(new Vector2(x_left_align, headerOffsetY), new Vector2(300f, 30f), Translate(Headers.BACKUPS), FLabelAlignment.Left, true, null);

            OpCheckBox enableBackupsToggle = createCheckBox(ConfigSettings.cfgAllowBackups, new Vector2(x_left_align, headerOffsetY - 40f));
            OpLabel enableBackupsLabel = createOptionLabel(enableBackupsToggle);

            OpCheckBox progressiveBackupsToggle = createCheckBox(ConfigSettings.cfgAllowProgressiveBackups, new Vector2(x_left_align, headerOffsetY - 80f));
            OpLabel progressiveBackupsLabel = createOptionLabel(progressiveBackupsToggle);

            //Right section
            OpUpdown backupLimitUpDown = new OpUpdown(ConfigSettings.cfgBackupsPerFile, new Vector2(x_right_align + 70f, headerOffsetY - 40f), 50f)
            {
                description = Translate(ModConsts.Config.Descriptions.BACKUPS_PER_FILE),
            };
            OpLabel backupLimitLabel = createOptionLabel(backupLimitUpDown, new Vector2(x_right_align - 80f, headerOffsetY - 35f));

            OpSimpleButton backupDeleteButton = new OpSimpleButton(new Vector2(x_right_align, headerOffsetY - 80f), new Vector2(120f, 30f), Translate(ModConsts.Config.OptionLabels.DELETE_OPTION))
            {
                description = Translate(ModConsts.Config.Descriptions.DELETE_OPTION)
            };

            backupDeleteButton.OnClick += ConfirmBackupRemoval;

            //Add elements to container
            tabElements.AddRange(new UIelement[]
            {
                backupsHeader,
                enableBackupsToggle,
                enableBackupsLabel,
                progressiveBackupsToggle,
                progressiveBackupsLabel,
                backupLimitUpDown,
                backupLimitLabel,
                backupDeleteButton,
            });

            headerOffsetY = progressiveBackupsToggle.PosY - 40f;

            //Lower left section
            backupsAllowedHeader = new OpLabel(new Vector2(x_left_align, headerOffsetY), new Vector2(300f, 30f), Translate(Headers.BACKUPS_ENABLED_LIST), FLabelAlignment.Left, true, null);

            tabElements.Add(backupsAllowedHeader);
        }

        private void ConfirmBackupRemoval(UIfocusable trigger)
        {
            ConfigConnector.CreateDialogBoxYesNo(
                "Are you sure you want to permanently delete the Backup directory and its contents?",
                RemoveBackups, Cancel);

            void RemoveBackups()
            {
                Plugin.Logger.LogInfo("Removing all backups");
                DirectoryUtils.SafeDelete(Plugin.BackupController.BackupPath);
            }

            void Cancel()
            {
                //Do nothing
            }
        }

        /// <summary>
        /// Removes existing enable backup options replacing them with a new set based on BackupEntries list
        /// </summary>
        public void ProcessBackupEnableOptions()
        {
            OpTab tab = backupElementsTab;
            var configurables = ConfigSettings.ConfigData.configurables;

            if (BackupElements.Count > 0) //Sanity check
            {
                Plugin.Logger.LogInfo($"Replacing {BackupElements.Count} backup options");

                //Remove existing items from tab
                for (int i = 0; i < BackupElements.Count; i++)
                {
                    var backupElementTuple = BackupElements[i];
                    var backupConfigurable = ConfigSettings.cfgBackupEntries[i];

                    tab.RemoveItems(backupElementTuple.Item1, backupElementTuple.Item2);
                    configurables.Remove("bkp" + backupConfigurable.info.GetDataTag()); //Recreates key from Tags
                }

                BackupElements.Clear();
                ConfigSettings.cfgBackupEntries.Clear();
            }
            else
                Plugin.Logger.LogInfo($"Initilizing backup options");

            //Create a backup element option for each backup entry
            for (int i = 0; i < Plugin.BackupController.BackupEntries.Count; i++)
            {
                var backupEntry = Plugin.BackupController.BackupEntries[i];
                bool backupEnabledByDefault = Plugin.BackupController.ProgressiveEnableMode;

                string entryKey = "bkp" + backupEntry.ID;

                if (configurables.ContainsKey(entryKey))
                {
                    Plugin.Logger.LogWarning($"Backup entry {entryKey} already exists");
                    configurables.Remove(entryKey);
                }

                Configurable<bool> backupConfigurable = createBackupConfigurable(entryKey, backupEntry);

                if (backupConfigurable == null)
                {
                    Plugin.Logger.LogWarning($"Unable to create backup configurable for {backupEntry.ID}");
                    continue;
                }

                ConfigSettings.cfgBackupEntries.Add(backupConfigurable);

                OpCheckBox checkBox = createCheckBox(backupConfigurable, new Vector2(x_left_align, backupsAllowedHeader.PosY - (40f * (i + 1))));
                OpLabel optionLabel = createOptionLabel(checkBox);

                tab.AddItems(checkBox, optionLabel);
                BackupElements.Add((checkBox, optionLabel));
            }

            Plugin.Logger.LogInfo($"Processed {BackupElements.Count} backup options");
        }

        private static Configurable<bool> createBackupConfigurable(string entryName, BackupEntry backupEntry)
        {
            object[] entryTags =
            {
                backupEntry.ID,    //Tracks LogID
                Plugin.PLUGIN_GUID //Tags mod identity
            };
            bool backupEnabledByDefault = Plugin.BackupController.ProgressiveEnableMode;

            ConfigHolder config = ConfigSettings.ConfigData;
            Configurable<bool> configEntry = null;
            ConfigurableInfo configInfo = new ConfigSettings.ConfigInfo(ModConsts.Config.Descriptions.BACKUPS_ENABLED_LIST, entryTags);

            try
            {
                configEntry = config.Bind(entryName, backupEnabledByDefault, configInfo);
                configEntry.Value = backupEntry.Enabled;
            }
            catch (ArgumentException) //Probably means we are dealing with a filename with characters not allowed as config entry key
            {
                //Bind method cannot be used for this method. LogManager will handle binding this config entry instead.
                configEntry = new Configurable<bool>(Plugin.OptionInterface, entryName, backupEnabledByDefault, configInfo);

                //Stray configurables is probably not applicable here
                if (config.strayConfigurables.ContainsKey(entryName))
                {
                    try
                    {
                        configEntry.Value = ValueConverter.ConvertToValue<bool>(config.strayConfigurables[entryName]);
                    }
                    catch (Exception ex)
                    {
                        MachineConnector.LogWarning(string.Format("[{0}] Failed to parse \"{1}\" in {2}! Using default value.",
                                entryName, config.strayConfigurables[entryName], configEntry.settingType));
                        MachineConnector.LogWarning(ex);
                        configEntry.Value = ValueConverter.ConvertToValue<bool>(configEntry.defaultValue);
                    }
                    config.strayConfigurables.Remove(entryName);
                }
                config.configurables.Add(entryName, configEntry);

                if (config.pendingReset)
                    configEntry.Value = ValueConverter.ConvertToValue<bool>(configEntry.defaultValue);
            }
            return configEntry;
        }

        /// <summary>
        /// Creates the elements used by the Remix menu interface to produce a standard OpCheckBox
        /// </summary>
        private OpCheckBox createCheckBox(Configurable<bool> configurable, Vector2 position)
        {
            return new OpCheckBox(configurable, position)
            {
                description = Translate(ConfigSettings.GetDescription(configurable))
            };
        }

        private List<ListItem> createPathOptions()
        {
            List<ListItem> options = new List<ListItem>();

            foreach (string path in LogsFolder.AvailablePaths)
            {
                string sortKey = PathUtils.Normalize(path);
                options.Add(new ListItem(sortKey, ConfigSettings.GetPathOptionName(path)));
            }
            return options;
        }

        private OpLabel createOptionLabel(UIconfig owner)
        {
            return createOptionLabel(owner, new Vector2(60f, owner.ScreenPos.y));
        }

        private OpLabel createOptionLabel(UIconfig owner, Vector2 pos)
        {
            return new OpLabel(pos.x, pos.y, Translate(ConfigSettings.GetOptionLabel(owner.cfgEntry)), false)
            {
                bumpBehav = owner.bumpBehav,
                description = owner.description
            };
        }

        private static void undoLastChange()
        {
            ConfigContainer.instance._UndoConfigChange();
        }
    }
}
