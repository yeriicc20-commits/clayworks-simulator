using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Monta el nivel y la barra de experiencia justo debajo del dinero, los dos
// clavados a la esquina superior derecha. Se construye por codigo colgando del
// mismo sitio que el texto del dinero, para no depender de montarlo a mano.
public class LevelHUD : MonoBehaviour
{
    public static LevelHUD Instance;

    [Header("Colocacion")]
    [Tooltip("Separacion respecto al borde de la pantalla.")]
    public float margin = 20f;
    [Tooltip("Ancho del bloque entero.")]
    public float width = 220f;
    [Tooltip("Alto de la linea del dinero.")]
    public float moneyHeight = 34f;
    [Tooltip("Hueco entre el dinero y el nivel.")]
    public float spacing = 8f;

    [Header("Barra")]
    public float labelHeight = 24f;
    public float barHeight = 12f;
    [Tooltip("Que parte del ancho del bloque ocupa la barra.")]
    [Range(0.2f, 1f)] public float barWidthFactor = 0.5f;
    public Color barColor = new Color(0.32f, 0.78f, 0.4f);

    [Header("Aviso de experiencia")]
    [Tooltip("Cuanto dura el +1 XP que asoma al ganar experiencia.")]
    public float popupSeconds = 1.1f;
    [Tooltip("Cuanto sube mientras se desvanece.")]
    public float popupRise = 16f;

    private TextMeshProUGUI label;
    private TextMeshProUGUI popup;
    private RectTransform fill;
    private LevelManager levels;
    private bool built = false;

    private float popupTimer = 0f;
    private float pendingGain = 0f;
    private Vector2 popupHome = Vector2.zero;

    void Awake()
    {
        Instance = this;
    }

    public static LevelHUD EnsureExists()
    {
        if (Instance != null) return Instance;

        LevelHUD existing = FindAnyObjectByType<LevelHUD>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("LevelHUD").AddComponent<LevelHUD>();
    }

    void Start()
    {
        levels = LevelManager.EnsureExists();

        if (levels != null) levels.Changed += Refresh;

        Build();
        Refresh();
    }

    void OnDestroy()
    {
        if (levels != null) levels.Changed -= Refresh;
    }

    // Pega un elemento a la esquina superior derecha. Anclado ahi de verdad, no
    // colocado a ojo: con el anclaje al centro que traia el dinero de la escena,
    // al estirar la ventana se iba andando por la pantalla.
    void PinTopRight(RectTransform rect, float fromTop, float height)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(-margin, -margin - fromTop);
    }

    void Build()
    {
        if (built) return;

        TextMeshProUGUI money = GameManager.Instance != null ? GameManager.Instance.moneyText : null;

        if (money == null) return;

        RectTransform moneyRect = money.rectTransform;
        RectTransform parent = moneyRect.parent as RectTransform;

        if (parent == null) return;

        PinTopRight(moneyRect, 0f, moneyHeight);
        money.alignment = TextAlignmentOptions.Right;

        RectTransform holder = UIFactory.Rect("NivelTienda", parent);

        PinTopRight(holder, moneyHeight + spacing, labelHeight + barHeight + 4f);

        label = UIFactory.Text("Texto", holder, "", Mathf.RoundToInt(money.fontSize * 0.8f), barColor,
            TextAlignmentOptions.Right);

        label.font = money.font;
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.sizeDelta = new Vector2(0f, labelHeight);
        label.rectTransform.anchoredPosition = Vector2.zero;

        // La barra no ocupa todo el bloque: se queda pegada a la derecha, bajo
        // el texto del nivel.
        Image track = UIFactory.Box("Barra", holder, new Color(0f, 0f, 0f, 0.45f));

        track.sprite = UIFactory.RoundedSprite(Mathf.RoundToInt(barHeight * 0.5f));
        track.type = Image.Type.Sliced;

        track.rectTransform.anchorMin = new Vector2(1f, 0f);
        track.rectTransform.anchorMax = new Vector2(1f, 0f);
        track.rectTransform.pivot = new Vector2(1f, 0f);
        track.rectTransform.sizeDelta = new Vector2(width * barWidthFactor, barHeight);
        track.rectTransform.anchoredPosition = Vector2.zero;

        // La propia barra hace de mascara: asi el relleno se recorta con su
        // misma curva y las puntas salen redondeadas a cualquier porcentaje.
        // Con Image.Type.Filled no valdria, porque el relleno por porcentaje se
        // salta los bordes del nueve-trozos y estira la curva.
        Mask mask = track.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        Image bar = UIFactory.Box("Relleno", track.transform, barColor);

        fill = bar.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        // El aviso de experiencia asoma a la izquierda de la barra. Cuelga del
        // bloque y no de la barra, porque la mascara lo recortaria.
        popup = UIFactory.Text("Ganancia", holder, "", Mathf.RoundToInt(money.fontSize * 0.8f), barColor,
            TextAlignmentOptions.Right);

        popup.font = money.font;
        popup.rectTransform.anchorMin = new Vector2(1f, 0f);
        popup.rectTransform.anchorMax = new Vector2(1f, 0f);
        popup.rectTransform.pivot = new Vector2(1f, 0.5f);
        popup.rectTransform.sizeDelta = new Vector2(90f, 24f);
        popupHome = new Vector2(-(width * barWidthFactor) - 10f, barHeight * 0.5f);
        popup.rectTransform.anchoredPosition = popupHome;
        popup.alpha = 0f;

        built = true;
    }

    public void Refresh()
    {
        if (!built) Build();
        if (levels == null || label == null) return;

        // Sin numeros: cuanto falta para el siguiente nivel es sorpresa, solo se
        // intuye por lo llena que esta la barra.
        label.text = "Nivel " + levels.level;

        if (fill != null)
        {
            // Se estira con el anclaje, y la mascara de la barra le redondea
            // las puntas: vale para cualquier ancho.
            fill.anchorMax = new Vector2(levels.Progress, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }

    // Un "+1 XP" que asoma al lado de la barra y se va solo. Si llegan varios
    // seguidos se suman en el mismo aviso en vez de pisarse.
    public void ShowGain(float amount)
    {
        if (!built) Build();
        if (popup == null || amount <= 0f) return;

        pendingGain = popupTimer > 0f ? pendingGain + amount : amount;
        popupTimer = popupSeconds;

        popup.text = "+" + Mathf.RoundToInt(pendingGain) + " XP";
    }

    void Update()
    {
        if (popup == null || popupTimer <= 0f) return;

        popupTimer -= Time.unscaledDeltaTime;

        float left = Mathf.Max(0f, popupTimer);

        // Sube un poco mientras se desvanece, para que se lea como algo que sale
        // de la barra y no como un texto que parpadea.
        float t = 1f - (left / popupSeconds);

        popup.rectTransform.anchoredPosition = popupHome + new Vector2(0f, popupRise * t);
        popup.alpha = left > popupSeconds * 0.5f ? 1f : left / (popupSeconds * 0.5f);

        if (popupTimer <= 0f)
        {
            popup.alpha = 0f;
            pendingGain = 0f;
            popup.rectTransform.anchoredPosition = popupHome;
        }
    }
}
