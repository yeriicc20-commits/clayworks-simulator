using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Los avisos de la esquina.
//
// Hay DOS niveles y la diferencia importa:
//
//   Aviso  - algo que el jugador tiene que saber. Sale en pantalla.
//   Nota   - charla. No sale; se queda en la consola por si hace falta.
//
// Antes todo era lo mismo y salia todo, asi que el cartel aparecia por llevar
// una caja en brazos, por comprar, por cada peluche que quedaba en la caja...
// Con un aviso saltando cada dos por tres, el que de verdad importa (que no
// llegas al dinero) pasa desapercibido entre los otros veinte.
//
// Y sale en una esquina, pequeno, no cruzado en mitad de la pantalla.
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance;

    [Tooltip("Sin usar. Se conserva por si la escena todavia lo tiene asignado: "
             + "el cartel se construye aqui y no depende de el.")]
    public TextMeshProUGUI notificationText;

    public float displayDuration = 2.6f;

    [Header("Aspecto")]
    [Tooltip("A que distancia de la esquina de arriba a la derecha.")]
    public Vector2 margen = new Vector2(28f, 28f);

    public float ancho = 330f;
    public float fuente = 17f;
    public float fundido = 0.18f;

    RectTransform tarjeta;
    CanvasGroup grupo;
    TextMeshProUGUI texto;
    Coroutine enMarcha;

    void Awake()
    {
        Instance = this;
        Construir();
    }

    // Algo que hay que ver: no llegas al dinero, la entrega ha fallado, subes
    // de nivel.
    public static void Aviso(string mensaje)
    {
        if (Instance != null) Instance.ShowMessage(mensaje);
    }

    // Charla: que llevas una caja, que quedan siete peluches, que la compra ha
    // salido bien. Se ve en el propio juego, no hace falta un cartel.
    public static void Nota(string mensaje)
    {
        Debug.Log("[Aviso] " + mensaje);
    }

    public void ShowMessage(string message)
    {
        if (texto == null) return;

        if (enMarcha != null) StopCoroutine(enMarcha);

        enMarcha = StartCoroutine(Mostrar(message));
    }

    void Construir()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler escala = gameObject.GetComponent<CanvasScaler>();
        if (escala == null) escala = gameObject.AddComponent<CanvasScaler>();

        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);

        grupo = gameObject.GetComponent<CanvasGroup>();
        if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();

        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        Image fondo = UIFactory.Box("Aviso", transform, new Color(0.09f, 0.10f, 0.12f, 0.88f));
        fondo.sprite = UIFactory.RoundedSprite(10);
        fondo.type = Image.Type.Sliced;

        tarjeta = fondo.rectTransform;

        // Arriba a la derecha: fuera de donde se mira para jugar.
        tarjeta.anchorMin = new Vector2(1f, 1f);
        tarjeta.anchorMax = new Vector2(1f, 1f);
        tarjeta.pivot = new Vector2(1f, 1f);
        tarjeta.sizeDelta = new Vector2(ancho, 52f);
        tarjeta.anchoredPosition = new Vector2(-margen.x, -margen.y);

        // Un filo de color a la izquierda. Es lo que hace que se lea como un
        // aviso del juego y no como un texto suelto encima de la imagen.
        Image filo = UIFactory.Box("Filo", fondo.transform, new Color(0.98f, 0.76f, 0.18f, 1f));
        RectTransform f = filo.rectTransform;
        f.anchorMin = new Vector2(0f, 0f);
        f.anchorMax = new Vector2(0f, 1f);
        f.pivot = new Vector2(0f, 0.5f);
        f.offsetMin = new Vector2(0f, 6f);
        f.offsetMax = new Vector2(4f, -6f);

        texto = UIFactory.Text("Texto", fondo.transform, "", Mathf.RoundToInt(fuente),
                               new Color(0.93f, 0.94f, 0.96f), TextAlignmentOptions.Left);

        UIFactory.Stretch(texto.rectTransform, 16f, 8f, 14f, 8f);
        texto.enableWordWrapping = true;
    }

    IEnumerator Mostrar(string mensaje)
    {
        texto.text = mensaje;

        // La tarjeta se ajusta a lo que ocupe el texto, para que un aviso corto
        // no salga en una caja de dos lineas medio vacia.
        yield return null;

        float alto = Mathf.Max(44f, texto.preferredHeight + 18f);
        tarjeta.sizeDelta = new Vector2(ancho, alto);

        yield return Fundir(1f);
        yield return new WaitForSeconds(displayDuration);
        yield return Fundir(0f);

        enMarcha = null;
    }

    IEnumerator Fundir(float destino)
    {
        float desde = grupo.alpha;
        float t = 0f;

        while (t < fundido)
        {
            t += Time.deltaTime;
            grupo.alpha = Mathf.Lerp(desde, destino, t / fundido);
            yield return null;
        }

        grupo.alpha = destino;
    }
}
