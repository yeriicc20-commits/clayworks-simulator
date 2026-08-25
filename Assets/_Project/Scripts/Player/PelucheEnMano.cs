using UnityEngine;

// El peluche que el jugador lleva en la mano, y el modo de mirarlo de cerca.
//
// Las dos cosas viven juntas porque son la misma: el modo de inspeccion no
// existe sin un peluche en la mano, y el sitio donde se sujeta es el mismo que
// se mueve hacia el centro de la vista al inspeccionarlo. Separarlas obligaba a
// que una le preguntara a la otra por el estado en cada fotograma.
//
// No se toca la fisica del peluche mientras se lleva: se apaga y punto. Un
// rigidbody colgando de la camara es un cuerpo movido por transform, que es
// justo lo que tuvo a la garra dando tumbos media sesion.
public class PelucheEnMano : MonoBehaviour
{
    public static PelucheEnMano Instancia;

    [Header("Como se sujeta")]
    public Vector3 sitioEnMano = new Vector3(0.28f, -0.22f, 0.55f);
    public Vector3 giroEnMano = new Vector3(-6f, 18f, 4f);

    [Header("Inspeccion")]
    [Tooltip("Boton del raton que entra y sale del modo mirar. 2 = rueda.")]
    public int botonInspeccion = 2;

    [Tooltip("A que distancia de la camara se pone al inspeccionarlo.")]
    public float distanciaMirar = 0.55f;

    [Tooltip("Lo mas cerca que llega con el zoom. Es un acercamiento corto a "
             + "proposito: pegarselo a la cara no deja ver la silueta.")]
    public float distanciaZoom = 0.36f;

    public float velocidadZoom = 4f;
    public float suavizado = 10f;

    [Tooltip("Grados de giro por unidad de raton.")]
    public float sensibilidadGiro = 220f;

    public bool Inspeccionando { get; private set; }

    PlushItem sostenido;
    Transform mano;
    Camera ojo;

    Quaternion giroMirado = Quaternion.identity;
    float distanciaActual;
    Vector3 escalaOriginal = Vector3.one;

    public static bool LlevaPeluche
    {
        get { return Instancia != null && Instancia.sostenido != null; }
    }

    public bool Sostiene(PlushItem p)
    {
        return sostenido != null && sostenido == p;
    }

    public static void Coger(PlushItem peluche)
    {
        Preparar();

        if (Instancia != null) Instancia.Agarrar(peluche);
    }

    static void Preparar()
    {
        if (Instancia != null) return;

        Instancia = FindAnyObjectByType<PelucheEnMano>();
        if (Instancia != null) return;

        GameObject go = new GameObject("PelucheEnMano");
        Instancia = go.AddComponent<PelucheEnMano>();
    }

    void Awake()
    {
        Instancia = this;
    }

    void MontarMano()
    {
        if (mano != null) return;

        if (ojo == null) ojo = Camera.main;

        if (ojo == null)
        {
            Debug.LogError("[Peluche] No encuentro la camara del jugador.", this);
            return;
        }

        GameObject punto = new GameObject("PelucheHoldPoint");
        punto.transform.SetParent(ojo.transform, false);

        mano = punto.transform;
    }

    void Agarrar(PlushItem peluche)
    {
        if (peluche == null || sostenido != null) return;

        MontarMano();
        if (mano == null) return;

        sostenido = peluche;
        escalaOriginal = peluche.transform.localScale;

        peluche.isGrabbed = true;

        Fisica(peluche, false);

        peluche.transform.SetParent(mano, false);
        peluche.transform.localPosition = sitioEnMano;
        peluche.transform.localRotation = Quaternion.Euler(giroEnMano);

        Inspeccionando = false;
        distanciaActual = distanciaMirar;

        if (NotificationManager.Instance != null)
            NotificationManager.Instance.ShowMessage("Peluche en la mano");
    }

    public void Soltar()
    {
        if (sostenido == null) return;

        SalirDeInspeccion();

        PlushItem peluche = sostenido;
        sostenido = null;

        peluche.transform.SetParent(null, true);
        peluche.transform.localScale = escalaOriginal;
        peluche.isGrabbed = false;

        Fisica(peluche, true);

        InteractionUI.Hide();
    }

