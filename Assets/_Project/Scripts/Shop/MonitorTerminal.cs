using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// El ordenador del mostrador. La tienda vive pegada a su pantalla y se queda
// siempre encendida: al salir sigues viendo desde lejos lo que dejaste abierto.
public class MonitorTerminal : MonoBehaviour
{
    public static bool InUse { get; private set; }

    [Header("Que objeto es el monitor")]
    [Tooltip("Nombre del objeto en la jerarquia. Se busca al arrancar si no asignas monitorObject.")]
    public string monitorObjectName = "Monitor 2";
    public Transform monitorObject;

    [Header("Colocacion")]
    [Tooltip("Desactivalo para colocar el canvas a mano en la escena: el script no lo tocara.")]
    public bool autoPlaceScreen = true;
    [Tooltip("Vacio = se calcula solo a partir de la caja del modelo.")]
    public Transform screenAnchor;
    [Tooltip("Ancho de la zona negra en metros. 0 = se mide del modelo.")]
    public float screenWidthMeters = 0f;
    [Tooltip("Alto de la zona negra en metros. 0 = se mide del modelo.")]
    public float screenHeightMeters = 0f;
    [Tooltip("Margen respecto a lo medido. 1 = ocupa todo.")]
    [Range(0.4f, 1f)] public float screenInset = 1f;
    [Tooltip("Ancho de emergencia si no se puede deducir nada del modelo.")]
    public float fallbackScreenWidth = 0.3f;
    [Tooltip("Marca esto si la interfaz sale en espejo: le da media vuelta.")]
    public bool flipScreenFacing = false;
    [Tooltip("Ajuste fino de la pantalla, en metros y en el espacio del anchor.")]
    public Vector3 anchorOffset = Vector3.zero;
    [Tooltip("Separacion del canvas respecto al cristal, para que no haga z-fighting.")]
    public float screenDepthOffset = 0.005f;

    [Header("Camara")]
    [Tooltip("Desde donde se mira la pantalla al usarla. Vacio = se calcula solo.")]
    public Transform viewPoint;
    public float viewDistance = 0.32f;
    public float focusSpeed = 7f;

    [Header("Interaccion")]
    public float interactDistance = 2.5f;
    [Tooltip("Margen sobre el rectangulo de la pantalla. 1 = justo el borde.")]
    [Range(0.5f, 1.5f)] public float aimMargin = 1f;
    public int useMouseButton = 0;
    public KeyCode exitKey = KeyCode.Escape;

    [Header("Referencias (se buscan solas si las dejas vacias)")]
    public RectTransform worldCanvas;
    public ShopUI shopUI;
    public Camera playerCamera;
    public FirstPersonController playerController;

    private float worldScreenWidth = 0f;
    private float worldScreenHeight = 0f;
    private Vector3 savedCamLocalPosition;
    private Quaternion savedCamLocalRotation;
    private bool returning = false;
    private float returnDeadline = 0f;
    private bool promptShown = false;

    void Awake()
    {
        InUse = false;
    }

    void Start()
    {
        if (shopUI == null) shopUI = FindAnyObjectByType<ShopUI>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerController == null) playerController = FindAnyObjectByType<FirstPersonController>();

        // Despues de resolver la camara: el desempate de orientacion la usa.
        ResolveMonitor();

        if (worldCanvas == null && shopUI != null && shopUI.shopPanel != null)
        {
            Canvas canvas = shopUI.shopPanel.GetComponentInParent<Canvas>();
            if (canvas != null) worldCanvas = canvas.GetComponent<RectTransform>();
        }

        ReportMissingReferences();

        AttachCanvasToScreen();
        BuildViewPoint();

