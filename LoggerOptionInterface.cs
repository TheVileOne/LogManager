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
        private const float y_offset = 560f;
        public override void Initialize()
        {
            base.Initialize();

            Tabs = new OpTab[]
            {
                new OpTab(this, Translate("Options"))
            };

            OpTab tab = Tabs[0];

            //Create elements
            OpLabel opLabel = new OpLabel(new Vector2(150f, y_offset - 40f), new Vector2(300f, 30f), Translate("Logging Tools"), FLabelAlignment.Center, true, null);
            OpCheckBox opCheckBox = new OpCheckBox(Config.cfgUseAlternativeDirectory, new Vector2(20f, y_offset - 90f))
            {
                description = Translate(Config.cfgUseAlternativeDirectory.info.description)
            };
            OpLabel infoLabel = new OpLabel(60f, y_offset - 90f, Translate(Config.cfgUseAlternativeDirectory.info.Tags[0] as string), false)
            {
                bumpBehav = opCheckBox.bumpBehav,
                description = opCheckBox.description
            };

            //Add elements to container
            tab.AddItems(new UIelement[]
            {
                opLabel,
                opCheckBox,
                infoLabel
            });
        }
    }
}