    void Update()
    {
        if (sostenido == null) return;
        if (CursorMode.FreeCursor) return;

        if (Input.GetMouseButtonDown(botonInspeccion))
        {
            if (Inspeccionando) SalirDeInspeccion();
            else EntrarEnInspeccion();
        }

        if (!Inspeccionando)
        {
            InteractionUI.Prompt("Clic central para mirar el peluche - "
                                 + "G para dejarlo");

            if (Input.GetKeyDown(KeyCode.G)) Soltar();
            return;
        }

        Inspeccion();
    }

    void EntrarEnInspeccion()
    {
        Inspeccionando = true;

        // El raton pasa a girar el peluche, asi que la camara no puede seguir
        // girando con el. No se suelta el cursor: si se soltara, el jugador
        // veria la flecha del raton y perderia el centro de la pantalla.
        FirstPersonController.LookLocked = true;

        giroMirado = sostenido.transform.rotation;
        distanciaActual = distanciaMirar;
    }

    void SalirDeInspeccion()
    {
        if (!Inspeccionando) return;

        Inspeccionando = false;
        FirstPersonController.LookLocked = false;

        // Vuelve a la mano. Se deja que llegue solo con el suavizado de abajo en
        // vez de teletransportarlo: si aparece de golpe en la esquina, parece
        // que se ha roto algo.
        if (sostenido != null) sostenido.transform.localScale = escalaOriginal;
    }

    void Inspeccion()
    {
        InteractionUI.Prompt("Mueve el raton para girarlo - clic derecho para "
                             + "acercar - Escape para salir");

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SalirDeInspeccion();
            return;
        }

        // Girar. Se acumula sobre el giro que ya tenia, en ejes de la CAMARA y
        // no del peluche: girando en ejes propios, en cuanto le das dos vueltas
        // los controles se invierten y no hay quien lo maneje.
        float mx = Input.GetAxis("Mouse X") * sensibilidadGiro * Time.deltaTime;
        float my = Input.GetAxis("Mouse Y") * sensibilidadGiro * Time.deltaTime;

        Transform camara = ojo != null ? ojo.transform : transform;

        giroMirado = Quaternion.AngleAxis(-mx, camara.up)
                     * Quaternion.AngleAxis(my, camara.right)
                     * giroMirado;

        // Zoom corto mientras se mantiene el clic derecho.
        float objetivo = Input.GetMouseButton(1) ? distanciaZoom : distanciaMirar;

        distanciaActual = Mathf.MoveTowards(distanciaActual, objetivo,
                                            velocidadZoom * Time.deltaTime);

        Vector3 destino = camara.position + camara.forward * distanciaActual;

        float k = 1f - Mathf.Exp(-Time.deltaTime * suavizado);

        sostenido.transform.position = Vector3.Lerp(sostenido.transform.position, destino, k);
        sostenido.transform.rotation = giroMirado;
    }

    void LateUpdate()
    {
        // Fuera del modo mirar, el peluche vuelve a su sitio en la mano poco a
        // poco. Es lo que hace que salir de la inspeccion se vea como que lo
        // bajas, y no como un corte.
        if (sostenido == null || Inspeccionando || mano == null) return;

        float k = 1f - Mathf.Exp(-Time.deltaTime * suavizado);

        sostenido.transform.localPosition =
            Vector3.Lerp(sostenido.transform.localPosition, sitioEnMano, k);

        sostenido.transform.localRotation =
            Quaternion.Slerp(sostenido.transform.localRotation,
                             Quaternion.Euler(giroEnMano), k);
    }

    void Fisica(PlushItem peluche, bool encendida)
    {
        Rigidbody rb = peluche.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Primero quitar el kinematic: tocar la velocidad de un rigidbody
            // kinematico es un error en Unity.
            rb.isKinematic = !encendida;
            rb.detectCollisions = encendida;
            rb.useGravity = encendida;

            if (encendida)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        foreach (Collider col in peluche.GetComponentsInChildren<Collider>())
        {
            col.enabled = encendida;
        }
    }

    void OnDisable()
    {
        if (Inspeccionando) SalirDeInspeccion();
    }
}
