using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

// Cuatro atajos para montar interfaz por codigo. La del ordenador se construye
// en tiempo de ejecucion en vez de a mano en la escena.
public static class UIFactory
{
    public static readonly Color Ink = new Color(0.12f, 0.13f, 0.16f);
    public static readonly Color Muted = new Color(0.45f, 0.47f, 0.52f);
    public static readonly Color Accent = new Color(0.16f, 0.45f, 0.85f);
    public static readonly Color Panel = new Color(0.96f, 0.96f, 0.97f);
    public static readonly Color Card = new Color(1f, 1f, 1f);

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        return rect;
    }

    public static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    public static Image Box(string name, Transform parent, Color color)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();

        image.color = color;

        return image;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string content, int size, Color color, TextAlignmentOptions align)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;

        return text;
    }

    public static Button Button(string name, Transform parent, string label, int size, Color background, Color labelColor, Action onClick)
    {
        Image image = Box(name, parent, background);
        Button button = image.gameObject.AddComponent<Button>();

        button.targetGraphic = image;

        TextMeshProUGUI text = Text("Label", image.transform, label, size, labelColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 6f, 2f, 6f, 2f);

        if (onClick != null) button.onClick.AddListener(() => onClick());

        return button;
    }

    public static VerticalLayoutGroup Column(RectTransform rect, float spacing, RectOffset padding)
    {
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        return layout;
    }

    public static LayoutElement Height(RectTransform rect, float height)
    {
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();

        element.minHeight = height;
        element.preferredHeight = height;

        return element;
    }

    private static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();

    // Un rectangulo de esquinas redondeadas dibujado a mano en una textura, para
    // no tener que importar ningun sprite al proyecto. Sale con bordes de nueve
    // trozos, asi que se puede estirar a cualquier tamano sin deformar la curva.
    public static Sprite RoundedSprite(int radius)
    {
        if (radius < 1) radius = 1;

        Sprite cached;
        if (roundedCache.TryGetValue(radius, out cached) && cached != null) return cached;

        // Dos pixeles de sobra en medio: son los que estira el nueve-trozos.
        int size = radius * 2 + 2;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // El punto mas cercano dentro del rectangulo interior: la
                // distancia hasta el es lo que redondea las cuatro esquinas.
                float qx = Mathf.Clamp(px, radius, size - radius);
                float qy = Mathf.Clamp(py, radius, size - radius);

                float distance = Mathf.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));

                // Medio pixel de margen: suaviza el borde en vez de dejarlo dentado.
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));

        roundedCache[radius] = sprite;

        return sprite;
    }
}
