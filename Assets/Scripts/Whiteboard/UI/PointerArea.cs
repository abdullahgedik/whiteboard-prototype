using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Whiteboard
{
    /// <summary>Reports pointer input on a UI element, with positions normalized to its rect (0..1).</summary>
    public sealed class PointerArea : MonoBehaviour,
        IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerClickHandler
    {
        public event Action<Vector2> Pressed;
        public event Action<Vector2> Dragged;
        public event Action<PointerEventData.InputButton> Clicked;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Pressed?.Invoke(Normalize(eventData));
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Dragged?.Invoke(Normalize(eventData));
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(eventData.button);

        Vector2 Normalize(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out var local);
            var rect = rt.rect;
            return new Vector2(
                Mathf.Clamp01((local.x - rect.xMin) / rect.width),
                Mathf.Clamp01((local.y - rect.yMin) / rect.height));
        }
    }
}
