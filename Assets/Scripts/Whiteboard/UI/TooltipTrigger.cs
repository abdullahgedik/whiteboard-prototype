using UnityEngine;
using UnityEngine.EventSystems;

namespace Whiteboard
{
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField] string text;
        bool showing;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Tooltip.Current == null || string.IsNullOrEmpty(text)) return;
            Tooltip.Current.Show(text, (RectTransform)transform);
            showing = true;
        }

        public void OnPointerExit(PointerEventData eventData) => HideTooltip();
        public void OnPointerDown(PointerEventData eventData) => HideTooltip();
        void OnDisable() => HideTooltip();

        void HideTooltip()
        {
            if (!showing) return;
            showing = false;
            if (Tooltip.Current != null) Tooltip.Current.Hide();
        }
    }
}
