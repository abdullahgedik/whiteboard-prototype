using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Whiteboard.EditorTools
{
    /// <summary>
    /// One-shot scaffolding: generates the UI art and builds the camera, event system and
    /// UI hierarchy in the open scene, wired to <see cref="WhiteboardApp"/>. After that the
    /// scene is the source of truth; edit it freely. Re-running replaces the generated objects.
    /// </summary>
    [InitializeOnLoad]
    public static class WhiteboardSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Whiteboard.unity";
        const string ArtFolder = "Assets/Art/UI";
        const string PrefabFolder = "Assets/Prefabs/UI";
        const string SwatchPrefabPath = PrefabFolder + "/Color Swatch.prefab";
        const int UILayer = 5;

        const float ButtonSize = 40f;
        const float IconSize = 24f;
        const int DeckPadding = 6;
        const float DeckBottom = 16f;
        const float PopupBottom = DeckBottom + ButtonSize + DeckPadding * 2 + 10f;
        const float SwatchSize = 20f;
        const float SwatchSpacing = 4f;
        const int SwatchColumns = 12;
        const int PanelPadding = 14;

        static readonly string[] GeneratedRoots = { "Main Camera", "EventSystem", "Whiteboard UI" };

        // First-time setup: if the UI art has never been generated, build the Whiteboard scene
        // automatically once. Afterwards the scene is only changed through the menu item.
        static WhiteboardSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.IsValidFolder(ArtFolder)) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
                if (SceneManager.GetActiveScene().path != ScenePath)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene(ScenePath);
                }
                BuildScene();
            };
        }

        [MenuItem("Tools/Whiteboard/Build Scene UI")]
        static void Build()
        {
            if (!EditorUtility.DisplayDialog("Build Whiteboard UI",
                    "Regenerates the sprites in " + ArtFolder + " and replaces the Main Camera, EventSystem and " +
                    "'Whiteboard UI' objects in the open scene. Manual changes to those objects will be lost.",
                    "Build", "Cancel"))
                return;
            BuildScene();
        }

        static void BuildScene()
        {
            var art = UIArtGenerator.Generate(ArtFolder);
            var swatchPrefab = BuildSwatchPrefab(art);

            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                if (Array.IndexOf(GeneratedRoots, root.name) >= 0) Object.DestroyImmediate(root);

            var app = Object.FindAnyObjectByType<WhiteboardApp>();
            if (app == null) app = new GameObject("Whiteboard").AddComponent<WhiteboardApp>();

            CreateCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Whiteboard UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            var canvas = (RectTransform)canvasGo.transform;

            Stretch(Img("Board", canvas, null, Theme.Panel).rectTransform);
            var grid = Raw("Grid", canvas);
            Stretch(grid.rectTransform);
            var drawing = Raw("Drawing", canvas);
            Stretch(drawing.rectTransform);
            var brushCursor = Raw("Brush Cursor", canvas);
            brushCursor.rectTransform.anchorMin = brushCursor.rectTransform.anchorMax = Vector2.zero;
            brushCursor.enabled = false;

            var deck = BuildDeck(canvas, art);
            var colorPanel = BuildColorPanel(canvas, art, swatchPrefab);
            BuildTooltip(canvas, art);
            SetLayerRecursively(canvasGo, UILayer);

            Wire(app,
                ("canvasScaler", scaler),
                ("gridImage", grid),
                ("drawingImage", drawing),
                ("brushCursor", brushCursor),
                ("deck", deck),
                ("colorPanel", colorPanel));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = app.gameObject;
            Debug.Log("Whiteboard UI built in scene '" + scene.name + "'.");
        }

        #region Sections

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;
            cam.cullingMask = 0;
        }

        static ToolDeck BuildDeck(RectTransform canvas, UIArt art)
        {
            var deck = Panel("Tool Deck", canvas, art);
            BottomCenter(deck, DeckBottom);
            RowLayout(deck, DeckPadding, 2f);

            var pen = IconButton("Pen", deck, art.Pen, art, "Pen  (P)");
            var eraser = IconButton("Eraser", deck, art.Eraser, art, "Eraser  (E)");
            var bucket = IconButton("Bucket", deck, art.Bucket, art, "Fill  (B)");
            Divider(deck);

            var color = IconButton("Color", deck, null, art, "Color  (C)");
            var swatch = Img("Swatch", color.transform, art.Circle, Palette.Defaults[0]);
            Center(swatch.rectTransform, 24f, 24f);
            Stretch(Img("Border", swatch.transform, art.Ring, Theme.SwatchBorder).rectTransform);

            var (slider, sizeLabel, sizeGroup) = SizeControl(deck, art);
            Divider(deck);

            var undo = IconButton("Undo", deck, art.Undo, art, "Undo  (Ctrl+Z)");
            var redo = IconButton("Redo", deck, art.Redo, art, "Redo  (Ctrl+Y)");
            Divider(deck);

            var gridToggle = IconButton("Grid", deck, art.Grid, art, "Grid  (G)");
            var clear = IconButton("Clear", deck, art.Trash, art, "Clear board");

            var eraserBar = Panel("Eraser Modes", canvas, art);
            BottomCenter(eraserBar, PopupBottom);
            RowLayout(eraserBar, 4, 2f);
            var strokeMode = LabeledButton("Stroke", eraserBar, art.StrokeErase, art.ButtonSmall, 88f, 32f, "Erase whole strokes");
            var areaMode = LabeledButton("Area", eraserBar, art.AreaErase, art.ButtonSmall, 88f, 32f, "Erase only where you brush");
            eraserBar.gameObject.SetActive(false);

            pen.SetActive(true);
            strokeMode.SetActive(true);

            var component = deck.gameObject.AddComponent<ToolDeck>();
            Wire(component,
                ("penButton", pen),
                ("eraserButton", eraser),
                ("bucketButton", bucket),
                ("colorButton", color),
                ("colorSwatch", swatch),
                ("sizeSlider", slider),
                ("sizeLabel", sizeLabel),
                ("sizeGroup", sizeGroup),
                ("undoButton", undo),
                ("redoButton", redo),
                ("gridButton", gridToggle),
                ("clearButton", clear),
                ("eraserBar", eraserBar.gameObject),
                ("strokeModeButton", strokeMode),
                ("areaModeButton", areaMode));
            return component;
        }

        static (Slider, TMP_Text, CanvasGroup) SizeControl(Transform parent, UIArt art)
        {
            var holder = Rect("Size", parent);
            Preferred(holder, 150f, ButtonSize);
            var group = holder.gameObject.AddComponent<CanvasGroup>();
            AddTooltip(holder.gameObject, "Brush size  ( [ ]  or mouse wheel )");

            var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources
            {
                standard = art.Slider,
                background = art.Slider,
                knob = art.Circle
            });
            var srt = (RectTransform)sliderGo.transform;
            srt.SetParent(holder, false);
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(1f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.offsetMin = new Vector2(10f, -9f);
            srt.offsetMax = new Vector2(-36f, 9f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.wholeNumbers = true;
            slider.minValue = 1f;
            slider.maxValue = 64f;
            slider.value = 4f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.transition = Selectable.Transition.None;

            foreach (var image in sliderGo.GetComponentsInChildren<Image>())
            {
                switch (image.gameObject.name)
                {
                    case "Background":
                        image.color = Theme.Divider;
                        break;
                    case "Fill":
                        image.color = Theme.Accent;
                        break;
                    case "Handle":
                        image.color = Color.white;
                        image.rectTransform.sizeDelta = new Vector2(18f, 0f);
                        Stretch(Img("Outline", image.transform, art.Ring, new Color(0f, 0f, 0f, 0.22f)).rectTransform);
                        break;
                }
            }

            var label = Label("Value", holder, "4", 14, Theme.Icon, TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(1f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(1f, 0.5f);
            lrt.sizeDelta = new Vector2(32f, 0f);
            lrt.anchoredPosition = Vector2.zero;

            return (slider, label, group);
        }

        static ColorPanel BuildColorPanel(RectTransform canvas, UIArt art, ColorSwatch swatchPrefab)
        {
            var panel = Panel("Color Panel", canvas, art);
            BottomCenter(panel, PopupBottom);
            panel.sizeDelta = new Vector2(SwatchColumns * SwatchSize + (SwatchColumns - 1) * SwatchSpacing + PanelPadding * 2, 0f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(PanelPadding, PanelPadding, PanelPadding, PanelPadding);
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sv = Raw("Saturation Value", panel, raycast: true);
            Preferred(sv, -1f, 140f);
            sv.gameObject.AddComponent<PointerArea>();
            var svMarker = Marker(sv.rectTransform, 14f, art);

            var hue = Raw("Hue", panel, raycast: true);
            Preferred(hue, -1f, 12f);
            hue.gameObject.AddComponent<PointerArea>();
            var hueMarker = Marker(hue.rectTransform, 16f, art);

            var row = Rect("Current", panel);
            Preferred(row, -1f, 26f);
            var preview = Img("Preview", row, art.Circle, Palette.Defaults[0]);
            var prt = preview.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(24f, 24f);
            prt.anchoredPosition = Vector2.zero;
            Stretch(Img("Border", preview.transform, art.Ring, Theme.SwatchBorder).rectTransform);

            var hex = Label("Hex", row, "#1E1E1E", 15, Theme.Icon, TextAlignmentOptions.MidlineLeft);
            Stretch(hex.rectTransform);
            hex.rectTransform.offsetMin = new Vector2(34f, 0f);

            var save = LabeledButton("Save", row, art.Plus, art.ButtonSmall, 76f, 26f, "Save to custom colors");
            var saveRt = (RectTransform)save.transform;
            saveRt.anchorMin = saveRt.anchorMax = new Vector2(1f, 0.5f);
            saveRt.pivot = new Vector2(1f, 0.5f);
            saveRt.anchoredPosition = Vector2.zero;
            Theme.SetButtonColors(save.Button, Theme.Hover, Theme.Pressed, Theme.Pressed);

            SectionLabel(panel, "Colors");
            var defaultsGrid = SwatchGrid("Default Colors", panel);
            SectionLabel(panel, "Custom   <color=#9AA0A6>right-click to remove</color>");
            var customGrid = SwatchGrid("Custom Colors", panel);

            var component = panel.gameObject.AddComponent<ColorPanel>();
            Wire(component,
                ("saturationValue", sv),
                ("saturationValueMarker", svMarker),
                ("hueBar", hue),
                ("hueMarker", hueMarker),
                ("preview", preview),
                ("hexLabel", hex),
                ("saveButton", save),
                ("swatchPrefab", swatchPrefab),
                ("defaultsGrid", defaultsGrid),
                ("customGrid", customGrid));
            panel.gameObject.SetActive(false);
            return component;
        }

        static void BuildTooltip(RectTransform canvas, UIArt art)
        {
            var root = Rect("Tooltip", canvas);
            Stretch(root);
            var bubble = Img("Bubble", root, art.Tooltip, Theme.TooltipBackground);
            bubble.rectTransform.pivot = new Vector2(0.5f, 0f);
            var layout = bubble.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 5);
            layout.childControlWidth = layout.childControlHeight = true;
            var fitter = bubble.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var label = Label("Label", bubble.transform, "Tooltip", 13, Color.white, TextAlignmentOptions.Center);
            bubble.gameObject.SetActive(false);

            var tooltip = root.gameObject.AddComponent<Tooltip>();
            Wire(tooltip, ("bubble", bubble.rectTransform), ("label", label));
        }

        static ColorSwatch BuildSwatchPrefab(UIArt art)
        {
            UIArtGenerator.EnsureFolder(PrefabFolder);
            var hit = Img("Color Swatch", null, null, Theme.Transparent, raycast: true);
            hit.rectTransform.sizeDelta = new Vector2(SwatchSize, SwatchSize);
            var input = hit.gameObject.AddComponent<PointerArea>();

            var fill = Img("Fill", hit.transform, art.Circle, Color.white);
            Stretch(fill.rectTransform);
            var border = Img("Border", hit.transform, art.Ring, Theme.SwatchBorder);
            Stretch(border.rectTransform);
            var selection = Img("Selection", hit.transform, art.Ring, Theme.Accent);
            Stretch(selection.rectTransform, -3.5f);
            selection.enabled = false;
            var plus = Img("Empty Icon", hit.transform, art.Plus, Theme.TextMuted);
            Center(plus.rectTransform, 12f, 12f);
            plus.enabled = false;

            var swatch = hit.gameObject.AddComponent<ColorSwatch>();
            Wire(swatch, ("fill", fill), ("border", border), ("selection", selection), ("emptyIcon", plus), ("input", input));
            SetLayerRecursively(hit.gameObject, UILayer);

            var prefab = PrefabUtility.SaveAsPrefabAsset(hit.gameObject, SwatchPrefabPath);
            Object.DestroyImmediate(hit.gameObject);
            return prefab.GetComponent<ColorSwatch>();
        }

        #endregion

        #region Element helpers

        static RectTransform Panel(string name, Transform parent, UIArt art)
        {
            var root = Rect(name, parent);

            var shadow = Img("Shadow", root, art.Shadow, Theme.Shadow);
            shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var srt = shadow.rectTransform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(-UIArtGenerator.ShadowBlur, -UIArtGenerator.ShadowBlur - 3f);
            srt.offsetMax = new Vector2(UIArtGenerator.ShadowBlur, UIArtGenerator.ShadowBlur - 3f);

            var background = Img("Background", root, art.Panel, Theme.Panel, raycast: true);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(background.rectTransform);
            return root;
        }

        static ToolButton IconButton(string name, Transform parent, Sprite icon, UIArt art, string tooltip)
        {
            var button = CreateButton(name, parent, art.Button, ButtonSize, ButtonSize, tooltip, out var background);
            Image iconImage = null;
            if (icon != null)
            {
                iconImage = Img("Icon", background.transform, icon, Theme.Icon);
                Center(iconImage.rectTransform, IconSize, IconSize);
            }
            return AddToolButton(button, iconImage, null);
        }

        static ToolButton LabeledButton(string label, Transform parent, Sprite icon, Sprite background, float width, float height, string tooltip)
        {
            var button = CreateButton(label, parent, background, width, height, tooltip, out var bg);

            var iconImage = Img("Icon", bg.transform, icon, Theme.Icon);
            var irt = iconImage.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(18f, 18f);
            irt.anchoredPosition = new Vector2(18f, 0f);

            var text = Label("Label", bg.transform, label, 14, Theme.Icon, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(33f, 0f);

            return AddToolButton(button, iconImage, text);
        }

        static Button CreateButton(string name, Transform parent, Sprite sprite, float width, float height, string tooltip, out Image background)
        {
            background = Img(name, parent, sprite, Color.white, raycast: true);
            Preferred(background, width, height);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Theme.SetButtonColors(button, Theme.Transparent, Theme.Hover, Theme.Pressed);
            if (!string.IsNullOrEmpty(tooltip)) AddTooltip(background.gameObject, tooltip);
            return button;
        }

        static ToolButton AddToolButton(Button button, Image icon, TMP_Text label)
        {
            var toolButton = button.gameObject.AddComponent<ToolButton>();
            Wire(toolButton, ("button", button), ("icon", icon), ("label", label));
            return toolButton;
        }

        static void AddTooltip(GameObject target, string text)
        {
            var trigger = target.AddComponent<TooltipTrigger>();
            var so = new SerializedObject(trigger);
            so.FindProperty("text").stringValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Divider(Transform parent)
        {
            var holder = Rect("Divider", parent);
            Preferred(holder, 9f, ButtonSize);
            Center(Img("Line", holder, null, Theme.Divider).rectTransform, 1f, 22f);
        }

        static void SectionLabel(Transform parent, string text)
        {
            var label = Label("Label", parent, text, 12, Theme.TextMuted, TextAlignmentOptions.BottomLeft);
            Preferred(label, -1f, 15f);
        }

        static RectTransform SwatchGrid(string name, Transform parent)
        {
            var grid = Rect(name, parent);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(SwatchSize, SwatchSize);
            layout.spacing = new Vector2(SwatchSpacing, SwatchSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = SwatchColumns;
            return grid;
        }

        /// <summary>White ring with a dark outline so it reads on any color.</summary>
        static RectTransform Marker(RectTransform parent, float size, UIArt art)
        {
            var outer = Img("Marker", parent, art.Ring, new Color(0f, 0f, 0f, 0.35f));
            var rt = outer.rectTransform;
            rt.sizeDelta = new Vector2(size + 2f, size + 2f);
            rt.anchoredPosition = Vector2.zero;
            Stretch(Img("Inner", rt, art.Ring, Color.white).rectTransform, 1f);
            return rt;
        }

        static void RowLayout(RectTransform root, int padding, float spacing)
        {
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        #endregion

        #region Primitives

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        static RawImage Raw(string name, Transform parent, bool raycast = false)
        {
            var image = Rect(name, parent).gameObject.AddComponent<RawImage>();
            image.raycastTarget = raycast;
            return image;
        }

        static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void Center(RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
        }

        static void BottomCenter(RectTransform rt, float bottom)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottom);
        }

        static void Preferred(Component c, float width, float height)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) le.preferredWidth = le.minWidth = width;
            if (height >= 0f) le.preferredHeight = le.minHeight = height;
            var rt = (RectTransform)c.transform;
            rt.sizeDelta = new Vector2(width >= 0f ? width : rt.sizeDelta.x, height >= 0f ? height : rt.sizeDelta.y);
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static void Wire(Object target, params (string property, Object value)[] references)
        {
            var so = new SerializedObject(target);
            foreach (var (property, value) in references)
            {
                var prop = so.FindProperty(property);
                if (prop == null) throw new ArgumentException(target.GetType().Name + " has no serialized field '" + property + "'");
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
