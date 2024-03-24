using LogManager.Helpers;
using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            initializePrimaryOptions(tab);
            initializeBackupOptions(tab);
            HasInitialized = true;
        }

        private void initializePrimaryOptions(OpTab tab)
        {
            //Create elements
            OpLabel tabHeader = new OpLabel(new Vector2(150f, y_offset - 40f), new Vector2(300f, 30f), Translate(Headers.PRIMARY), FLabelAlignment.Center, true, null);

            OpCheckBox directoryOptionToggle = createCheckBox(Config.cfgUseAlternativeDirectory, new Vector2(x_left_align, y_offset - 90f));
            OpLabel directoryOptionLabel = createOptionLabel(directoryOptionToggle);

            //Add elements to container
            tab.AddItems(new UIelement[]
            {
                tabHeader,
                directoryOptionToggle,
                directoryOptionLabel,
            });
        }

        private void initializeBackupOptions(OpTab tab)
        {
            float headerOffsetY = y_offset - 150f;

            //Upper left section
            OpLabel backupsHeader = new OpLabel(new Vector2(x_left_align, headerOffsetY), new Vector2(300f, 30f), Translate(Headers.BACKUPS), FLabelAlignment.Left, true, null);

            OpCheckBox enableBackupsToggle = createCheckBox(Config.cfgAllowBackups, new Vector2(x_left_align, headerOffsetY - 40f));
            OpLabel enableBackupsLabel = createOptionLabel(enableBackupsToggle);

            OpCheckBox progressiveBackupsToggle = createCheckBox(Config.cfgAllowProgressiveBackups, new Vector2(x_left_align, headerOffsetY - 80f));
            OpLabel progressiveBackupsLabel = createOptionLabel(progressiveBackupsToggle);

            //Right section
            OpUpdown backupLimitUpDown = new OpUpdown(Config.cfgBackupsPerFile, new Vector2(x_right_align + 70f, headerOffsetY - 40f), 50f)
            {
                description = Translate(ModConsts.Config.Descriptions.BACKUPS_PER_FILE),
            };
            OpLabel backupLimitLabel = createOptionLabel(backupLimitUpDown, new Vector2(x_right_align - 80f, headerOffsetY - 35f));           

            OpSimpleButton backupDeleteButton = new OpSimpleButton(new Vector2(x_right_align, headerOffsetY - 80f), new Vector2(120f, 30f), Translate(ModConsts.Config.OptionLabels.DELETE_OPTION))
            {
                description = Translate(ModConsts.Config.Descriptions.DELETE_OPTION)
            };

            backupDeleteButton.OnClick += BackupDeleteButton_OnClick;

            //Add elements to container
            tab.AddItems(new UIelement[]
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

            tab.AddItems(backupsAllowedHeader);
        }

        private void BackupDeleteButton_OnClick(UIfocusable trigger)
        {
            Plugin.Logger.LogInfo("Deleting backups");
            FileSystemUtils.SafeDeleteDirectory(Path.Combine(Logger.BaseDirectory, "Backup"));
        }

        /// <summary>
        /// Removes existing enable backup options replacing them with a new set based on BackupEntries list
        /// </summary>
        public void ProcessBackupEnableOptions()
        {
            OpTab tab = backupElementsTab;

            if (Plugin.BackupManager.HasRunOnce && BackupElements.Count > 0) //Sanity check
            {
                Plugin.Logger.LogInfo($"Replacing {BackupElements.Count} backup options");

                //Remove existing items from tab
                for (int i = 0; i < BackupElements.Count; i++)
                {
                    var backupElementTuple = BackupElements[i];
                    var backupConfigurable = Config.cfgBackupEntries[i];

                    tab.RemoveItems(backupElementTuple.Item1, backupElementTuple.Item2);
                    Config.ConfigData.configurables.Remove("bkp" + backupConfigurable.info.Tags[0]); //Recreates key from Tags
                }

                BackupElements.Clear();
                Config.cfgBackupEntries.Clear();
            }
            else
                Plugin.Logger.LogInfo($"Initilizing backup options");

            //Create a backup element option for each backup entry
            for (int i = 0; i < Plugin.BackupManager.BackupEntries.Count; i++)
            {
                var backupEntry = Plugin.BackupManager.BackupEntries[i];
                bool backupEnabledByDefault = Plugin.BackupManager.ProgressiveEnableMode;

                var backupConfigurable = Config.ConfigData.Bind("bkp" + backupEntry.Item1, backupEnabledByDefault,
                    new Config.ConfigInfo(ModConsts.Config.Descriptions.BACKUPS_ENABLED_LIST, new object[]
                {
                    backupEntry.Item1
                }));

                backupConfigurable.Value = backupEntry.Item2;

                Config.cfgBackupEntries.Add(backupConfigurable);

                OpCheckBox checkBox = createCheckBox(backupConfigurable, new Vector2(x_left_align, backupsAllowedHeader.PosY - (40f * (i + 1))));
                OpLabel optionLabel = createOptionLabel(checkBox);

                tab.AddItems(checkBox, optionLabel);
                BackupElements.Add((checkBox, optionLabel));
            }

            Plugin.Logger.LogInfo($"Processed {BackupElements.Count} backup options");
        }

        /// <summary>
        /// Creates the elements used by the Remix menu interface to produce a standard OpCheckBox
        /// </summary>
        private OpCheckBox createCheckBox(Configurable<bool> configurable, Vector2 position)
        {
            return new OpCheckBox(configurable, position)
            {
                description = Translate(Config.GetDescription(configurable))
            };
        }

        private OpLabel createOptionLabel(UIconfig owner)
        {
            return createOptionLabel(owner, new Vector2(60f, owner.ScreenPos.y));
        }

        private OpLabel createOptionLabel(UIconfig owner, Vector2 pos)
        {
            return new OpLabel(pos.x, pos.y, Translate(Config.GetOptionLabel(owner.cfgEntry)), false)
            {
                bumpBehav = owner.bumpBehav,
                description = owner.description
            };
        }
    }
}
