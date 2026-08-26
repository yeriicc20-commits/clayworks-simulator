using UnityEngine;

// Llevar una caja en brazos. Con la caja encima, E saca lo que hay dentro para
// colocarlo y otra E lo vuelve a guardar. La caja se puede soltar y recoger.
public class BoxCarrier : MonoBehaviour
{
    public static BoxCarrier Instance;

    [Header("Como se sujeta")]
    public Transform holdPoint;
    public Vector3 heldLocalPosition = new Vector3(0.32f, -0.3f, 0.6f);
    public Vector3 heldLocalEuler = new Vector3(-8f, 22f, 6f);
    public float heldScale = 0.35f;

    [Header("Controles")]
    [Tooltip("Boton del raton para dejar la caja en el suelo. 0 = izquierdo.")]
    public int dropMouseButton = 0;

    // Las teclas ya no son campos: las elige el jugador en el menu de
    // ajustes. Se dejan como propiedades con el mismo nombre para que todo
    // lo que las usa siga leyendose igual, carteles de ayuda incluidos.
    static KeyCode useKey { get { return AjustesControles.Tecla(AjustesControles.Accion.Usar); } }
    static KeyCode throwKey { get { return AjustesControles.Tecla(AjustesControles.Accion.Lanzar); } }

    [Header("Lanzar")]
    [Tooltip("Cuanto hay que mantener la tecla para llegar al maximo.")]
    public float cargaCompleta = 1.4f;

    [Tooltip("Velocidad de salida con la barra a cero, en m/s. Es un empujon: "
             + "soltar la tecla al instante no puede dejar la caja clavada en el "
             + "aire.")]
    public float velocidadMinima = 3f;

    [Tooltip("Velocidad de salida con la barra al maximo. Nueve metros por "
             + "segundo cruzan el local en un segundo largo, que es lo lejos que "
             + "tiene sentido tirar una caja de carton.")]
    public float velocidadMaxima = 9f;

    private float carga = 0f;

    public bool IsCarrying { get { return carried != null; } }
    public bool IsDeploying { get; private set; }
    public bool IsPlacingBox { get; private set; }

    private CarriableBox carried;
    private Vector3 originalScale = Vector3.one;
    private int pickedUpFrame = -1;
    private string shownHint = "";
    private bool hintVisible = false;

    void Awake()
    {
        Instance = this;

        if (holdPoint == null) BuildHoldPoint();
    }

    // No hace falta ponerlo en la escena: si no existe, se monta solo colgando
    // de la camara del jugador.
    public static BoxCarrier EnsureExists()
    {
        if (Instance != null) return Instance;

        BoxCarrier existing = FindAnyObjectByType<BoxCarrier>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject holder = new GameObject("BoxCarrier");

        return holder.AddComponent<BoxCarrier>();
    }

