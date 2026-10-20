using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Whiteboard
{
    /// <summary>Deck/popup button with an optional icon and label that can show an
    /// active (selected tool) and a disabled state.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ToolButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;

        public Button Button => button;

        void Awake()
        {
            // Clicking selects a Selectable, which would suppress hover feedback afterwards.
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
        }

        public void SetActive(bool active)
        {
            Theme.SetButtonColors(button,
                active ? Theme.AccentSoft : Theme.Transparent,
                active ? Theme.AccentSoftHover : Theme.Hover,
                Theme.Pressed);
            SetContentColor(active ? Theme.Accent : Theme.Icon);
        }

        public void SetEnabled(bool enabled)
        {
            button.interactable = enabled;
            var c = Theme.Icon;
            c.a = enabled ? 1f : 0.3f;
            SetContentColor(c);
        }

        void SetContentColor(Color color)
        {
            if (icon != null) icon.color = color;
            if (label != null) label.color = color;
        }

        void Reset()
        {
            button = GetComponent<Button>();
        }
    }
}
