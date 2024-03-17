using Menu.Remix.MixedUI;
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

            //Create elements
            OpLabel backupsHeader = new OpLabel(new Vector2(x_left_align, headerOffsetY), new Vector2(300f, 30f), Translate("Backup Management"), FLabelAlignment.Left, true, null);

            OpCheckBox enableBackupsToggle = createCheckBox(Config.cfgAllowBackups, new Vector2(x_left_align, headerOffsetY - 40f));
            OpLabel enableBackupsTooltip = createTooltip(enableBackupsToggle);

            OpCheckBox progressiveBackupsToggle = createCheckBox(Config.cfgAllowProgressiveBackups, new Vector2(x_left_align, headerOffsetY - 80f));
            OpLabel progressiveBackupsTooltip = createTooltip(progressiveBackupsToggle);

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

        private OpLabel createTooltip(OpCheckBox owner)
        {
            return new OpLabel(60f, owner.ScreenPos.y, Translate(Config.GetTooltip(owner.cfgEntry)), false)
            {
                bumpBehav = owner.bumpBehav,
                description = owner.description
            };
        }
    }
}
