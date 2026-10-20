using UnityEngine;
using UnityEngine.UI;

namespace Whiteboard
{
    /// <summary>Round color chip used for built-in colors and custom slots.</summary>
    public sealed class ColorSwatch : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] Image border;
        [SerializeField] Image selection;
        [SerializeField] Image emptyIcon;
        [SerializeField] PointerArea input;

        public PointerArea Input => input;

        /// <summary>Null shows an empty slot.</summary>
        public void SetColor(Color32? color)
        {
            fill.color = color ?? new Color32(0, 0, 0, 0);
            border.color = color.HasValue ? Theme.SwatchBorder : Theme.EmptySlotBorder;
            emptyIcon.enabled = !color.HasValue;
        }

        public void SetSelected(bool selected) => selection.enabled = selected;
    }
}
