using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whiteboard
{
    public enum BoardTool
    {
        Pen,
        Eraser,
        Bucket
    }

    public enum EraserMode
    {
        Stroke,
        Area
    }

    /// <summary>Compact toolbar at the bottom center of the screen, plus the small
    /// eraser-mode bar that floats above it while the eraser is selected.</summary>
    public sealed class ToolDeck : MonoBehaviour
    {
        [Header("Tools")]
        [SerializeField] ToolButton penButton;
        [SerializeField] ToolButton eraserButton;
        [SerializeField] ToolButton bucketButton;

        [Header("Color & Size")]
        [SerializeField] ToolButton colorButton;
        [SerializeField] Image colorSwatch;
        [SerializeField] Slider sizeSlider;
        [SerializeField] TMP_Text sizeLabel;
        [SerializeField] CanvasGroup sizeGroup;

        [Header("History & Board")]
        [SerializeField] ToolButton undoButton;
        [SerializeField] ToolButton redoButton;
        [SerializeField] ToolButton gridButton;
        [SerializeField] ToolButton clearButton;

        [Header("Eraser Modes")]
        [SerializeField] GameObject eraserBar;
        [SerializeField] ToolButton strokeModeButton;
        [SerializeField] ToolButton areaModeButton;

        bool updatingSlider;

        public event Action<BoardTool> ToolClicked;
        public event Action<EraserMode> EraserModeClicked;
        public event Action<float> SizeChanged;
        public event Action ColorClicked;
        public event Action UndoClicked;
        public event Action RedoClicked;
        public event Action GridClicked;
        public event Action ClearClicked;

        public void Initialize()
        {
            penButton.Button.onClick.AddListener(() => ToolClicked?.Invoke(BoardTool.Pen));
            eraserButton.Button.onClick.AddListener(() => ToolClicked?.Invoke(BoardTool.Eraser));
            bucketButton.Button.onClick.AddListener(() => ToolClicked?.Invoke(BoardTool.Bucket));
            colorButton.Button.onClick.AddListener(() => ColorClicked?.Invoke());
            undoButton.Button.onClick.AddListener(() => UndoClicked?.Invoke());
            redoButton.Button.onClick.AddListener(() => RedoClicked?.Invoke());
            gridButton.Button.onClick.AddListener(() => GridClicked?.Invoke());
            clearButton.Button.onClick.AddListener(() => ClearClicked?.Invoke());
            strokeModeButton.Button.onClick.AddListener(() => EraserModeClicked?.Invoke(EraserMode.Stroke));
            areaModeButton.Button.onClick.AddListener(() => EraserModeClicked?.Invoke(EraserMode.Area));
            sizeSlider.onValueChanged.AddListener(value =>
            {
                sizeLabel.text = Mathf.RoundToInt(value).ToString();
                if (!updatingSlider) SizeChanged?.Invoke(value);
            });
        }

        public void SetTool(BoardTool tool)
        {
            penButton.SetActive(tool == BoardTool.Pen);
            eraserButton.SetActive(tool == BoardTool.Eraser);
            bucketButton.SetActive(tool == BoardTool.Bucket);
        }

        public void SetEraserMode(EraserMode mode)
        {
            strokeModeButton.SetActive(mode == EraserMode.Stroke);
            areaModeButton.SetActive(mode == EraserMode.Area);
        }

        public void SetEraserBarVisible(bool visible) => eraserBar.SetActive(visible);

        public void SetColor(Color32 color) => colorSwatch.color = color;

        public void SetGrid(bool visible) => gridButton.SetActive(visible);

        public void SetHistory(bool canUndo, bool canRedo, bool canClear)
        {
            undoButton.SetEnabled(canUndo);
            redoButton.SetEnabled(canRedo);
            clearButton.SetEnabled(canClear);
        }

        public void SetSize(float value, float min, float max, bool enabled)
        {
            updatingSlider = true;
            sizeSlider.minValue = min;
            sizeSlider.maxValue = max;
            sizeSlider.value = value;
            updatingSlider = false;
            sizeSlider.interactable = enabled;
            sizeGroup.alpha = enabled ? 1f : 0.35f;
            sizeLabel.text = Mathf.RoundToInt(value).ToString();
        }
    }
}
