using Menu.Remix.MixedUI;
using System.Collections.Generic;
using UnityEngine;

namespace LogManager.Interface
{
    /// <summary>
    /// ComboBox variant that fixes draw order and opacity - code attributed to Alduris
    /// </summary>
    internal class ComboBox : OpComboBox
    {
        public const int BG_SPRITE_INDEX_RANGE = 9;

        public ComboBox(Configurable<string> config, Vector2 pos, float width, string[] array) : base(config, pos, width, array)
        {
        }

        public ComboBox(Configurable<string> config, Vector2 pos, float width, List<ListItem> list) : base(config, pos, width, list)
        {
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);
            if (_rectList != null && !_rectList.isHidden)
            {
                myContainer.MoveToFront();

                for (int i = 0; i < BG_SPRITE_INDEX_RANGE; i++)
                {
                    _rectList.sprites[i].alpha = 1;
                }
            }
        }
    }
}
