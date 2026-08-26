using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El carrito, en forma de lista.
//
// Antes reutilizaba la misma ficha que la rejilla de comprar: una tarjeta
// grande con su boton, una por linea. Eso esta bien para elegir, porque ahi lo
// que quieres es ver la foto grande; pero para repasar lo que llevas es
// justo lo contrario. Lo que se quiere es una linea por cosa y poder leer en
// vertical: que es, cuantos, a cuanto.
//
// Asi que las filas se construyen aqui, con las columnas alineadas:
//
//   [foto]  nombre                x3      120 $      [x]
//
// El total va abajo a la derecha, que es donde lo busca cualquiera que haya
// visto un ticket, y el boton de comprar debajo y ancho.
public class CartUI : MonoBehaviour
{
    public static CartUI Instance;

    public GameObject cartPanel;
    public Button cartButton;
    public Button checkoutButton;
    public Transform cartListContainer;

    [Tooltip("Sin usar: las filas se construyen por codigo. Se conserva por si "
             + "la escena todavia lo tiene asignado.")]
    public GameObject itemCardPrefab;

    public TextMeshProUGUI totalText;
    public TextMeshProUGUI miniTotalText;

    [Header("Medidas de la lista")]
    public float altoFila = 56f;
    public float foto = 44f;
    public float anchoCantidad = 54f;
    public float anchoPrecio = 96f;
    public float anchoQuitar = 34f;

    static readonly Color TINTA = new Color(0.13f, 0.14f, 0.17f);
    static readonly Color SUAVE = new Color(0.45f, 0.47f, 0.52f);
    static readonly Color FILA = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color FILA_ALT = new Color(0f, 0f, 0f, 0.035f);
    static readonly Color QUITAR = new Color(0.80f, 0.30f, 0.28f);

    private bool isOpen = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (cartButton != null) cartButton.onClick.AddListener(ToggleCart);
        if (checkoutButton != null) checkoutButton.onClick.AddListener(() => ShoppingCart.Instance.Checkout());

        Vestir();

