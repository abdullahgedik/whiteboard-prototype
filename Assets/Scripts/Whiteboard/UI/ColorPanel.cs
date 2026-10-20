using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Whiteboard
{
    /// <summary>Popup above the deck: HSV picker, built-in colors and custom slots.</summary>
    public sealed class ColorPanel : MonoBehaviour
    {
        [Header("Picker")]
        [SerializeField] RawImage saturationValue;
        [SerializeField] RectTransform saturationValueMarker;
        [SerializeField] RawImage hueBar;
        [SerializeField] RectTransform hueMarker;
        [SerializeField, Min(8)] int pickerResolution = 64;

        [Header("Current Color")]
        [SerializeField] Image preview;
        [SerializeField] TMP_Text hexLabel;
        [SerializeField] ToolButton saveButton;

        [Header("Swatches")]
        [SerializeField] ColorSwatch swatchPrefab;
        [SerializeField] Transform defaultsGrid;
        [SerializeField] Transform customGrid;

        ColorModel model;
        Palette palette;
        Texture2D svTexture;
        Color32[] svPixels;
        ColorSwatch[] defaultSwatches;
        ColorSwatch[] customSwatches;
        float svTextureHue = -1f;

        public bool IsOpen => gameObject.activeSelf;

        public void Initialize(ColorModel colorModel, Palette colorPalette)
        {
            model = colorModel;
            palette = colorPalette;

            svPixels = new Color32[pickerResolution * pickerResolution];
            svTexture = new Texture2D(pickerResolution, pickerResolution, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            saturationValue.texture = svTexture;
            var svInput = saturationValue.GetComponent<PointerArea>();
            svInput.Pressed += OnSaturationValue;
            svInput.Dragged += OnSaturationValue;

            var huePixels = new Color32[256];
            for (int i = 0; i < huePixels.Length; i++) huePixels[i] = Color.HSVToRGB(i / 255f, 1f, 1f);
            hueBar.texture = TextureUtil.Create(huePixels.Length, 1, huePixels, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var hueInput = hueBar.GetComponent<PointerArea>();
            hueInput.Pressed += OnHue;
            hueInput.Dragged += OnHue;

            saveButton.Button.onClick.AddListener(() => palette.AddCustom(model.Current));

            defaultSwatches = new ColorSwatch[Palette.Defaults.Length];
            for (int i = 0; i < defaultSwatches.Length; i++)
            {
                var color = Palette.Defaults[i];
                var swatch = Instantiate(swatchPrefab, defaultsGrid);
                swatch.name = "Swatch " + i;
                swatch.SetColor(color);
                swatch.Input.Clicked += button =>
                {
                    if (button == PointerEventData.InputButton.Left) model.SetColor(color);
                };
                defaultSwatches[i] = swatch;
            }

            customSwatches = new ColorSwatch[Palette.CustomSlotCount];
            for (int i = 0; i < customSwatches.Length; i++)
            {
                int slot = i;
                var swatch = Instantiate(swatchPrefab, customGrid);
                swatch.name = "Slot " + i;
                swatch.Input.Clicked += button => OnCustomSlotClicked(slot, button);
                customSwatches[i] = swatch;
            }

            model.Changed += Refresh;
            palette.Changed += RefreshCustomSlots;
            RefreshCustomSlots();
        }

        void OnDestroy()
        {
            if (svTexture != null) Destroy(svTexture);
            if (hueBar != null && hueBar.texture != null) Destroy(hueBar.texture);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            gameObject.SetActive(open);
            if (open) transform.SetAsLastSibling();
        }

        void OnSaturationValue(Vector2 p) => model.SetHsv(model.Hue, p.x, p.y);

        void OnHue(Vector2 p) => model.SetHsv(p.x, model.Saturation, model.Value);

        void OnCustomSlotClicked(int slot, PointerEventData.InputButton button)
        {
            var stored = palette.GetCustom(slot);
            if (button == PointerEventData.InputButton.Right)
            {
                if (stored.HasValue) palette.ClearCustom(slot);
            }
            else if (button == PointerEventData.InputButton.Left)
            {
                if (stored.HasValue) model.SetColor(stored.Value);
                else palette.SetCustom(slot, model.Current);
            }
        }

        void Refresh()
        {
            if (!Mathf.Approximately(svTextureHue, model.Hue))
            {
                svTextureHue = model.Hue;
                float max = pickerResolution - 1f;
                for (int y = 0; y < pickerResolution; y++)
                for (int x = 0; x < pickerResolution; x++)
                    svPixels[y * pickerResolution + x] = Color.HSVToRGB(svTextureHue, x / max, y / max);
                svTexture.SetPixels32(svPixels);
                svTexture.Apply(false);
            }

            saturationValueMarker.anchorMin = saturationValueMarker.anchorMax = new Vector2(model.Saturation, model.Value);
            hueMarker.anchorMin = hueMarker.anchorMax = new Vector2(model.Hue, 0.5f);

            var current = model.Current;
            preview.color = current;
            hexLabel.text = "#" + ColorUtility.ToHtmlStringRGB(current);

            for (int i = 0; i < defaultSwatches.Length; i++)
                defaultSwatches[i].SetSelected(Palette.Same(Palette.Defaults[i], current));
            for (int i = 0; i < customSwatches.Length; i++)
            {
                var stored = palette.GetCustom(i);
                customSwatches[i].SetSelected(stored.HasValue && Palette.Same(stored.Value, current));
            }
        }

        void RefreshCustomSlots()
        {
            for (int i = 0; i < customSwatches.Length; i++) customSwatches[i].SetColor(palette.GetCustom(i));
            Refresh();
        }
    }
}
