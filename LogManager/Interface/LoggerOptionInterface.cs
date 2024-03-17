﻿using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vector2 = UnityEngine.Vector2;

namespace LogManager.Interface
{
    internal class LoggerOptionInterface : OptionInterface
    {
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
        }

        private void initializePrimaryOptions(OpTab tab)
        {
            //Create elements
            OpLabel tabHeader = new OpLabel(new Vector2(150f, y_offset - 40f), new Vector2(300f, 30f), Translate("Logging Tools"), FLabelAlignment.Center, true, null);

            OpCheckBox directoryOptionToggle = createCheckBox(Config.cfgUseAlternativeDirectory, new Vector2(x_left_align, y_offset - 90f));
            OpLabel directoryOptionTooltip = createTooltip(directoryOptionToggle);

            //Add elements to container
            tab.AddItems(new UIelement[]
            {
                tabHeader,
                directoryOptionToggle,
                directoryOptionTooltip,
            });
        }

        private void initializeBackupOptions(OpTab tab)
        {
            float headerOffsetY = y_offset - 150f;

            //Upper left section
            OpLabel backupsHeader = new OpLabel(new Vector2(x_left_align, headerOffsetY), new Vector2(300f, 30f), Translate("Backup Management"), FLabelAlignment.Left, true, null);

            OpCheckBox enableBackupsToggle = createCheckBox(Config.cfgAllowBackups, new Vector2(x_left_align, headerOffsetY - 40f));
            OpLabel enableBackupsTooltip = createTooltip(enableBackupsToggle);

            OpCheckBox progressiveBackupsToggle = createCheckBox(Config.cfgAllowProgressiveBackups, new Vector2(x_left_align, headerOffsetY - 80f));
            OpLabel progressiveBackupsTooltip = createTooltip(progressiveBackupsToggle);

            //Right section
            OpUpdown backupLimitUpDown = new OpUpdown(Config.cfgBackupsPerFile, new Vector2(x_right_align + 80f, headerOffsetY - 40f), 0)
            {
                description = Translate("Backups per file"),
            };
            OpLabel backupLimitTooltip = createTooltip(backupLimitUpDown, new Vector2(x_right_align - 60f, headerOffsetY - 35f));           

            OpSimpleButton backupDeleteButton = new OpSimpleButton(new Vector2(x_right_align, headerOffsetY - 80f), new Vector2(120f, 30f), Translate("Delete Backups"))
            {
                description = Translate("Removes Backup folder and its contents")
            };

            //Add elements to container
            tab.AddItems(new UIelement[]
            {
                backupsHeader,
                enableBackupsToggle,
                enableBackupsTooltip,
                progressiveBackupsToggle,
                progressiveBackupsTooltip,
                backupLimitUpDown,
                backupLimitTooltip,
                backupDeleteButton,
            });

            headerOffsetY = progressiveBackupsToggle.PosY - 40f;

            //Lower left section
            OpLabel backupsAllowedHeader = new OpLabel(new Vector2(x_left_align, headerOffsetY), new Vector2(300f, 30f), Translate("Backup Allow List"), FLabelAlignment.Left, true, null);

            tab.AddItems(backupsAllowedHeader);

            //Create a backup element option for each backup entry
            for (int i = 0; i < Plugin.BackupManager.BackupEntries.Count; i++)
            {
                var backupEntry = Plugin.BackupManager.BackupEntries[i];
                bool backupEnabledByDefault = Plugin.BackupManager.ProgressiveEnableMode;

                var backupConfigurable = Config.ConfigData.Bind("bkp" + backupEntry.Item1, backupEnabledByDefault, new Config.ConfigInfo("Allow backups for this log", new object[]
                {
                    backupEntry.Item1
                }));

                Config.cfgBackupEntries.Add(backupConfigurable);

                OpCheckBox checkBox = createCheckBox(backupConfigurable, new Vector2(x_left_align, headerOffsetY - (40f * (i + 1))));
                OpLabel tooltip = createTooltip(checkBox);

                tab.AddItems(checkBox, tooltip);
                BackupElements.Add((checkBox, tooltip));
            }
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

        private OpLabel createTooltip(UIconfig owner)
        {
            return createTooltip(owner, new Vector2(60f, owner.ScreenPos.y));
        }

        private OpLabel createTooltip(UIconfig owner, Vector2 pos)
        {
            return new OpLabel(pos.x, pos.y, Translate(Config.GetTooltip(owner.cfgEntry)), false)
            {
                bumpBehav = owner.bumpBehav,
                description = owner.description
            };
        }
    }
}
