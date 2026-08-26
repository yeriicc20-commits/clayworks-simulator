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

    // Las medidas y los colores de las filas viven en FilaLista, que es quien
    // las dibuja. Tenerlos aqui repetidos es como acaban la tienda y el
    // carrito pareciendo dos programas distintos.

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
            totalText.color = FilaLista.Tinta;
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
                                       18, FilaLista.Suave, TextAlignmentOptions.Center);
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
        FilaLista.Crear(cartListContainer, indice, line.itemName, line.icon,
                        "x" + line.quantity, line.unitPrice * line.quantity,
                        "Quitar", new Color(0.72f, 0.30f, 0.28f),
                        () => ShoppingCart.Instance.RemoveOne(line.itemName));
    }

}
