using UnityEngine;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance;

    [Tooltip("Ajuste a rejilla en el suelo, en metros. 0 = libre.")]
    public float gridSize = 0f;
    public float rotationStep = 15f;
    public Material validMaterial;
    public Material invalidMaterial;
    public LayerMask obstacleLayers;
    public LayerMask groundLayer;
    [Tooltip("Donde se puede apoyar: suelo, mostradores, mesas...")]
    public LayerMask surfaceLayers = ~0;
    [Tooltip("Cuanto de horizontal tiene que ser la superficie. 1 = plana.")]
    [Range(0f, 1f)] public float minSurfaceUp = 0.7f;
    public Camera playerCamera;
    public float placeDistance = 8f;
    [Tooltip("Distancia minima a la que se puede dejar algo, para no plantarselo encima al jugador.")]
    public float minPlaceDistance = 1.7f;

    private GameObject ghostObject;
    private Renderer[] ghostRenderers;
    private int placementStartedFrame = -1;
    private GameObject finalPrefab;
    // Donde puede pegarse lo que se esta colocando.
    //
    // Viene con el objeto y no de aqui: una bombilla va en el techo la ponga
    // quien la ponga. Sin regla, se comporta como siempre -- apoyado en algo
    // que mire hacia arriba -- asi que nada de lo que ya existia cambia.
    private ReglaDeColocacion regla;
    private Vector3 surfaceNormal = Vector3.up;

    private bool isPlacing = false;
    private bool canPlace = false;
    private float currentYRotation = 0f;

    public bool IsPlacing { get { return isPlacing; } }

    private Vector3 ghostPivotToBottomLocal;
    private Vector3 ghostPivotToCenterLocal;
    private Vector3 ghostBoundsSize;

    private class CarriedItem
    {
        public GameObject item;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    private readonly List<CarriedItem> carriedItems = new List<CarriedItem>();

    // Los juguetes que hubiera dentro de la maquina viajan con ella: los guardamos
    // en local respecto al objeto que se recoge y los devolvemos al colocarlo.
    public void CarryWithNextPlacement(List<GameObject> items, Transform reference)
    {
        DropCarriedItems();

        if (items == null || reference == null) return;

        foreach (GameObject item in items)
        {
            if (item == null) continue;

            CarriedItem entry = new CarriedItem();
            entry.item = item;
            entry.localPosition = reference.InverseTransformPoint(item.transform.position);
            entry.localRotation = Quaternion.Inverse(reference.rotation) * item.transform.rotation;

            FreezeItem(item, true);
            item.SetActive(false);

            carriedItems.Add(entry);
        }
    }

    void FreezeItem(GameObject item, bool frozen)
    {
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null) return;

        if (frozen)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = frozen;
    }

    void RestoreCarriedItems(Transform reference)
    {
        if (reference == null)
        {
            DropCarriedItems();
            return;
        }

        foreach (CarriedItem entry in carriedItems)
        {
            if (entry.item == null) continue;

            entry.item.transform.SetPositionAndRotation(
                reference.TransformPoint(entry.localPosition),
                reference.rotation * entry.localRotation);

            entry.item.SetActive(true);
            FreezeItem(entry.item, false);
        }

        carriedItems.Clear();
    }

    // Si se cancela la colocacion los juguetes se quedan donde estuviera el fantasma.
    void DropCarriedItems()
    {
        foreach (CarriedItem entry in carriedItems)
        {
            if (entry.item == null) continue;

            entry.item.SetActive(true);
            FreezeItem(entry.item, false);
        }

        carriedItems.Clear();
    }

    void Awake()
    {
        Instance = this;
    }

    private System.Action onConfirmed;
    private System.Action onCancelled;
    private System.Action<Vector3, Quaternion> onMoved;
    private GameObject movingObject;
    private Transform playerRoot;

    public void StartPlacement(GameObject prefabToPlace)
    {
        StartPlacement(prefabToPlace, null, null);
    }

    // Colocar algo que ya existe. El fantasma se clona del propio objeto, pero
    // al confirmar no se crea nada: se avisa de donde ha quedado para moverlo.
    public void StartMoving(GameObject existingObject, System.Action<Vector3, Quaternion> placedAt, System.Action cancelled)
    {
        onMoved = placedAt;
        movingObject = existingObject;

        StartPlacement(existingObject, null, cancelled);
    }

    // Con avisos: quien la lanza necesita saber si acabo colocada o cancelada.
    public void StartPlacement(GameObject prefabToPlace, System.Action confirmed, System.Action cancelled)
    {
        onConfirmed = confirmed;
        onCancelled = cancelled;

        if (confirmed != null) onMoved = null;

        if (prefabToPlace == null)
        {
            Debug.LogWarning("StartPlacement recibio un prefab nulo, cancelando.");
            return;
        }

        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }

        isPlacing = false;

        finalPrefab = prefabToPlace;
        regla = prefabToPlace.GetComponent<ReglaDeColocacion>();

        ghostObject = Instantiate(prefabToPlace);

        MonoBehaviour[] scripts = ghostObject.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            script.enabled = false;
        }

        Collider[] colliders = ghostObject.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = ghostObject.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        ghostRenderers = ghostObject.GetComponentsInChildren<Renderer>();

        // Base limpia antes de medir. Si viene inclinado (una caja en brazos lo
        // esta) los offsets salen en esa base torcida y luego, al rotar con la
        // rueda, el objeto orbita en vez de girar sobre si mismo.
        float startYaw = ghostObject.transform.eulerAngles.y;
        ghostObject.transform.rotation = Quaternion.Euler(0f, startYaw, 0f);

        CalculatePivotOffsets(ghostObject);
        currentYRotation = startYaw;
        canPlace = false;
        placementStartedFrame = Time.frameCount;

        // Que nazca ya coloreado, sin esperar al primer frame de LateUpdate.
        ApplyGhostMaterial(false);

        isPlacing = true;
    }

    void CalculatePivotOffsets(GameObject obj)
    {
        Renderer[] allRenderers = obj.GetComponentsInChildren<Renderer>();
        List<Renderer> filteredList = new List<Renderer>();
        foreach (Renderer r in allRenderers)
        {
            if (!(r is LineRenderer))
            {
                filteredList.Add(r);
            }
        }
        Renderer[] renderers = filteredList.ToArray();

        if (renderers.Length == 0)
        {
            ghostPivotToBottomLocal = Vector3.zero;
            ghostPivotToCenterLocal = Vector3.zero;
            ghostBoundsSize = Vector3.one;
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;
        foreach (Renderer rend in renderers)
        {
            combinedBounds.Encapsulate(rend.bounds);
        }

        Vector3 centerWorld = combinedBounds.center;
        Vector3 bottomWorld = new Vector3(combinedBounds.center.x, combinedBounds.min.y, combinedBounds.center.z);

        Quaternion invRot = Quaternion.Inverse(obj.transform.rotation);

        ghostPivotToCenterLocal = invRot * (centerWorld - obj.transform.position);
        ghostPivotToBottomLocal = invRot * (bottomWorld - obj.transform.position);
        ghostBoundsSize = combinedBounds.size;
    }

    void LateUpdate()
    {
        if (!isPlacing) return;

        if (ghostObject == null)
        {
            isPlacing = false;
            return;
        }

        HandleRotationInput();
        UpdateGhostPosition();

        // El clic que abre el modo colocar llega a este LateUpdate todavia como
        // "pulsado este frame", y confirmaba al instante sin dejarte ver nada.
        if (Time.frameCount == placementStartedFrame) return;

        if (Input.GetMouseButtonDown(0) && canPlace)
        {
            ConfirmPlacement();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    void HandleRotationInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll > 0f)
        {
            currentYRotation += rotationStep;
        }
        else if (scroll < 0f)
        {
            currentYRotation -= rotationStep;
        }
    }

    void UpdateGhostPosition()
    {
        if (ghostObject == null) return;

        Ray cameraRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        Vector3 surfacePoint = Vector3.zero;
        bool foundSurface = false;
        bool onGround = false;

        // Lo que estas mirando, si es una superficie donde se pueda apoyar algo.
        // Asi se puede dejar el ordenador encima del mostrador, no solo en el suelo.
        RaycastHit cameraHit;
        if (Physics.Raycast(cameraRay, out cameraHit, placeDistance, surfaceLayers, QueryTriggerInteraction.Ignore)
            && SuperficieVale(cameraHit.normal))
        {
            surfacePoint = cameraHit.point;
            surfaceNormal = cameraHit.normal;
            foundSurface = true;
            onGround = IsInLayerMask(cameraHit.collider.gameObject.layer, groundLayer);
        }

        // Buscar suelo debajo solo tiene sentido para lo que va en el suelo.
        // Con una bombilla, este respaldo la bajaria al suelo cada vez que
        // apuntases a un sitio que no es techo.
        if (!foundSurface && EsDeSuelo)
        {
            Vector3 aimPoint = cameraRay.origin + cameraRay.direction * placeDistance;
            Vector3 groundCheckStart = new Vector3(aimPoint.x, aimPoint.y + 10f, aimPoint.z);

            RaycastHit groundHit;
            if (Physics.Raycast(groundCheckStart, Vector3.down, out groundHit, 50f, groundLayer))
            {
                surfacePoint = groundHit.point;
                surfaceNormal = groundHit.normal;
                foundSurface = true;
                onGround = true;
            }
        }

        if (!foundSurface)
        {
            // Sin sitio donde apoyarlo: lo dejamos flotando delante y en rojo,
            // en vez de dejarlo donde estuviera y sin colorear.
            canPlace = false;

            Transform cam = playerCamera.transform;
            Quaternion floatRotation = Quaternion.Euler(0f, currentYRotation, 0f);

            ghostObject.transform.rotation = floatRotation;
            ghostObject.transform.position = cam.position + cam.forward * (placeDistance * 0.4f)
                                           - floatRotation * ghostPivotToCenterLocal;

            ApplyGhostMaterial(false);
            return;
        }

        // Apartarlo del jugador solo aplica a lo que se deja en el suelo. Una
        // bombilla en el techo no le estorba a nadie a los pies.
        if (EsDeSuelo) surfacePoint = PushAwayFromPlayer(surfacePoint);

        // Rejilla solo en el suelo, y solo si esta activada: con gridSize a 0 la
        // colocacion es libre y sigue al raton sin dar saltos.
        bool snap = onGround && gridSize > 0.001f;

        float x = snap ? Mathf.Round(surfacePoint.x / gridSize) * gridSize : surfacePoint.x;
        float z = snap ? Mathf.Round(surfacePoint.z / gridSize) * gridSize : surfacePoint.z;

        if (EsDeSuelo)
        {
            Quaternion desiredRotation = Quaternion.Euler(0f, currentYRotation, 0f);
            Vector3 desiredBottomWorld = new Vector3(x, surfacePoint.y, z);
            Vector3 rotatedBottomOffset = desiredRotation * ghostPivotToBottomLocal;

            ghostObject.transform.rotation = desiredRotation;
            ghostObject.transform.position = desiredBottomWorld - rotatedBottomOffset;
        }
        else
        {
            // En techo y pared el objeto se pega por su origen, no por su
            // base: la bombilla esta modelada colgando desde la roseta, y el
            // interruptor con la placa en su origen. Asi el punto que toca la
            // superficie es el pivote, y no hay que calcular nada.
            ghostObject.transform.rotation = regla.Orientacion(surfaceNormal,
                                                              currentYRotation);
            ghostObject.transform.position = surfacePoint
                                             + surfaceNormal * regla.separacion;
        }

        CheckPlacementValidity();
    }

    bool EsDeSuelo
    {
        get
        {
            return regla == null || regla.donde == ReglaDeColocacion.Donde.Suelo;
        }
    }

    bool SuperficieVale(Vector3 normal)
    {
        if (regla == null) return normal.y >= minSurfaceUp;

        return regla.Vale(normal, minSurfaceUp);
    }

    // Nunca a los pies del jugador: al reactivarse el collider lo despenetraria
    // y saldria disparado hacia arriba.
    Vector3 PushAwayFromPlayer(Vector3 point)
    {
        if (PlayerRoot == null || minPlaceDistance <= 0f) return point;

        Vector3 from = PlayerRoot.position;

        Vector3 flat = point - from;
        flat.y = 0f;

        if (flat.magnitude >= minPlaceDistance) return point;

        Vector3 direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : FlatForward();
        Vector3 pushed = from + direction * minPlaceDistance;

        RaycastHit hit;
        if (Physics.Raycast(pushed + Vector3.up * 3f, Vector3.down, out hit, 12f, surfaceLayers, QueryTriggerInteraction.Ignore)
            && hit.normal.y >= minSurfaceUp)
        {
            return hit.point;
        }

        return new Vector3(pushed.x, point.y, pushed.z);
    }

    Vector3 FlatForward()
    {
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : Vector3.forward;
        forward.y = 0f;

        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    void CheckPlacementValidity()
    {
        if (ghostObject == null) return;

        Vector3 rotatedCenterOffset = ghostObject.transform.rotation * ghostPivotToCenterLocal;
        Vector3 centerWorldNow = ghostObject.transform.position + rotatedCenterOffset;

        Vector3 checkSize = ghostBoundsSize * 0.9f;

        Collider[] overlaps = Physics.OverlapBox(centerWorldNow, checkSize / 2f, ghostObject.transform.rotation, obstacleLayers, QueryTriggerInteraction.Ignore);

        canPlace = overlaps.Length == 0;

        // Tampoco encima del jugador: al activarse el collider lo despenetraria
        // y saldria disparado hacia arriba.
        if (canPlace && OverlapsPlayer(centerWorldNow, checkSize)) canPlace = false;

        ApplyGhostMaterial(canPlace);
    }

    // Los renderers se cachean al crear el fantasma: buscarlos cada frame y
    // asignar .material creaba una copia nueva del material en cada frame.
    void ApplyGhostMaterial(bool valid)
    {
        Material mat = valid ? validMaterial : invalidMaterial;

        if (mat == null || ghostRenderers == null) return;

        foreach (Renderer rend in ghostRenderers)
        {
            if (rend == null || rend is LineRenderer) continue;
            if (rend.sharedMaterial == mat) continue;

            // Un renderer puede tener varios huecos de material y hay que
            // pintarlos todos, no solo el primero.
            Material[] slots = new Material[rend.sharedMaterials.Length];

            for (int i = 0; i < slots.Length; i++) slots[i] = mat;

            rend.sharedMaterials = slots;
        }
    }

    Transform PlayerRoot
    {
        get
        {
            if (playerRoot == null)
            {
                FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null) playerRoot = controller.transform;
            }

            return playerRoot;
        }
    }

    bool OverlapsPlayer(Vector3 center, Vector3 size)
    {
        if (PlayerRoot == null) return false;

        Collider[] hits = Physics.OverlapBox(center, size / 2f, ghostObject.transform.rotation, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            // Contra todos los colliders del jugador: tiene CharacterController
            // y ademas una capsula, y antes solo se comparaba con uno de ellos.
            if (hit != null && hit.transform.IsChildOf(PlayerRoot)) return true;
        }

        return false;
    }

    void ConfirmPlacement()
    {
        if (ghostObject == null || finalPrefab == null) return;

        Vector3 finalPosition = ghostObject.transform.position;
        Quaternion finalRotation = ghostObject.transform.rotation;
        GameObject prefabToSpawn = finalPrefab;

        Destroy(ghostObject);
        ghostObject = null;
        isPlacing = false;
        ghostRenderers = null;

        // Modo mover: el objeto ya existe, solo hay que decir donde ha quedado.
        if (onMoved != null)
        {
            System.Action<Vector3, Quaternion> moved = onMoved;

            GameObject moving = movingObject;

            onMoved = null;
            onConfirmed = null;
            onCancelled = null;
            movingObject = null;

            DropCarriedItems();
            moved.Invoke(finalPosition, finalRotation);

            // Recien puesto puede quedar rozando al jugador: sin esto el motor
            // los separa de golpe y le manda por los aires.
            PlacedCollisionGrace.Apply(moving, PlayerRoot);
            return;
        }

        GameObject newInstance = Instantiate(prefabToSpawn, finalPosition, finalRotation);

        PlaceableObject placeable = newInstance.GetComponentInChildren<PlaceableObject>();
        if (placeable != null)
        {
            placeable.sourcePrefab = prefabToSpawn;
        }

        RestoreCarriedItems(newInstance.transform);

        // Recien puesto puede quedar rozando al jugador: sin esto el motor los
        // separa de golpe y le manda por los aires.
        PlacedCollisionGrace.Apply(newInstance, PlayerRoot);

        NotifyFinished(true);
    }

    void NotifyFinished(bool placed)
    {
        System.Action callback = placed ? onConfirmed : onCancelled;

        onConfirmed = null;
        onCancelled = null;
        onMoved = null;
        movingObject = null;

        if (callback != null) callback.Invoke();
    }

    public void CancelPlacement()
    {
        if (!isPlacing && ghostObject == null) return;

        if (ghostObject != null)
        {
            RestoreCarriedItems(ghostObject.transform);
            Destroy(ghostObject);
        }
        else
        {
            DropCarriedItems();
        }

        ghostObject = null;
        isPlacing = false;

        NotifyFinished(false);
    }
}