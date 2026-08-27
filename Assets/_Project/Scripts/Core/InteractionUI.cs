using UnityEngine;
using UnityEngine.UI;
using TMPro;

// El aviso de "Pulsa E para..." de la parte de abajo de la pantalla.
//
// La colocacion y el tamano se ajustan por codigo al arrancar: en la escena
// venia clavado en el centro exacto y con fuente 36, que tapaba media pantalla
// y ademas se movia al cambiar la resolucion, porque estaba anclado al centro
// del canvas en vez de al borde de abajo.
public class InteractionUI : MonoBehaviour
{
    public static InteractionUI Instance;

    public GameObject promptRoot;
    public TextMeshProUGUI promptTextComponent;

    [Header("Aspecto")]
    [Tooltip("A que altura sobre el borde de abajo se pone el aviso.")]
    public float bottomOffset = 120f;
    public float fontSize = 20f;

    [Tooltip("Fondo detras del texto, para que se lea sobre cualquier cosa.")]
    public bool showBackdrop = true;
    public Color backdropColor = new Color(0f, 0f, 0f, 0.55f);
    public Vector2 backdropPadding = new Vector2(22f, 10f);

    private RectTransform backdrop;
    private bool styled = false;

    void Awake()
    {
        Instance = this;
    }

    // Atajos que aguantan que todavia no exista.
    //
    // El singleton se rellena en Awake, y el orden entre componentes no esta
    // garantizado: quien pregunte antes se come un NullReference. Estaba
    // petando en bucle en la maquina de garra. Con esto, el peor caso es que un
    // aviso no salga durante un fotograma, en vez de una excepcion por frame.
    // El aviso caduca solo al final del fotograma.
    //
    // Antes cada componente tenia que acordarse de apagar el suyo, y eso
    // fallaba por los dos lados. El que se olvidaba dejaba el cartel pegado
    // aunque te fueras -- le paso al interruptor. Y el que se acordaba
    // borraba tambien el de los demas: si la maquina pedia el suyo y despues,
    // en el mismo fotograma, otro componente llamaba a Hide por estar lejos,
    // el de la maquina desaparecia sin que nadie hubiera hecho nada mal.
    //
    // Pidiendolo cada fotograma el que lo quiera, y apagandose solo si no lo
    // pide nadie, las dos cosas se arreglan y no hay nada que recordar.
    public static void Prompt(string texto)
    {
        if (Instance == null) return;

        Instance.pedido = texto;
        Instance.frameDelPedido = Time.frameCount;
    }

    // Ya no hace falta llamarla, y por eso no hace nada.
    //
    // Se deja porque hay media docena de sitios que la llaman al alejarse, y
    // eso ahora lo resuelve la caducidad. Vaciarla es justo lo que quita la
    // pelea entre unos y otros; borrarla obligaria a tocar seis ficheros para
    // no ganar nada.
    public static void Hide()
    {
    }

    string pedido;
    int frameDelPedido = -1;

    // En LateUpdate: todos los Update ya han tenido su turno de pedirlo.
    void LateUpdate()
    {
        if (frameDelPedido == Time.frameCount) ShowPrompt(pedido);
        else HidePrompt();
    }

    void Start()
    {
        ApplyStyle();

        // Que no se quede visible de arranque si la escena lo dejo encendido.
        HidePrompt();
    }

    void ApplyStyle()
    {
        if (styled || promptTextComponent == null) return;

        RectTransform rect = promptTextComponent.rectTransform;
        RectTransform parent = rect.parent as RectTransform;

        if (parent == null) return;

        promptTextComponent.fontSize = fontSize;
        promptTextComponent.alignment = TextAlignmentOptions.Center;
        promptTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        promptTextComponent.raycastTarget = false;

        PinBottom(rect, fontSize * 1.8f);

        if (!showBackdrop) { styled = true; return; }

        Image image = UIFactory.Box("PromptFondo", parent, backdropColor);

        image.sprite = UIFactory.RoundedSprite(10);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        backdrop = image.rectTransform;

        PinBottom(backdrop, fontSize * 1.8f);

        // Detras del texto: en la interfaz de Unity manda el orden de la
        // jerarquia, y lo que va antes se dibuja debajo.
        backdrop.SetSiblingIndex(rect.GetSiblingIndex());

        styled = true;
    }

    // Anclado al borde de abajo de verdad, no colocado a ojo desde el centro:
    // asi se queda donde tiene que estar a cualquier resolucion.
    void PinBottom(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(0f, bottomOffset);
    }

    public void ShowPrompt(string message)
    {
        ApplyStyle();

        // Encender antes de medir: con el objeto apagado TMP no tiene la malla
        // montada y el ancho preferido sale a cero.
        if (promptRoot != null) promptRoot.SetActive(true);
        if (backdrop != null) backdrop.gameObject.SetActive(true);

        if (promptTextComponent != null) promptTextComponent.text = message;

        Resize(message);
    }

    // El fondo se ajusta a lo que ocupa el texto: un recuadro fijo dejaria
    // mensajes cortos nadando y cortaria los largos.
    void Resize(string message)
    {
        if (promptTextComponent == null) return;

        float width = promptTextComponent.GetPreferredValues(message).x;

        RectTransform rect = promptTextComponent.rectTransform;

        rect.sizeDelta = new Vector2(width + 4f, rect.sizeDelta.y);

        if (backdrop != null)
        {
            backdrop.sizeDelta = new Vector2(width + backdropPadding.x * 2f, fontSize * 1.8f + backdropPadding.y);
        }
    }

    public void HidePrompt()
    {
        if (promptRoot != null) promptRoot.SetActive(false);
        if (backdrop != null) backdrop.gameObject.SetActive(false);
    }
}
