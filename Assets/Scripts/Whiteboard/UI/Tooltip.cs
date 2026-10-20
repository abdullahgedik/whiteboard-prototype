using TMPro;
using UnityEngine;

namespace Whiteboard
{
    /// <summary>Shared floating label shown above hovered controls (see <see cref="TooltipTrigger"/>).
    /// Lives on an always-active object; only the bubble is toggled.</summary>
    public sealed class Tooltip : MonoBehaviour
    {
        [SerializeField] RectTransform bubble;
        [SerializeField] TMP_Text label;
        [SerializeField] float offset = 8f;

        public static Tooltip Current { get; private set; }

        void Awake()
        {
            Current = this;
            bubble.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Show(string text, RectTransform target)
        {
            label.text = text;
            bubble.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var rect = target.rect;
            // Screen-space overlay canvas: world space equals screen pixels.
            var top = target.TransformPoint(new Vector3(rect.center.x, rect.yMax, 0f));
            bubble.position = top + new Vector3(0f, offset * target.lossyScale.y, 0f);
        }

        public void Hide() => bubble.gameObject.SetActive(false);
    }
}
