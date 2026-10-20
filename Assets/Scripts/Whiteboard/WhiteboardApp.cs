using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Whiteboard
{
    /// <summary>
    /// Entry point: owns the paint surface and routes mouse/keyboard input to the active tool.
    /// The UI lives in the scene (built once with Tools ▸ Whiteboard ▸ Build Scene UI).
    /// </summary>
    public sealed class WhiteboardApp : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] CanvasScaler canvasScaler;
        [SerializeField] RawImage gridImage;
        [SerializeField] RawImage drawingImage;
        [SerializeField] RawImage brushCursor;
        [SerializeField] ToolDeck deck;
        [SerializeField] ColorPanel colorPanel;

        [Header("Pen")]
        [SerializeField] float penSize = 4f;
        [SerializeField] Vector2 penSizeRange = new Vector2(1f, 64f);
        [Tooltip("Filters hand jitter. 0 follows the pointer exactly; higher values are smoother but trail the cursor more.")]
        [SerializeField, Range(0f, 0.9f)] float strokeSmoothing = 0.35f;

        [Header("Eraser")]
        [SerializeField] float eraserSize = 24f;
        [SerializeField] Vector2 eraserSizeRange = new Vector2(4f, 160f);
        [SerializeField] EraserMode eraserMode = EraserMode.Stroke;

        [Header("Bucket")]
        [Tooltip("How far (0-255 per channel) a pixel may differ from the clicked color and still be filled.")]
        [SerializeField, Range(0, 255)] int fillTolerance = 48;

        [Header("Board")]
        [SerializeField] bool gridVisible;
        [SerializeField, Min(4)] int gridCellSize = 32;
        [SerializeField] Color gridColor = new Color(0.16f, 0.24f, 0.36f, 0.09f);
        [Tooltip("Scale the UI by the screen DPI (96 dpi = 1x) so it keeps its physical size on high-DPI displays.")]
        [SerializeField] bool scaleUiWithDpi = true;

        PaintSurface surface;
        Palette palette;
        ColorModel color;
        Texture2D gridTexture;
        Texture2D brushCursorTexture;
        int brushCursorDiameter = -1;

        BoardTool tool = BoardTool.Pen;
        bool pointerActive;
        Vector2 lastPointer;
        int screenWidth, screenHeight;

        float ActiveSize => tool == BoardTool.Eraser ? eraserSize : penSize;

        void Awake()
        {
            if (scaleUiWithDpi && Screen.dpi > 0f) canvasScaler.scaleFactor = Mathf.Clamp(Screen.dpi / 96f, 1f, 3f);

            palette = Palette.Load();
            color = new ColorModel(Palette.Defaults[0]);
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            surface = new PaintSurface(screenWidth, screenHeight);
            drawingImage.texture = surface.Texture;

            gridTexture = CreateGridTexture();
            gridImage.texture = gridTexture;
            UpdateGridTiling();
            brushCursor.enabled = false;

            deck.Initialize();
            deck.ToolClicked += OnToolClicked;
            deck.EraserModeClicked += SetEraserMode;
            deck.SizeChanged += OnSizeChanged;
            deck.ColorClicked += () => SetColorPanelOpen(!colorPanel.IsOpen);
            deck.UndoClicked += surface.Undo;
            deck.RedoClicked += surface.Redo;
            deck.GridClicked += () => SetGrid(!gridVisible);
            deck.ClearClicked += surface.Clear;
            colorPanel.Initialize(color, palette);
            colorPanel.SetOpen(false);

            color.Changed += OnColorChanged;
            surface.HistoryChanged += RefreshHistory;
            SelectTool(BoardTool.Pen);
            SetGrid(gridVisible);
            deck.SetColor(color.Current);
            deck.SetEraserMode(eraserMode);
            RefreshHistory();
        }

        void OnDestroy()
        {
            Cursor.visible = true;
            surface?.Dispose();
            if (gridTexture != null) Destroy(gridTexture);
            if (brushCursorTexture != null) Destroy(brushCursorTexture);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) EndPointerAction();
        }

        void Update()
        {
            HandleResize();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            HandleShortcuts(overUI);
            HandlePointer(overUI);
            UpdateBrushCursor(overUI);
        }

        void LateUpdate() => surface.Upload();

        #region Input

        void HandlePointer(bool overUI)
        {
            Vector2 position = Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                // A click on the board while the color popup is open just closes it.
                if (colorPanel.IsOpen) SetColorPanelOpen(false);
                else BeginPointerAction(position);
            }
            else if (pointerActive && Input.GetMouseButton(0) && position != lastPointer)
            {
                ContinuePointerAction(position);
            }

            if (pointerActive && !Input.GetMouseButton(0)) EndPointerAction();
        }

        void BeginPointerAction(Vector2 position)
        {
            lastPointer = position;
            switch (tool)
            {
                case BoardTool.Pen:
                    surface.BeginStroke(StrokeKind.Paint, color.Current, penSize * 0.5f, position, strokeSmoothing);
                    pointerActive = true;
                    break;
                case BoardTool.Eraser when eraserMode == EraserMode.Area:
                    surface.BeginStroke(StrokeKind.Erase, default, eraserSize * 0.5f, position, strokeSmoothing);
                    pointerActive = true;
                    break;
                case BoardTool.Eraser:
                    surface.BeginObjectErase();
                    surface.EraseObjects(position, position, eraserSize * 0.5f);
                    pointerActive = true;
                    break;
                case BoardTool.Bucket:
                    surface.Fill(position, color.Current, fillTolerance);
                    break;
            }
        }

        void ContinuePointerAction(Vector2 position)
        {
            if (tool == BoardTool.Eraser && eraserMode == EraserMode.Stroke)
                surface.EraseObjects(lastPointer, position, eraserSize * 0.5f);
            else
                surface.ExtendStroke(position);
            lastPointer = position;
        }

        void EndPointerAction()
        {
            if (!pointerActive) return;
            pointerActive = false;
            surface.EndStroke();
            surface.EndObjectErase();
        }

        void HandleShortcuts(bool overUI)
        {
            if (pointerActive) return;

            bool command = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (command)
            {
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    if (shift) surface.Redo();
                    else surface.Undo();
                }
                if (Input.GetKeyDown(KeyCode.Y)) surface.Redo();
                return;
            }

            for (int digit = 1; digit <= 9; digit++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + digit) || Input.GetKeyDown(KeyCode.Keypad0 + digit))
                    RunNumberShortcut(digit);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) SetColorPanelOpen(false);

            float scroll = Input.mouseScrollDelta.y;
            if (!overUI && Mathf.Abs(scroll) > 0.01f) StepSize(scroll > 0f ? 1 : -1);
        }

        /// <summary>Number keys follow the tool deck from left to right. Clear has no key on purpose.</summary>
        void RunNumberShortcut(int digit)
        {
            switch (digit)
            {
                case 1: OnToolClicked(BoardTool.Pen); break;
                case 2: OnToolClicked(BoardTool.Eraser); break;
                case 3: OnToolClicked(BoardTool.Bucket); break;
                case 4: SetColorPanelOpen(!colorPanel.IsOpen); break;
                case 5: StepSize(-1); break;
                case 6: StepSize(1); break;
                case 7: surface.Undo(); break;
                case 8: surface.Redo(); break;
                case 9: SetGrid(!gridVisible); break;
            }
        }

        #endregion

        #region Tools

        void OnToolClicked(BoardTool clicked)
        {
            // Clicking the active eraser again flips its mode.
            if (clicked == BoardTool.Eraser && tool == BoardTool.Eraser)
                SetEraserMode(eraserMode == EraserMode.Stroke ? EraserMode.Area : EraserMode.Stroke);
            else
                SelectTool(clicked);
        }

        void SelectTool(BoardTool next)
        {
            EndPointerAction();
            tool = next;
            if (tool == BoardTool.Eraser) colorPanel.SetOpen(false);
            deck.SetTool(tool);
            RefreshSize();
            RefreshEraserBar();
        }

        void SetEraserMode(EraserMode mode)
        {
            eraserMode = mode;
            deck.SetEraserMode(mode);
        }

        void SetColorPanelOpen(bool open)
        {
            colorPanel.SetOpen(open);
            RefreshEraserBar();
        }

        void OnColorChanged()
        {
            deck.SetColor(color.Current);
            if (tool == BoardTool.Eraser) SelectTool(BoardTool.Pen);
        }

        void OnSizeChanged(float value)
        {
            if (tool == BoardTool.Eraser) eraserSize = value;
            else if (tool == BoardTool.Pen) penSize = value;
        }

        void StepSize(int direction)
        {
            if (tool == BoardTool.Bucket) return;
            var range = tool == BoardTool.Eraser ? eraserSizeRange : penSizeRange;
            float size = ActiveSize;
            float step = Mathf.Max(1f, Mathf.Round(size * 0.1f));
            OnSizeChanged(Mathf.Clamp(size + direction * step, range.x, range.y));
            RefreshSize();
        }

        void SetGrid(bool visible)
        {
            gridVisible = visible;
            gridImage.enabled = visible;
            deck.SetGrid(visible);
        }

        void RefreshSize()
        {
            var range = tool == BoardTool.Eraser ? eraserSizeRange : penSizeRange;
            deck.SetSize(ActiveSize, range.x, range.y, tool != BoardTool.Bucket);
        }

        void RefreshEraserBar() => deck.SetEraserBarVisible(tool == BoardTool.Eraser && !colorPanel.IsOpen);

        void RefreshHistory() => deck.SetHistory(surface.CanUndo, surface.CanRedo, surface.CanClear);

        #endregion

        #region Board

        void HandleResize()
        {
            if (Screen.width == screenWidth && Screen.height == screenHeight) return;
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            EndPointerAction();
            surface.Resize(screenWidth, screenHeight);
            drawingImage.texture = surface.Texture;
            UpdateGridTiling();
        }

        Texture2D CreateGridTexture()
        {
            // One tile holds 5×5 cells; the first row/column is drawn darker as a major line.
            int size = gridCellSize * 5;
            var minor = (Color32)gridColor;
            var major = minor;
            major.a = (byte)Mathf.Min(255, minor.a * 2);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (x == 0 || y == 0) px[y * size + x] = major;
                else if (x % gridCellSize == 0 || y % gridCellSize == 0) px[y * size + x] = minor;
            }
            return TextureUtil.Create(size, size, px, FilterMode.Point, TextureWrapMode.Repeat);
        }

        void UpdateGridTiling()
        {
            float tile = gridCellSize * 5f;
            gridImage.uvRect = new Rect(0f, 0f, screenWidth / tile, screenHeight / tile);
        }

        void UpdateBrushCursor(bool overUI)
        {
            Vector2 p = Input.mousePosition;
            bool inside = p.x >= 0f && p.y >= 0f && p.x < screenWidth && p.y < screenHeight;
            bool show = tool != BoardTool.Bucket && inside && (pointerActive || !overUI);
            brushCursor.enabled = show;
            Cursor.visible = !show;
            if (!show) return;

            int diameter = Mathf.RoundToInt(ActiveSize);
            if (diameter != brushCursorDiameter) RebuildBrushCursor(diameter);
            float scale = canvasScaler.scaleFactor;
            brushCursor.rectTransform.sizeDelta = new Vector2(brushCursorTexture.width, brushCursorTexture.height) / scale;
            brushCursor.rectTransform.anchoredPosition = p / scale;
        }

        /// <summary>Dark ring with a light halo (visible on any color) at exactly the brush footprint.</summary>
        void RebuildBrushCursor(int diameter)
        {
            brushCursorDiameter = diameter;
            float radius = Mathf.Max(diameter * 0.5f, 2f);
            int size = Mathf.CeilToInt(radius * 2f) + 8;
            float center = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float dark = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / 0.8f);
                float light = Mathf.Clamp01(1f - Mathf.Abs(d - radius - 1.3f) / 0.8f);
                float dot = diameter > 10 ? Mathf.Clamp01(1.4f - d) : 0f;
                float darkAlpha = Mathf.Max(dark, dot) * 0.85f;
                float a = darkAlpha + light * 0.9f * (1f - darkAlpha);
                byte shade = a > 0f ? TextureUtil.ToByte(light * 0.9f * (1f - darkAlpha) / a) : (byte)0;
                px[y * size + x] = new Color32(shade, shade, shade, TextureUtil.ToByte(a));
            }
            if (brushCursorTexture != null) Destroy(brushCursorTexture);
            brushCursorTexture = TextureUtil.Create(size, size, px, FilterMode.Bilinear, TextureWrapMode.Clamp);
            brushCursor.texture = brushCursorTexture;
        }

        #endregion
    }
}
