using UnityEngine;
using UnityEngine.UI;

// La barra de fuerza de lanzar una caja.
//
// Se monta sola la primera vez que hace falta, igual que el panel de sonido: no
// hay que acordarse de ponerla en la escena ni de reasignarla si se rehace el
// canvas.
//
// Sin numeros a proposito. Un "73%" invita a mirar la cifra en vez de la caja, y
// lo que se quiere es que el jugador aprenda a calcular el tiro a ojo. El color
// dice lo mismo y no hay que leerlo.
public class MedidorFuerza : MonoBehaviour
{
    static MedidorFuerza instancia;

    // El recorrido de color: de poca fuerza a maxima. Termina en rojo porque el
    // rojo es lo unico que todo el mundo lee como "cuidado, esto es el tope".
    static readonly Color FLOJO = new Color(0.42f, 0.80f, 0.35f);
    static readonly Color MEDIO = new Color(0.95f, 0.78f, 0.22f);
    static readonly Color FUERTE = new Color(0.88f, 0.16f, 0.12f);

    RectTransform barra;
    Image relleno;
    CanvasGroup grupo;

    // El ancho se guarda al construirla. Preguntarselo al RectTransform en
    // caliente devuelve cero hasta que el layout ha pasado una vez, y la
    // primera pulsacion de la tecla pintaba la barra vacia.
    float ancho;

    public static void Mostrar(float t)
    {
        Preparar();
        instancia.Pintar(Mathf.Clamp01(t));
    }

    public static void Ocultar()
    {
        if (instancia == null) return;

        instancia.grupo.alpha = 0f;
    }

    static void Preparar()
    {
        if (instancia != null) return;

        instancia = FindAnyObjectByType<MedidorFuerza>();
        if (instancia != null) return;

        GameObject go = new GameObject("MedidorFuerza");
        instancia = go.AddComponent<MedidorFuerza>();
    }

    void Awake()
    {
        instancia = this;
        Construir();
    }

    void Construir()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Por encima del aviso de "pulsa E", que va en el canvas normal.
        canvas.sortingOrder = 60;

        CanvasScaler escala = gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);

        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        // Encima del centro de la pantalla y no abajo del todo: la caja se lanza
        // mirando, y la vista no tiene que bajar a buscar la barra.
        Image marco = UIFactory.Box("Marco", transform, new Color(0f, 0f, 0f, 0.55f));
        marco.sprite = UIFactory.RoundedSprite(10);
        marco.type = Image.Type.Sliced;

        RectTransform m = marco.rectTransform;
        m.anchorMin = new Vector2(0.5f, 0.5f);
        m.anchorMax = new Vector2(0.5f, 0.5f);
        m.pivot = new Vector2(0.5f, 0.5f);
        m.sizeDelta = new Vector2(320f, 26f);
        ancho = 320f - 10f;
        m.anchoredPosition = new Vector2(0f, -150f);

        Image fondo = UIFactory.Box("Hueco", marco.transform, new Color(1f, 1f, 1f, 0.12f));
        UIFactory.Stretch(fondo.rectTransform, 5f, 5f, 5f, 5f);

        relleno = UIFactory.Box("Relleno", fondo.transform, FLOJO);

        barra = relleno.rectTransform;
        barra.anchorMin = new Vector2(0f, 0f);
        barra.anchorMax = new Vector2(0f, 1f);
        barra.pivot = new Vector2(0f, 0.5f);
        barra.offsetMin = Vector2.zero;
        barra.offsetMax = Vector2.zero;
        barra.sizeDelta = new Vector2(0f, 0f);
    }

    void Pintar(float t)
    {
        grupo.alpha = 1f;

        barra.sizeDelta = new Vector2(ancho * t, 0f);

        // Dos tramos y no uno: mezclando verde con rojo de golpe, el medio sale
        // marron y no se lee como "vas por la mitad".
        relleno.color = t < 0.5f
            ? Color.Lerp(FLOJO, MEDIO, t * 2f)
            : Color.Lerp(MEDIO, FUERTE, (t - 0.5f) * 2f);
    }
}