        // El monitor esta siempre encendido.
        if (shopUI != null) shopUI.ShowOnMonitor();
    }

    // Busca el monitor por nombre y le fabrica un anchor sobre el cristal a
    // partir de la caja del modelo, para no depender de colocarlo a mano.
    void ResolveMonitor()
    {
        if (monitorObject == null && !string.IsNullOrEmpty(monitorObjectName))
        {
            GameObject found = GameObject.Find(monitorObjectName);
            if (found != null) monitorObject = found.transform;
        }

        // Con anchor manual seguimos necesitando calcular el ancho: si salimos
        // antes, el canvas se queda a escala cero y no se ve nada.
        bool manualAnchor = screenAnchor != null;

        if (monitorObject == null)
        {
            if (!manualAnchor)
            {
                Debug.LogError("[MonitorTerminal] No encuentro ningun objeto llamado \"" + monitorObjectName +
                               "\" en la escena. Corrige el nombre o arrastra el monitor a monitorObject.", this);
                screenAnchor = transform;
            }

            worldScreenWidth = screenWidthMeters > 0f ? screenWidthMeters : fallbackScreenWidth;
            return;
        }

        List<Bounds> pieces = CollectLocalBounds(monitorObject);

        if (pieces.Count == 0)
        {
            if (!manualAnchor)
            {
                Debug.LogWarning("[MonitorTerminal] \"" + monitorObject.name + "\" no tiene mallas; " +
                                 "uso su pivote como pantalla.", this);
                screenAnchor = monitorObject;
            }

            worldScreenWidth = screenWidthMeters > 0f ? screenWidthMeters : fallbackScreenWidth;
            return;
        }

        Bounds total = pieces[0];
        for (int i = 1; i < pieces.Count; i++) total.Encapsulate(pieces[i]);

        Vector3 size = total.size;

        // El lado mas fino del modelo es la profundidad: la pantalla mira por ahi.
        int depth = 0;
        if (size.y < size.x && size.y < size.z) depth = 1;
        else if (size.z < size.x && size.z < size.y) depth = 2;

        // La caja entera incluye el pie, que tira el centro hacia abajo. La
        // pantalla es la pieza mas grande vista de frente.
        Bounds panel = total;
        float best = -1f;

        foreach (Bounds piece in pieces)
        {
            float area = PlaneArea(piece.size, depth);

            if (area > best)
            {
                best = area;
                panel = piece;
            }
        }

        Vector3 localNormal = Vector3.zero;
        localNormal[depth] = 1f;

        Vector3 localUp = depth == 1 ? Vector3.forward : Vector3.up;

        Vector3 modelScale = monitorObject.lossyScale;

        int widthIndex = depth == 0 ? 2 : 0;
        int heightIndex = depth == 1 ? 2 : 1;

        float modelWidth = panel.size[widthIndex] * Mathf.Abs(modelScale[widthIndex]);
        float modelHeight = panel.size[heightIndex] * Mathf.Abs(modelScale[heightIndex]);

        worldScreenWidth = screenWidthMeters > 0f ? screenWidthMeters : modelWidth * screenInset;
        worldScreenHeight = screenHeightMeters > 0f ? screenHeightMeters : modelHeight * screenInset;

        if (worldScreenWidth <= 0.0001f) worldScreenWidth = fallbackScreenWidth;
        if (worldScreenHeight <= 0.0001f) worldScreenHeight = worldScreenWidth * 0.75f;

        // Lo de abajo solo sirve para fabricar el anchor automatico.
        if (manualAnchor) return;

        Vector3 worldCenter = monitorObject.TransformPoint(panel.center);

        // Con TransformPoint y no TransformDirection: si el modelo trae escala
        // negativa, la rotacion sola miente y la interfaz sale en espejo.
        Vector3 worldNormal = (monitorObject.TransformPoint(panel.center + localNormal) - worldCenter).normalized;
        Vector3 worldUp = (monitorObject.TransformPoint(panel.center + localUp) - worldCenter).normalized;

        if (!ScreenFacesForward(worldCenter, worldNormal))
        {
            worldNormal = -worldNormal;
        }

        if (Vector3.Cross(worldNormal, worldUp).sqrMagnitude < 0.0001f) worldUp = Vector3.up;

        float halfDepth = panel.size[depth] * 0.5f * Mathf.Abs(modelScale[depth]) + screenDepthOffset;

        // Sin padre a proposito: si el modelo trae escala negativa, colgar el
        // canvas de el lo dibujaria en espejo.
        GameObject anchor = new GameObject("ScreenAnchor");
        anchor.transform.position = worldCenter + worldNormal * halfDepth;
        anchor.transform.rotation = Quaternion.LookRotation(worldNormal, worldUp);
        anchor.transform.localScale = Vector3.one;

        screenAnchor = anchor.transform;
    }

    static float PlaneArea(Vector3 size, int depth)
    {
        if (depth == 0) return size.y * size.z;
        if (depth == 1) return size.x * size.z;

        return size.x * size.y;
    }

    // De las dos caras posibles, la pantalla es la que tiene sitio libre: un
    // monitor se pone contra la pared, no mirandola.
    bool ScreenFacesForward(Vector3 center, Vector3 candidate)
    {
        float ahead = FreeSpace(center, candidate);
        float behind = FreeSpace(center, -candidate);

        if (!Mathf.Approximately(ahead, behind)) return ahead > behind;

        // Empate (sin paredes cerca): tiramos de donde esta el jugador.
        Vector3 reference = playerCamera != null ? playerCamera.transform.position : center + Vector3.forward;

        return Vector3.Dot(candidate, reference - center) >= 0f;
    }

    float FreeSpace(Vector3 origin, Vector3 direction)
    {
        float maxDistance = 2f;
        float nearest = maxDistance;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            // Ignoramos el propio monitor y lo que sea que lo sostenga debajo.
            if (hit.transform.IsChildOf(monitorObject)) continue;

            if (hit.distance < nearest) nearest = hit.distance;
        }

        return nearest;
    }

    // Una caja por malla, en el espacio del propio monitor. Con la caja del
    // mundo el eje fino sale mal en cuanto el modelo esta girado.
    List<Bounds> CollectLocalBounds(Transform target)
    {
        List<Bounds> result = new List<Bounds>();

        MeshFilter[] filters = target.GetComponentsInChildren<MeshFilter>(true);

        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null) continue;

            Matrix4x4 toLocal = target.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds meshBounds = filter.sharedMesh.bounds;

            Bounds piece = new Bounds();
            bool started = false;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents, Corner(i));
                Vector3 point = toLocal.MultiplyPoint3x4(corner);

                if (!started)
                {
                    piece = new Bounds(point, Vector3.zero);
                    started = true;
                }
                else
                {
                    piece.Encapsulate(point);
                }
            }

            if (started) result.Add(piece);
        }

        return result;
    }

    static Vector3 Corner(int i)
    {
        return new Vector3(
            (i & 1) == 0 ? -1f : 1f,
            (i & 2) == 0 ? -1f : 1f,
            (i & 4) == 0 ? -1f : 1f);
    }

    // Si falta algo preferimos decirlo a quedarnos callados sin hacer nada.
    void ReportMissingReferences()
    {
        if (playerCamera == null)
        {
            Debug.LogError("[MonitorTerminal] No encuentro la camara del jugador. " +
                           "Comprueba que tiene el tag MainCamera, o asignala a mano.", this);
        }

        if (playerController == null)
        {
            Debug.LogError("[MonitorTerminal] No encuentro el FirstPersonController en la escena.", this);
        }

        if (shopUI == null)
        {
            Debug.LogError("[MonitorTerminal] No encuentro el ShopUI en la escena.", this);
        }
        else if (worldCanvas == null)
        {
            Debug.LogError("[MonitorTerminal] No encuentro el canvas MonitorScreen. " +
                           "Asigna su RectTransform en worldCanvas.", this);
        }
    }

    void AttachCanvasToScreen()
    {
        if (worldCanvas == null || screenAnchor == null) return;

        Canvas canvas = worldCanvas.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = playerCamera;
        }

        if (worldCanvas.GetComponent<RectMask2D>() == null)
        {
            worldCanvas.gameObject.AddComponent<RectMask2D>();
        }

        // Colocado a mano: respetamos tal cual lo que hay en la escena.
        if (!autoPlaceScreen) return;

        // Sin emparentar: el monitor lleva un LODGroup, y colgar el canvas de el
        // lo apagaria junto con el LOD en cuanto te alejas. Como el monitor no se
        // mueve, copiar su transform una vez es suficiente.
        worldCanvas.SetParent(null, false);

        Quaternion facing = flipScreenFacing ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        worldCanvas.rotation = screenAnchor.rotation * facing;
        worldCanvas.position = screenAnchor.TransformPoint(anchorOffset);

        float targetWidth = worldScreenWidth;
        float targetHeight = worldScreenHeight;

        if (targetWidth <= 0.0001f)
        {
            targetWidth = fallbackScreenWidth;

            Debug.LogWarning("[MonitorTerminal] No he podido deducir el ancho de la pantalla; " +
                             "uso " + fallbackScreenWidth + " m. Pon screenWidthMeters a mano.", this);
        }

        if (targetHeight <= 0.0001f) targetHeight = targetWidth * 0.75f;

        // En vez de encajar un canvas 4:3 y dejar bandas negras, le damos al
        // canvas la proporcion de la pantalla real. Asi la ocupa entera y la
        // escala sigue siendo uniforme, o sea que nada se deforma.
        float referenceWidth = 800f;
        float referenceHeight = referenceWidth * (targetHeight / targetWidth);

        worldCanvas.sizeDelta = new Vector2(referenceWidth, referenceHeight);

        float scale = targetWidth / referenceWidth;

        worldCanvas.localScale = new Vector3(scale, scale, scale);
    }

    // Referencia de la pantalla: el anchor si colocamos nosotros, o el propio
    // canvas si lo has puesto tu a mano.
    Transform ScreenTransform
    {
        get
        {
            if (autoPlaceScreen && screenAnchor != null) return screenAnchor;

            return worldCanvas != null ? worldCanvas.transform : screenAnchor;
        }
    }

    void BuildViewPoint()
    {
        Transform screen = ScreenTransform;

        if (viewPoint != null || screen == null) return;

        // En mundo, no en local: el monitor puede venir escalado del modelo.
        GameObject point = new GameObject("MonitorViewPoint");
        point.transform.position = screen.position + screen.forward * viewDistance;
        point.transform.rotation = Quaternion.LookRotation(-screen.forward, Vector3.up);

        viewPoint = point.transform;
    }

    void Update()
    {
        if (InUse)
        {
            FocusCamera();

            if (Input.GetKeyDown(exitKey)) Exit();
            return;
        }

        if (returning)
        {
            ReturnCamera();
            return;
        }

        HandlePrompt();
    }

    // Lo que de verdad se ve es el canvas, asi que apuntamos contra el.
    Transform AimTarget
    {
        get { return worldCanvas != null ? worldCanvas.transform : ScreenTransform; }
    }

    Vector2 ScreenHalfSize
    {
        get
        {
            if (worldCanvas != null)
            {
                return new Vector2(worldCanvas.rect.width * 0.5f, worldCanvas.rect.height * 0.5f);
            }

            float width = worldScreenWidth > 0f ? worldScreenWidth : fallbackScreenWidth;
            float height = worldScreenHeight > 0f ? worldScreenHeight : width * 0.75f;

            return new Vector2(width * 0.5f, height * 0.5f);
        }
    }

    // Cerca y apuntando dentro del rectangulo de la pantalla. Cruzamos el rayo
    // de la camara con su plano, asi que no hace falta collider en el modelo.
    bool PlayerIsAiming()
    {
        Transform screen = AimTarget;

        if (playerCamera == null || screen == null) return false;

        Transform cam = playerCamera.transform;

        if (Vector3.Distance(cam.position, screen.position) > interactDistance) return false;

        Plane plane = new Plane(screen.forward, screen.position);
        Ray ray = new Ray(cam.position, cam.forward);

        float distance;
        if (!plane.Raycast(ray, out distance)) return false;
        if (distance > interactDistance) return false;

        Vector3 local = screen.InverseTransformPoint(ray.GetPoint(distance));
        Vector2 half = ScreenHalfSize;

        return Mathf.Abs(local.x) <= half.x * aimMargin
            && Mathf.Abs(local.y) <= half.y * aimMargin;
    }

    void HandlePrompt()
    {
        bool aiming = PlayerIsAiming();
        bool canUse = aiming && !PlayerCarry.HandsFull;

        if (aiming && !promptShown)
        {
            promptShown = true;
            ShowPrompt(canUse ? "Clic izquierdo para usar el ordenador" : PlayerCarry.BusyMessage);
        }
        else if (!aiming && promptShown)
        {
            promptShown = false;
            HidePrompt();
        }

        if (canUse && Input.GetMouseButtonDown(useMouseButton)) Enter();
    }

    void ShowPrompt(string message)
    {
        if (InteractionUI.Instance != null) InteractionUI.Instance.ShowPrompt(message);
    }

    void HidePrompt()
    {
        if (InteractionUI.Instance != null) InteractionUI.Instance.HidePrompt();
    }

    void Enter()
    {
        if (playerCamera == null) return;

        savedCamLocalPosition = playerCamera.transform.localPosition;
        savedCamLocalRotation = playerCamera.transform.localRotation;

        if (playerController != null) playerController.enabled = false;

        if (promptShown)
        {
            promptShown = false;
            HidePrompt();
        }

        CursorMode.Free(this);

        InUse = true;

        if (shopUI != null) shopUI.Open();
    }

    void Exit()
    {
        InUse = false;
        returning = true;

        // Tope duro: si la interpolacion no converge, el jugador no se puede
        // quedar sin control de la camara para siempre.
        returnDeadline = Time.unscaledTime + 1.5f;

        CursorMode.Release(this);

        // La pantalla no se apaga: se queda como la dejaste.
        if (shopUI != null) shopUI.Close();
    }

    void FocusCamera()
    {
        if (playerCamera == null || viewPoint == null) return;

        float t = 1f - Mathf.Exp(-focusSpeed * Time.unscaledDeltaTime);

        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, viewPoint.position, t);
        playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, viewPoint.rotation, t);
    }

    void ReturnCamera()
    {
        if (playerCamera == null)
        {
            returning = false;
            return;
        }

        float t = 1f - Mathf.Exp(-focusSpeed * Time.unscaledDeltaTime);
        Transform cam = playerCamera.transform;

        cam.localPosition = Vector3.Lerp(cam.localPosition, savedCamLocalPosition, t);
        cam.localRotation = Quaternion.Slerp(cam.localRotation, savedCamLocalRotation, t);

        bool close = Vector3.Distance(cam.localPosition, savedCamLocalPosition) < 0.01f
                  && Quaternion.Angle(cam.localRotation, savedCamLocalRotation) < 1f;

        if (!close && Time.unscaledTime < returnDeadline) return;

        cam.localPosition = savedCamLocalPosition;
        cam.localRotation = savedCamLocalRotation;

        if (playerController != null) playerController.enabled = true;

        returning = false;
    }

    void OnDisable()
    {
        InUse = false;

        // Si el terminal se apaga con la pantalla abierta, el raton no se puede
        // quedar suelto: nadie mas lo devolveria.
        CursorMode.Release(this);
    }
}