        cartPanel.SetActive(false);
        Refresh(ShoppingCart.Instance.GetLines());
    }

    void ToggleCart()
    {
        isOpen = !isOpen;
        cartPanel.SetActive(isOpen);

        if (isOpen)
        {
            Refresh(ShoppingCart.Instance.GetLines());
        }
    }

    // El boton de comprar y el total, retocados desde aqui.
    //
    // Se hace por codigo y no a mano en la escena a proposito: asi el aspecto
    // vive junto al que construye las filas y no se van separando el uno del
    // otro cada vez que se toca algo.
    void Vestir()
    {
        if (totalText != null)
        {
            totalText.fontSize = 30f;
            totalText.fontStyle = FontStyles.Bold;
            totalText.color = TINTA;
            totalText.alignment = TextAlignmentOptions.Right;
        }

        if (checkoutButton == null) return;

        RectTransform r = checkoutButton.GetComponent<RectTransform>();
        if (r != null) UIFactory.Height(r, 58f);

        Image fondo = checkoutButton.GetComponent<Image>();

        if (fondo != null)
        {
            fondo.color = new Color(0.15f, 0.62f, 0.35f);
            fondo.sprite = UIFactory.RoundedSprite(12);
            fondo.type = Image.Type.Sliced;
        }

        // Que se note al pasar por encima y al pulsar. Un boton que no responde
        // parece que no funciona, y entonces se pulsa dos veces.
        ColorBlock cb = checkoutButton.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        cb.pressedColor = new Color(0.86f, 0.86f, 0.86f);
        cb.fadeDuration = 0.08f;
        checkoutButton.colors = cb;

        TextMeshProUGUI etiqueta = checkoutButton.GetComponentInChildren<TextMeshProUGUI>();

        if (etiqueta != null)
        {
            etiqueta.text = "COMPRAR";
            etiqueta.fontSize = 24f;
            etiqueta.fontStyle = FontStyles.Bold;
            etiqueta.characterSpacing = 6f;
            etiqueta.color = Color.white;
            etiqueta.alignment = TextAlignmentOptions.Center;
        }
    }

    public void Refresh(List<CartLine> lines)
    {
        foreach (Transform child in cartListContainer)
        {
            Destroy(child.gameObject);
        }

        int total = 0;
        int fila = 0;

        foreach (CartLine line in lines)
        {
            total += line.unitPrice * line.quantity;

            Fila(line, fila);
            fila++;
        }

        if (lines.Count == 0)
        {
            var vacio = UIFactory.Text("Vacio", cartListContainer, "El carrito esta vacio",
                                       18, SUAVE, TextAlignmentOptions.Center);
            UIFactory.Height(vacio.rectTransform, 60f);
        }

        if (totalText != null)
        {
            totalText.text = "Total  " + GameManager.Format(total);
        }

        if (miniTotalText != null)
        {
            miniTotalText.text = GameManager.Format(total);
        }
    }

    void Fila(CartLine line, int indice)
    {
        RectTransform raiz = UIFactory.Rect("Linea_" + line.itemName, cartListContainer);
        UIFactory.Height(raiz, altoFila);

        // Las filas pares con un fondo apenas visible. Es lo que deja seguir una
        // linea de izquierda a derecha sin perderse de renglon.
        Image fondo = UIFactory.Box("Fondo", raiz, indice % 2 == 0 ? FILA : FILA_ALT);
        UIFactory.Stretch(fondo.rectTransform, 0f, 0f, 0f, 0f);
        fondo.sprite = UIFactory.RoundedSprite(8);
        fondo.type = Image.Type.Sliced;
        fondo.raycastTarget = false;

        float x = 10f;

        // --- la foto -----------------------------------------------------
        Image imagen = UIFactory.Box("Foto", raiz, Color.white);
        Anclar(imagen.rectTransform, x, foto);
        imagen.preserveAspect = true;

        if (line.icon != null)
        {
            imagen.sprite = line.icon;
        }
        else
        {
            // Sin foto, un hueco marcado en vez de un cuadro blanco: un cuadro
            // blanco parece una imagen que no ha cargado.
            imagen.color = new Color(0f, 0f, 0f, 0.06f);
            imagen.sprite = UIFactory.RoundedSprite(6);
            imagen.type = Image.Type.Sliced;
        }

        x += foto + 12f;

        // --- el nombre, que se come lo que sobre ---------------------------
        float libre = anchoCantidad + anchoPrecio + anchoQuitar + 34f;

        var nombre = UIFactory.Text("Nombre", raiz, line.itemName, 19, TINTA,
                                    TextAlignmentOptions.Left);

        RectTransform n = nombre.rectTransform;
        n.anchorMin = new Vector2(0f, 0f);
        n.anchorMax = new Vector2(1f, 1f);
        n.offsetMin = new Vector2(x, 0f);
        n.offsetMax = new Vector2(-libre, 0f);
        nombre.enableWordWrapping = false;
        nombre.overflowMode = TextOverflowModes.Ellipsis;

        // --- cantidad, precio y quitar, pegados a la derecha ---------------
        float d = anchoQuitar + 12f;

        Button quitar = UIFactory.Button("Quitar", raiz, "x", 18,
                                         new Color(0f, 0f, 0f, 0.05f), QUITAR,
                                         () => ShoppingCart.Instance.RemoveOne(line.itemName));

        AnclarDerecha(quitar.GetComponent<RectTransform>(), d, anchoQuitar);
        d += anchoQuitar + 8f;

        var precio = UIFactory.Text("Precio", raiz,
                                    GameManager.Format(line.unitPrice * line.quantity),
                                    19, TINTA, TextAlignmentOptions.Right);
        AnclarDerecha(precio.rectTransform, d, anchoPrecio);
        precio.fontStyle = FontStyles.Bold;
        d += anchoPrecio + 8f;

        var cantidad = UIFactory.Text("Cantidad", raiz, "x" + line.quantity, 18, SUAVE,
                                      TextAlignmentOptions.Center);
        AnclarDerecha(cantidad.rectTransform, d, anchoCantidad);
    }

    static void Anclar(RectTransform r, float desdeIzquierda, float ancho)
    {
        r.anchorMin = new Vector2(0f, 0.5f);
        r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(ancho, ancho);
        r.anchoredPosition = new Vector2(desdeIzquierda, 0f);
    }

    static void AnclarDerecha(RectTransform r, float desdeDerecha, float ancho)
    {
        r.anchorMin = new Vector2(1f, 0f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(ancho, 0f);
        r.anchoredPosition = new Vector2(-desdeDerecha, 0f);
    }
}
