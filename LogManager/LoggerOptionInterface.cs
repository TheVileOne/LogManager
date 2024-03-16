using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vector2 = UnityEngine.Vector2;

namespace LogManager
{
    internal class LoggerOptionInterface : OptionInterface
    {
        /// <summary>
        /// The initial y-value by which all other controls are positioned from 
        /// </summary>
        private const float y_offset = 560f;
        
        /// <summary>
        /// The position by which controls are aligned along the x-axis
        /// </summary>
        private const float x_left_align = 20f;
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
            OpCheckBox directoryOptionToggle = new OpCheckBox(Config.cfgUseAlternativeDirectory, new Vector2(x_left_align, y_offset - 90f))
            {
                description = Translate(Config.cfgUseAlternativeDirectory.info.description)
            };
            OpLabel directoryOptionTooltip = new OpLabel(60f, y_offset - 90f, Translate(Config.cfgUseAlternativeDirectory.info.Tags[0] as string), false)
            {
                bumpBehav = directoryOptionToggle.bumpBehav,
                description = directoryOptionToggle.description
            };

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

            //Create elements
            OpLabel backupsHeader = new OpLabel(new Vector2(x_left_align, headerOffsetY), new Vector2(300f, 30f), Translate("Backup Management"), FLabelAlignment.Left, true, null);

            OpCheckBox enableBackupsToggle = new OpCheckBox(Config.cfgAllowBackups, new Vector2(x_left_align, headerOffsetY - 40f))
            {
                description = Translate(Config.cfgAllowBackups.info.description)
            };
            OpLabel enableBackupsTooltip = new OpLabel(60f, headerOffsetY - 40f, Translate(Config.cfgAllowBackups.info.Tags[0] as string), false)
            {
                bumpBehav = enableBackupsToggle.bumpBehav,
                description = enableBackupsToggle.description
            };

            OpCheckBox progressiveBackupsToggle = new OpCheckBox(Config.cfgAllowProgressiveBackups, new Vector2(x_left_align, headerOffsetY - 80f))
            {
                description = Translate(Config.cfgAllowProgressiveBackups.info.description)
            };
            OpLabel progressiveBackupsTooltip = new OpLabel(60f, headerOffsetY - 80f, Translate(Config.cfgAllowProgressiveBackups.info.Tags[0] as string), false)
            {
                bumpBehav = enableBackupsToggle.bumpBehav,
                description = enableBackupsToggle.description
            };

            OpSimpleButton backupDeleteButton = new OpSimpleButton(new Vector2(x_left_align, headerOffsetY - 140f), new Vector2(120f, 30f), Translate("Delete Backups"))
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
                backupDeleteButton
            });
        }
    }
}