    void BuildHoldPoint()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("[BoxCarrier] No encuentro la camara del jugador (tag MainCamera).", this);
            return;
        }

        GameObject point = new GameObject("BoxHoldPoint");
        point.transform.SetParent(cam.transform, false);
        point.transform.localPosition = Vector3.zero;
        point.transform.localRotation = Quaternion.identity;

        holdPoint = point.transform;
    }

    public bool IsCarryingThis(CarriableBox box)
    {
        return carried != null && carried == box;
    }

    public bool Carry(CarriableBox box)
    {
        if (carried != null || box == null) return false;

        if (holdPoint == null)
        {
            Debug.LogError("[BoxCarrier] Falta holdPoint: sin el no hay donde sujetar la caja.", this);
            return false;
        }

        carried = box;
        originalScale = box.transform.localScale;

        // Sin esto, la misma pulsacion que la recoge la desplegaria acto seguido.
        pickedUpFrame = Time.frameCount;

        SetPhysicsEnabled(box, false);

        box.transform.SetParent(holdPoint, false);
        box.transform.localPosition = heldLocalPosition;
        box.transform.localRotation = Quaternion.Euler(heldLocalEuler);
        box.transform.localScale = originalScale * heldScale;

        NotificationManager.Nota("Caja en brazos");

        return true;
    }

    void Update()
    {
        if (carried == null)
        {
            // Si la caja se ha ido por otro sitio (consumida, colocada) en mitad
            // de una carga, la barra se queda encendida para siempre.
            if (carga > 0f)
            {
                carga = 0f;
                MedidorFuerza.Ocultar();
            }

            HideHint();
            return;
        }

        if (Time.frameCount == pickedUpFrame) return;

        if (IsDeploying)
        {
            ShowHint("Clic izquierdo para colocar - " + AjustesControles.NombreTecla(useKey) + " para guardarlo en la caja");

            if (AjustesControles.Pulsando(AjustesControles.Accion.Usar)) PlacementManager.Instance.CancelPlacement();
            return;
        }

        if (IsPlacingBox)
        {
            ShowHint("Clic izquierdo para dejar la caja aqui - " + AjustesControles.NombreTecla(useKey) + " para volver a cogerla");

            if (AjustesControles.Pulsando(AjustesControles.Accion.Usar)) PlacementManager.Instance.CancelPlacement();
            return;
        }

        // Cargar el tiro se mira ANTES que nada. Mientras se esta apuntando no
        // se deja usar la caja ni soltarla: si no, el clic de "meter peluches"
        // se colaba en mitad de la carga y te quedabas apuntando sin caja.
        if (Cargando()) return;

        ShowHint(carried.CarryHint + " - manten " + AjustesControles.NombreTecla(throwKey) + " para lanzarla");

        // La caja tiene prioridad: si consume el clic (meter peluches en una
        // maquina) no se interpreta como dejarla en el suelo.
        if (carried.HandleCarryInput(this)) return;

        if (AjustesControles.Pulsando(AjustesControles.Accion.Usar)) carried.OnUseKey(this);
        else if (Input.GetMouseButtonDown(dropMouseButton)) Drop();
    }

    // Devuelve true mientras se esta apuntando el tiro.
    bool Cargando()
    {
        if (AjustesControles.Pulsada(AjustesControles.Accion.Lanzar))
        {
            carga = Mathf.Min(carga + Time.deltaTime, cargaCompleta);

            MedidorFuerza.Mostrar(carga / cargaCompleta);
            ShowHint("Suelta " + AjustesControles.NombreTecla(throwKey) + " para lanzar la caja");

            return true;
        }

        if (carga <= 0f) return false;

        // Se ha soltado la tecla: sale la caja con lo que hubiera acumulado.
        float fuerza = carga / cargaCompleta;
        carga = 0f;

        MedidorFuerza.Ocultar();
        Lanzar(fuerza);

        return true;
    }

    void Lanzar(float fuerza)
    {
        CarriableBox caja = carried;
        if (caja == null) return;

        carried = null;
        HideHint();

        caja.transform.SetParent(null, true);
        caja.transform.localScale = originalScale;

        Camera cam = Camera.main;
        Transform ojo = cam != null ? cam.transform : transform;

        Vector3 mirada = ojo.forward;

        // Fuera del cuerpo antes de soltarla. En la mano va a escala reducida y
        // al recuperar su tamano crece alrededor de su pivote: si se suelta ahi
        // mismo, media caja queda dentro del jugador y PhysX la escupe.
        Vector3 plano = new Vector3(mirada.x, 0f, mirada.z);
        if (plano.sqrMagnitude < 0.0001f) plano = Vector3.forward;

        Vector3 tam = ApiladorCajas.Tamano(caja.gameObject);
        float fondo = Mathf.Max(tam.x, tam.z) * 0.5f;

        caja.transform.position = ojo.position + plano.normalized * (0.45f + fondo)
                                  + Vector3.up * 0.05f;

        SetPhysicsEnabled(caja, true);

        Rigidbody rb = caja.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Algo hacia arriba. Un tiro completamente recto se estrella a dos
        // metros y no parece que la hayas lanzado, parece que se te ha caido.
        Vector3 salida = (mirada + Vector3.up * 0.22f).normalized;

        rb.linearVelocity = salida * Mathf.Lerp(velocidadMinima, velocidadMaxima, fuerza);

        // Un poco de vuelta sobre si misma, que es lo que hace una caja tirada a
        // mano. Poca: girando mucho parece un dado.
        rb.angularVelocity = ojo.right * Random.Range(-2.2f, -0.6f);

        // Barrido continuo mientras vuela. A nueve metros por segundo recorre 15
        // cm en un paso de fisica, y con deteccion discreta eso se cuela por una
        // pared fina sin enterarse.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    // Saca el contenido: la caja desaparece de las manos mientras colocas.
    public void DeployContents(GameObject contents)
    {
        if (carried == null) return;

        if (contents == null)
        {
            NotificationManager.Instance.ShowMessage("Esta caja esta vacia");
            return;
        }

        IsDeploying = true;
        carried.gameObject.SetActive(false);

        PlacementManager.Instance.StartPlacement(contents, OnContentPlaced, ReturnToArms);
    }

    // La caja se ha gastado del todo mientras la llevabas.
    public void ConsumeCarriedBox()
    {
        if (carried == null) return;

        Destroy(carried.gameObject);
        carried = null;

        HideHint();
    }

    // Cancelaste la colocacion: la caja vuelve a los brazos con su contenido.
    void ReturnToArms()
    {
        IsDeploying = false;

        if (carried != null) carried.gameObject.SetActive(true);
    }

    // Colocado de verdad: la caja ya no hace falta.
    void OnContentPlaced()
    {
        IsDeploying = false;

        if (carried != null) Destroy(carried.gameObject);

        carried = null;
    }

    // Dejar la caja se coloca igual que una maquina: fantasma, rejilla y clic.
    public void Drop()
    {
        if (carried == null) return;

        IsPlacingBox = true;

        // El fantasma se clona de la caja, asi que primero le devolvemos su
        // tamano real y la sacamos de las manos.
        carried.transform.SetParent(null, true);
        carried.transform.localScale = originalScale;

        PlacementManager.Instance.StartMoving(carried.gameObject, OnBoxPlaced, OnBoxPlacementCancelled);

        carried.gameObject.SetActive(false);
    }

    void OnBoxPlaced(Vector3 position, Quaternion rotation)
    {
        IsPlacingBox = false;

        if (carried == null) return;

        carried.gameObject.SetActive(true);
        carried.transform.SetPositionAndRotation(position, rotation);

        SetPhysicsEnabled(carried, true);

        // Con el fantasma la caja aparece ya apoyada y no llega a chocar con
        // nada, asi que el sonido no saltaria solo. Se pide flojito, que es lo
        // que suena dejar una caja en el suelo con cuidado.
        GolpeCaja ruido = carried.GetComponent<GolpeCaja>();
        if (ruido != null) ruido.Sonar(ruido.velocidadMinima + 0.3f);

        carried = null;
    }

    void OnBoxPlacementCancelled()
    {
        IsPlacingBox = false;

        if (carried == null) return;

        carried.gameObject.SetActive(true);

        carried.transform.SetParent(holdPoint, false);
        carried.transform.localPosition = heldLocalPosition;
        carried.transform.localRotation = Quaternion.Euler(heldLocalEuler);
        carried.transform.localScale = originalScale * heldScale;
    }

    // El aviso se queda fijo mientras lleves la caja: no depende de acordarse.
    void ShowHint(string message)
    {
        hintVisible = true;

        if (shownHint == message) return;

        shownHint = message;
        if (InteractionUI.Instance != null) InteractionUI.Instance.ShowPrompt(message);
    }

    void HideHint()
    {
        if (!hintVisible) return;

        hintVisible = false;
        shownHint = "";
        if (InteractionUI.Instance != null) InteractionUI.Instance.HidePrompt();
    }

    void SetPhysicsEnabled(CarriableBox box, bool enabled)
    {
        Rigidbody rb = box.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Primero quitar el kinematic: tocar la velocidad de un rigidbody
            // kinematico es un error en Unity.
            rb.isKinematic = !enabled;
            rb.detectCollisions = enabled;

            if (enabled)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Si al reaparecer roza algo, que se separe despacio en vez de
                // salir rebotada.
                rb.maxDepenetrationVelocity = 0.5f;
            }
        }

        foreach (Collider col in box.GetComponentsInChildren<Collider>())
        {
            col.enabled = enabled;
        }
    }
}
