using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class ClawController : MonoBehaviour
{
    public Transform railX;
    public Transform railZ;
    public Transform clawArm;
    public Transform clawHead;
    public Transform[] fingers;
    public Transform[] fingerTips;
    public Transform hingePoint;
    public Transform prizeZone;
    public Transform swingAnchor;

    [Header("Movimiento de los railes")]
    public float moveSpeed = 1.5f;
    public float acceleration = 4f;
    public float deceleration = 6f;

    public bool invertX = false;
    public bool invertZ = false;

    public float limitXMin = -0.8f;
    public float limitXMax = 0.8f;
    public float limitZMin = -0.8f;
    public float limitZMax = 0.8f;

    [Header("Movimiento vertical del brazo")]
    public float armDownY = -1.2f;
    public float armUpY = 0f;
    public float armMoveSpeed = 1f;
    public float armAcceleration = 3f;
    public float armDeceleration = 5f;

    [Header("Deteccion del juguete (zona bajo la garra)")]
    public float detectionRadius = 0.3f;
    public float detectionHeightOffset = 0.2f;
    public float gripHeightOffset = 0.02f;
    public float minDescentBeforeStop = 0.05f;

    [Header("Cierre de las pinzas")]
    public float fingerCloseAngle = 40f;
    public float fingerSpeed = 20f;
    public float minAngleBeforeContactCheck = 5f;
    public float fingerContactDistance = 0.02f;
    [Range(0f, 1f)] public float fingerContactSampleStart = 0.5f;

    [Header("Cierre solapado con la subida (sistema realista)")]
    public float closeBeforeLiftDelay = 0.5f;
    public float postLiftCloseGrace = 0.8f;
    public float liveGripMaxDuration = 6f;

    [Header("Validacion del agarre")]
    public int requiredContactCount = 1;
    public float minClosingAngleForValidGrip = 5f;
    public float maxGrabMass = 10f;
    public float grabVerificationDelay = 0.2f;

    [Header("El peluche debe estar dentro del hueco entre las pinzas")]
    public float insideGripRadius = 0.22f;
    public float insideGripHeightOffset = 0.1f;

    public LayerMask plushLayer;

    [Header("Bloqueo de obstaculos/barreras (el brazo se mueve por script, no por fisica)")]
    public LayerMask obstacleLayerMask;
    public float obstacleCheckRadius = 0.12f;

    [Header("Fuerza de la maquina (sistema antiguo)")]
    public float baseGripStrength = 180f;
    public float gripRandomRange = 40f;
    [HideInInspector] public float currentGripStrength;

    [Header("Dedos con motor (rozamiento puro)")]
    [Tooltip("Los dedos dejan de moverse escribiendo su rotacion y pasan a ser "
             + "cuerpos con bisagra y motor. Un transform no empuja: se mete "
             + "dentro del peluche y PhysX lo expulsa, que es por lo que la "
             + "garra no agarraba nada. Apagarlo vuelve al sistema viejo.")]
    public bool useMotorFingers = true;

    [Tooltip("Cuanto se hunde la garra en el peluche al bajar, como parte de su "
             + "altura. 0,5 deja las puntas a media altura, que es la parte mas "
             + "ancha y de donde mejor se coge. Subirlo hunde mas la garra.")]
    [Range(0.15f, 1f)] public float grabDepth = 0.6f;

    [HideInInspector] public ClawFingerMotors fingerMotors;

    [Header("Fisica de agarre realista")]
    public bool useRealisticGripPhysics = true;
    public float gripForcePlayerMin = 1f;
    public float gripForcePlayerMax = 10f;
    public float gripForceNPCMin = 1f;
    public float gripForceNPCMax = 3f;
    public float forceNewtonsPerUnit = 22f;
    public float breakForceMultiplier = 1.3f;
    [HideInInspector] public float currentGripForceRating;

    [Header("Resbalon (se cae el peluche agarrado)")]
    public float slipExtraCloseAngle = 15f;
    public float slipCloseSpeed = 40f;

    [Header("Reparto de peluches al llenar")]
    [Tooltip("Que parte del area de juego se usa. 1 = de pared a pared.")]
    [Range(0.2f, 1f)] public float toyScatterSpread = 0.55f;
    [Tooltip("Desde que altura caen, por encima del punto de suelta.")]
    public float toyDropHeight = 0.2f;
    [Tooltip("Radio alrededor de la boca del premio donde no se sueltan.")]
    public float prizeZoneClearance = 0.3f;
    [Tooltip("Margen minimo con las paredes de la maquina, en metros.")]
    public float toyBoundsMargin = 0.12f;

    [Header("Viaje a zona de premio")]
    public float prizeTravelSpeed = 0.6f;
    [Tooltip("Margen para que el premio caiga por la rampa antes de retirarlo.")]
    public float prizeDeliverDelay = 1.5f;
    public int fallbackPrizeReward = 20;

    [Header("Cable fisico")]
    // Apagado a proposito. El cable fisico necesita que la garra sea antes un
    // conjunto articulado: su pivote esta a metros de la garra visible y varios
    // dedos no tienen collider, asi que su masa esta mal repartida y no hay
    // muelle que lo arregle. Se enciende en el paso 5, con los dedos ya hechos.
    [Tooltip("Cuelga la cabeza de una articulacion real. Requiere dedos articulados (paso 5).")]
    public bool usePhysicalCable = false;
    [Tooltip("Masa de la cabeza en kg. Debe ser del mismo orden que el peluche.")]
    public float headMass = 1.5f;
    [Tooltip("Cable mas corto posible, con la garra recogida del todo.")]
    public float minCableLength = 0.05f;
    [Tooltip("Rigidez con la que vuelve a la vertical. Mas alto = menos se inclina.")]
    public float uprightSpring = 18f;
    [Tooltip("Cuanto se le va el balanceo. Mas alto = se para antes.")]
    public float uprightDamper = 9f;
    [Tooltip("Frena el pendulo. 0 = se balancea eternamente.")]
    public float headLinearDamping = 1.4f;
    [Tooltip("A que distancia por debajo del cable cuelga el peso. Mas alto = se inclina mas despacio y mas amplio.")]
    public float comDropBelowCable = 0.18f;
    [Tooltip("Collider mas grande que se le permite a una pieza de la garra, en metros.")]
    public float maxClawColliderSize = 0.35f;

    [Header("Balanceo (Swing) - sustituido por el cable fisico")]
    public bool enableSwing = true;
    public float swingStiffness = 30f;
    public float swingDamping = 5f;
    public float swingTiltAmount = 9f;
    public float swingMaxTiltAngle = 20f;

    [HideInInspector] public bool isControllable = false;
    [HideInInspector] public Transform activeCarrySpot;
    private bool isBusy = false;
    public bool IsBusy { get { return isBusy; } }

    [Header("NPC")]
    public Transform npcSpot;
    public float npcStandDistance = 0.7f;
    public float npcApproachDistance = 2.2f;
    public float npcQueueFirstGap = 1.1f;
    public float npcQueueSpacing = 0.8f;

    public static readonly List<ClawController> AllMachines = new List<ClawController>();
    private readonly List<NPCClawPlayer> npcQueue = new List<NPCClawPlayer>();
    [HideInInspector] public NPCClawPlayer currentNPCUser;

    public int NPCQueueCount { get { return npcQueue.Count; } }

    void OnEnable()
    {
        if (!AllMachines.Contains(this)) AllMachines.Add(this);
    }

    void OnDisable()
    {
        AllMachines.Remove(this);
        npcQueue.Clear();
        currentNPCUser = null;
    }

    public void JoinQueue(NPCClawPlayer npc)
    {
        if (npc == null) return;

        npcQueue.RemoveAll(n => n == null);

        if (!npcQueue.Contains(npc)) npcQueue.Add(npc);
    }

    public void LeaveQueue(NPCClawPlayer npc)
    {
        npcQueue.Remove(npc);
        npcQueue.RemoveAll(n => n == null);

        if (currentNPCUser == npc) currentNPCUser = null;
    }

    public bool IsTurnOf(NPCClawPlayer npc)
    {
        npcQueue.RemoveAll(n => n == null);

        if (npcQueue.Count == 0 || npcQueue[0] != npc) return false;
        if (currentNPCUser != null && currentNPCUser != npc) return false;

        return !isBusy && !isControllable;
    }

    private Bounds cachedMachineBounds;
    private bool machineBoundsCached = false;

    // El pivote de este objeto no esta en el centro de la maquina, asi que para
    // saber que lado es el delantero usamos la caja real de la carcasa.
    public Bounds MachineBounds
    {
        get
        {
            if (machineBoundsCached) return cachedMachineBounds;

            Renderer[] renderers = transform.root.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool any = false;

            foreach (Renderer rend in renderers)
            {
                if (rend == null || rend is LineRenderer) continue;

                if (!any)
                {
                    bounds = rend.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(rend.bounds);
                }
            }

            cachedMachineBounds = bounds;
            machineBoundsCached = true;

            return cachedMachineBounds;
        }
    }

    public Vector3 MachineCenter { get { return MachineBounds.center; } }

    // Direccion que sale de la parte delantera de la maquina hacia fuera.
    public Vector3 NPCFrontDirection
    {
        get
        {
            if (npcSpot == null) return transform.forward;

            Vector3 away = npcSpot.position - MachineCenter;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f) away = -npcSpot.forward;

            return away.normalized;
        }
    }

    // Medio ancho de la maquina en la direccion en la que se planta el NPC.
    float FrontExtent(Vector3 front)
    {
        Vector3 extents = MachineBounds.extents;

        return Mathf.Abs(extents.x * front.x) + Mathf.Abs(extents.z * front.z);
    }

    // Donde se planta el NPC para jugar: delante del cristal, nunca dentro.
    public Vector3 GetPlayPosition()
    {
        if (npcSpot == null) return transform.position;

        Vector3 front = NPCFrontDirection;

        Vector3 fromCenter = npcSpot.position - MachineCenter;
        fromCenter.y = 0f;

        float spotDistance = Vector3.Dot(fromCenter, front);
        float minimumDistance = FrontExtent(front) + npcStandDistance;

        float distance = Mathf.Max(spotDistance + npcStandDistance, minimumDistance);

        Vector3 origin = MachineCenter;
        origin.y = npcSpot.position.y;

        return origin + front * distance;
    }

    // Punto por el que se entra: bien delante, para no rozar los laterales.
    public Vector3 GetApproachPosition()
    {
        if (npcSpot == null) return transform.position;

        return GetPlayPosition() + NPCFrontDirection * npcApproachDistance;
    }

    public Vector3 GetWaitPosition(NPCClawPlayer npc)
    {
        if (npcSpot == null) return transform.position;

        npcQueue.RemoveAll(n => n == null);

        int index = npcQueue.IndexOf(npc);
        if (index <= 0) return GetPlayPosition();

        float back = npcQueueFirstGap + (index - 1) * npcQueueSpacing;

        return GetPlayPosition() + NPCFrontDirection * back;
    }

    private FixedJoint currentJoint;
    private Rigidbody clawHeadRb;
    private Rigidbody carriageRb;
    private Rigidbody armRb;
    private float[] anguloPrevio;
    private bool cerrandoMotores = false;
    private ConfigurableJoint cableJoint;
    private Vector3 cableAnchorLocal;
    private float cableBaseLength;
    private Vector3 cableEndLocal;
    private Collider lastTouchedPlushCollider;
    private Rigidbody heldPlushRb;
    private float[] currentFingerAngle;
    private Vector3[] fingerRotationAxis;
    private bool[] fingerStopped;
    private float startPosX;
    private float startPosZ;

    private Collider[] fingerTouchedCollider;
    private Vector3[] fingerContactPoint;
    private Rigidbody[] fingerJointHostRb;
    private Vector3[] fingerRestLocalPos;
    private Quaternion[] fingerRestLocalRot;
    private Vector3[] fingerHingeLocal;
    private Vector3[] fingerAxisLocal;
    private ConfigurableJoint[] activeFingerJoints;
    private Rigidbody currentGrabTargetRb;
    private Rigidbody intendedTargetRb;

    private bool IsNPCTurn { get { return activeCarrySpot != null; } }

    private float currentVelX = 0f;
    private float currentVelZ = 0f;

    private float tiltX = 0f;
    private float tiltZ = 0f;
    private float velTiltX = 0f;
    private float velTiltZ = 0f;
    private Vector3 lastRailPosForSwing;
    private Vector3 armBaseLocalPos;

    void Start()
    {
        isControllable = false;

        // La cabeza solo necesita cuerpo propio si va a colgar de un cable
        // fisico. Si no, es una pieza mas del brazo y su collider debe formar
        // parte del de el.
        //
        // Ponerselo siempre metia un Rigidbody DENTRO de otro Rigidbody, que es
        // pedirle a PhysX que obedezca a dos amos: la jerarquia de transforms
        // mueve al hijo por un lado y el motor de fisica por otro.
        if (usePhysicalCable)
        {
            clawHeadRb = clawHead.GetComponent<Rigidbody>();
            if (clawHeadRb == null)
            {
                clawHeadRb = clawHead.gameObject.AddComponent<Rigidbody>();
            }
            clawHeadRb.isKinematic = true;
            clawHeadRb.useGravity = false;
        }

        SetUpCarriage();

        startPosX = railX.localPosition.x;
        startPosZ = railZ.localPosition.z;

        lastRailPosForSwing = new Vector3(railX.localPosition.x, 0f, railZ.localPosition.z);
        armBaseLocalPos = clawArm.localPosition;

        // Despues de guardar la posicion base: desengancha la cabeza y la cuelga.
        EnsureClawColliders();
        SetUpCable();

        currentFingerAngle = new float[fingers.Length];
        anguloPrevio = new float[fingers.Length];
        fingerRotationAxis = new Vector3[fingers.Length];
        fingerStopped = new bool[fingers.Length];
        fingerTouchedCollider = new Collider[fingers.Length];
        fingerContactPoint = new Vector3[fingers.Length];
        fingerRestLocalPos = new Vector3[fingers.Length];
        fingerRestLocalRot = new Quaternion[fingers.Length];
        fingerHingeLocal = new Vector3[fingers.Length];
        fingerAxisLocal = new Vector3[fingers.Length];
        fingerJointHostRb = new Rigidbody[fingers.Length];
        activeFingerJoints = new ConfigurableJoint[fingers.Length];

        for (int i = 0; i < fingers.Length; i++)
        {
            currentFingerAngle[i] = 0f;

            Vector3 directionFromHinge = fingers[i].position - hingePoint.position;
            directionFromHinge.y = 0f;

            if (directionFromHinge.sqrMagnitude < 0.0001f)
            {
                fingerRotationAxis[i] = Vector3.right;
            }
            else
            {
                fingerRotationAxis[i] = Vector3.Cross(directionFromHinge.normalized, Vector3.up).normalized;
            }

            // Pose de reposo del dedo, en local. La rotacion se calcula siempre
            // desde aqui, nunca acumulando: asi no puede quedarse desencajado.
            fingerRestLocalPos[i] = fingers[i].localPosition;
            fingerRestLocalRot[i] = fingers[i].localRotation;

            Transform parent = fingers[i].parent;

            if (parent != null)
            {
                fingerHingeLocal[i] = parent.InverseTransformPoint(hingePoint.position);
                fingerAxisLocal[i] = parent.InverseTransformDirection(fingerRotationAxis[i]).normalized;
            }
            else
            {
                fingerHingeLocal[i] = hingePoint.position;
                fingerAxisLocal[i] = fingerRotationAxis[i];
            }

            // Esto es del sistema viejo de uniones invisibles: cada punta
            // necesitaba un cuerpo al que colgar la union con el peluche.
            //
            // Con motores no solo sobra, sino que estropea todo. La punta es
            // HIJA del dedo, y el dedo pasa a ser un cuerpo dinamico: meterle
            // dentro un cuerpo CINEMATICO es pedirle a PhysX que obedezca a dos
            // amos a la vez. De ahi que los dedos hicieran cosas raras.
            if (useMotorFingers) continue;

            Transform jointHost = GetJointHost(i);
            Rigidbody hostRb = jointHost.GetComponent<Rigidbody>();
            if (hostRb == null)
            {
                hostRb = jointHost.gameObject.AddComponent<Rigidbody>();
            }
            hostRb.isKinematic = true;
            hostRb.useGravity = false;
            fingerJointHostRb[i] = hostRb;
        }

        // Aqui y no antes: los motores necesitan los ejes de giro, y esos se
        // acaban de calcular en el bucle de arriba.
        SetUpFingerMotors();

        AddStaticMachineColliders();
        AddNavObstacle();
    }

    // Para que los NPC rodeen la maquina aunque se haya colocado despues de
    // hornear el NavMesh: un obstaculo que recorta el mapa en caliente.
    void AddNavObstacle()
    {
        Transform root = transform.root;

        if (root.GetComponentInChildren<NavMeshObstacle>() != null) return;

        Bounds bounds = MachineBounds;
        if (bounds.size.sqrMagnitude < 0.0001f) return;

        GameObject holder = new GameObject("NavObstacle");
        holder.transform.SetParent(root, false);
        holder.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);

        Vector3 parentScale = root.lossyScale;
        holder.transform.localScale = new Vector3(
            parentScale.x != 0f ? 1f / parentScale.x : 1f,
            parentScale.y != 0f ? 1f / parentScale.y : 1f,
            parentScale.z != 0f ? 1f / parentScale.z : 1f);

        NavMeshObstacle obstacle = holder.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = bounds.size;
        obstacle.carveOnlyStationary = true;
        obstacle.carving = true;
    }

    void AddStaticMachineColliders()
    {
        Transform root = transform.root;
        MeshFilter[] allMeshes = root.GetComponentsInChildren<MeshFilter>(true);

        foreach (MeshFilter mf in allMeshes)
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            if (mf.GetComponentInParent<PlushItem>() != null) continue;
            if (railX != null && mf.transform.IsChildOf(railX)) continue;

            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.convex = false;
        }
    }

    bool ClawBlockedAt(Vector3 worldPos)
    {
        if (obstacleLayerMask.value == 0) return false;
        return Physics.CheckSphere(worldPos, obstacleCheckRadius, obstacleLayerMask, QueryTriggerInteraction.Ignore);
    }

    bool FingerBlockedByObstacle(int i)
    {
        if (obstacleLayerMask.value == 0) return false;

        Vector3 checkPos = (fingerTips != null && fingerTips.Length > i && fingerTips[i] != null) ? fingerTips[i].position : fingers[i].position;
        return Physics.CheckSphere(checkPos, obstacleCheckRadius, obstacleLayerMask, QueryTriggerInteraction.Ignore);
    }

    bool PlushIsInsideGrip(Rigidbody plushRb)
    {
        if (plushRb == null) return false;

        Vector3 zoneCenter = hingePoint.position + Vector3.down * insideGripHeightOffset;
        return Vector3.Distance(plushRb.worldCenterOfMass, zoneCenter) <= insideGripRadius;
    }

    void Update()
    {
        // GetKeyDown solo es fiable en Update: en FixedUpdate se pierden
        // pulsaciones cuando el fotograma dura mas que el paso de fisica.
        if (isControllable && !isBusy && Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(GrabSequence());
        }

    }

    // El carro empuja peluches, asi que su movimiento pertenece al paso de
    // fisica. En Update avanzaba distinto segun los FPS.
    void FixedUpdate()
    {
        if (isControllable && !isBusy) HandleMovement();

        // El brazo se coloca aqui y no en Update. Lleva colgando los dedos por
        // articulaciones, y una articulacion pertenece al paso de fisica: si el
        // ancla se mueve al ritmo del dibujo, el solver ve saltos que no
        // corresponden a ninguna velocidad y responde a lo loco.
        ApplyArmPosition();
    }

    void ApplyArmPosition()
    {
        // Con cable fisico no se toca el transform: la cabeza cuelga y la fisica
        // decide donde queda. Aqui solo se le dice cuanto cable hay soltado.
        //
        // El UpdateSwing de antes simulaba el balanceo a mano y ahora sobra: si
        // siguiera activo tendriamos dos sistemas moviendo la misma cabeza.
        if (cableJoint != null)
        {
            ApplyCableLength();
            return;
        }

        if (enableSwing)
        {
            UpdateSwing();
        }
        else
        {
            ColocarBrazo(armBaseLocalPos, Quaternion.identity);
        }
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // La pose del dedo es funcion pura de su angulo, calculada desde el reposo.
    // Antes se acumulaba con RotateAround en espacio de mundo y cualquier
    // desfase se quedaba para siempre: los dedos acababan sueltos por el aire.
    // Monta las bisagras con motor. Se llama al final de Start, cuando ya estan
    // calculados los ejes de giro de cada dedo.
    void SetUpFingerMotors()
    {
        if (!useMotorFingers || fingers == null || fingers.Length == 0) return;

        // El soporte tiene que ser algo que viaje CON el brazo al bajar. Si se
        // colgasen del carro, la bisagra tiraria de ellos hacia arriba en cuanto
        // el brazo descendiese.
        Transform anfitrion = clawArm != null ? clawArm : transform;

        Rigidbody soporte = anfitrion.GetComponent<Rigidbody>();

        if (soporte == null)
        {
            soporte = anfitrion.gameObject.AddComponent<Rigidbody>();
        }

        soporte.isKinematic = true;
        soporte.useGravity = false;
        soporte.interpolation = RigidbodyInterpolation.Interpolate;

        // Un cinematico dormido deja de avisar a las articulaciones que cuelgan
        // de el, y los dedos se quedan tiesos en el aire.
        soporte.sleepThreshold = 0f;

        // El BRAZO sale de debajo del carro.
        //
        // El carro ya es un cuerpo cinematico. Colgar de el otro cuerpo
        // cinematico es lo que hacia que nada se estuviese quieto: la jerarquia
        // de transforms mueve al hijo por un lado y PhysX lo mueve por otro con
        // MovePosition, y las dos versiones no coinciden nunca del todo. Queda
        // un ancla que tiembla, y unas bisagras colgadas de un ancla que tiembla
        // hacen justo lo que se veia.
        //
        // Fuera de la jerarquia, al brazo no le toca el transform nadie mas: se
        // coloca por coordenadas de mundo calculadas desde el carro.
        if (clawArm != null && clawArm.parent != transform)
        {
            clawArm.SetParent(transform, true);
        }

        // Y los dedos SALEN de debajo del brazo.
        //
        // Un cuerpo con fisica no puede colgar de un transform que se reescribe
        // cada fotograma. El balanceo le asigna al brazo su rotacion en cada
        // Update, y eso teletransporta a los hijos: borra en el acto lo que
        // acabase de hacer la bisagra, asi que los dedos no llegaban a cerrarse
        // nunca por mucho que el motor empujase.
        //
        // A partir de aqui lo que los sujeta es la articulacion, no la
        // jerarquia. Es el mismo patron que se usa en un ragdoll, y ademas hace
        // que el balanceo del brazo los arrastre de forma fisica en vez de
        // llevarlos pegados.
        foreach (Transform dedo in fingers)
        {
            if (dedo != null && dedo.parent != transform) dedo.SetParent(transform, true);
        }

        if (fingerMotors == null) fingerMotors = GetComponent<ClawFingerMotors>();
        if (fingerMotors == null) fingerMotors = gameObject.AddComponent<ClawFingerMotors>();

        armRb = soporte;
        fingerMotors.soporte = soporte;
        fingerMotors.fingers = fingers;
        fingerMotors.ejes = fingerRotationAxis;
        fingerMotors.closedAngle = fingerCloseAngle;

        if (!fingerMotors.Construir())
        {
            Debug.LogWarning("[Garra] No he podido montar los motores de los dedos. "
                             + "Se sigue con el sistema viejo, que no agarra bien.", this);
            useMotorFingers = false;
        }
    }

    bool ConMotores { get { return useMotorFingers && fingerMotors != null && fingerMotors.Listo; } }

    void ApplyFingerAngle(int i)
    {
        if (fingers == null || i >= fingers.Length || fingers[i] == null) return;

        if (!IsFinite(currentFingerAngle[i])) currentFingerAngle[i] = 0f;

        // Con motores, la pose del dedo la manda la fisica. Aqui solo se le dice
        // al motor hacia donde tiene que ir.
        //
        // Se le pide SIEMPRE el cierre completo, no el angulo intermedio que
        // lleve la rutina: la gracia del motor es que empuje hasta que algo lo
        // pare. Si se le fuese dando el angulo poco a poco, se quedaria quieto
        // en cuanto la rutina dejase de subirlo y no apretaria nada.
        if (ConMotores)
        {
            // Abrir o cerrar se decide por HACIA DONDE va el angulo, no por
            // cuanto vale.
            //
            // Mirando solo el valor, durante toda la rampa de apertura el
            // angulo sigue siendo grande y el motor seguia recibiendo la orden
            // de apretar. Los dedos no se abrian hasta el ultimo grado, o sea
            // casi dos segundos tarde, y si la rutina se cortaba antes se
            // quedaban cerrados hasta la partida siguiente.
            float previo = anguloPrevio[i];
            anguloPrevio[i] = currentFingerAngle[i];

            if (Mathf.Abs(currentFingerAngle[i] - previo) > 0.001f)
            {
                cerrandoMotores = Mathf.Abs(currentFingerAngle[i]) > Mathf.Abs(previo);
            }

            // Y con el objetivo en el reposo, abierta sin discusion.
            if (Mathf.Abs(currentFingerAngle[i]) < 1f) cerrandoMotores = false;

            if (cerrandoMotores) fingerMotors.Cerrar(fingerMotors.ParActual);
            else fingerMotors.Abrir();

            return;
        }

        currentFingerAngle[i] = Mathf.Clamp(currentFingerAngle[i], -180f, 180f);

        Quaternion turn = Quaternion.AngleAxis(currentFingerAngle[i], fingerAxisLocal[i]);

        fingers[i].localPosition = fingerHingeLocal[i] + turn * (fingerRestLocalPos[i] - fingerHingeLocal[i]);
        fingers[i].localRotation = turn * fingerRestLocalRot[i];
    }

    void UpdateSwing()
    {
        float dt = Time.deltaTime;

        // Con el juego pausado dt es 0 y la division dejaria el balanceo en NaN
        // para siempre, incluso despues de reanudar.
        if (dt <= 0f) return;

        Vector3 currentRailPos = new Vector3(railX.localPosition.x, 0f, railZ.localPosition.z);
        Vector3 realVelocity = (currentRailPos - lastRailPosForSwing) / dt;
        lastRailPosForSwing = currentRailPos;

        float targetTiltZ = Mathf.Clamp(-realVelocity.x * swingTiltAmount, -swingMaxTiltAngle, swingMaxTiltAngle);
        float targetTiltX = Mathf.Clamp(realVelocity.z * swingTiltAmount, -swingMaxTiltAngle, swingMaxTiltAngle);

        float forceZ = -swingStiffness * (tiltZ - targetTiltZ) - swingDamping * velTiltZ;
        velTiltZ += forceZ * dt;
        tiltZ += velTiltZ * dt;

        float forceX = -swingStiffness * (tiltX - targetTiltX) - swingDamping * velTiltX;
        velTiltX += forceX * dt;
        tiltX += velTiltX * dt;

        // Red de seguridad: si el muelle se desestabiliza lo reiniciamos en vez
        // de dejar que un NaN se propague al Quaternion.
        if (!IsFinite(tiltX) || !IsFinite(tiltZ) || !IsFinite(velTiltX) || !IsFinite(velTiltZ))
        {
            tiltX = 0f;
            tiltZ = 0f;
            velTiltX = 0f;
            velTiltZ = 0f;
        }

        tiltX = Mathf.Clamp(tiltX, -swingMaxTiltAngle * 2f, swingMaxTiltAngle * 2f);
        tiltZ = Mathf.Clamp(tiltZ, -swingMaxTiltAngle * 2f, swingMaxTiltAngle * 2f);

        Quaternion swingRotation = Quaternion.Euler(tiltX, 0f, tiltZ);

        Vector3 destino = armBaseLocalPos;

        if (swingAnchor != null)
        {
            Vector3 anchorLocalPos = swingAnchor.localPosition;
            destino = anchorLocalPos + swingRotation * (armBaseLocalPos - anchorLocalPos);
        }

        ColocarBrazo(destino, swingRotation);
    }

    // Coloca el brazo. Si tiene cuerpo cinematico, se le PIDE el movimiento en
    // vez de escribirle el transform.
    //
    // La diferencia es la que hacia que los dedos fuesen cada uno por su lado.
    // Escribir el transform y llamar a SyncTransforms TELETRANSPORTA el cuerpo:
    // las bisagras de los dedos ven su restriccion rota de golpe, sin ninguna
    // velocidad que lo explique, y el solver responde con un impulso enorme
    // para recolocarlos. De ahi los dedos disparados y los peluches empujados a
    // traves del suelo.
    //
    // MovePosition mueve CON velocidad, que es justo lo que una articulacion
    // sabe seguir.
    void ColocarBrazo(Vector3 localPos, Quaternion localRot)
    {
        // localPos y localRot siguen expresados en el espacio del CARRO, aunque
        // el brazo ya no cuelgue de el: el resto del codigo razona en esas
        // coordenadas (armDownY, armBaseLocalPos) y no hay motivo para cambiarlo.
        Transform marco = railZ != null ? railZ : transform;

        Vector3 mundoPos = marco.TransformPoint(localPos);
        Quaternion mundoRot = marco.rotation * localRot;

        if (armRb != null && armRb.isKinematic)
        {
            armRb.MovePosition(mundoPos);
            armRb.MoveRotation(mundoRot);
            return;
        }

        clawArm.position = mundoPos;
        clawArm.rotation = mundoRot;
        Physics.SyncTransforms();
    }

    // El carro pasa a ser un cuerpo cinematico. Cinematico y no dinamico porque
    // lo manda el jugador, no las fuerzas: un motor que obedece a empujones se
    // siente impreciso. Pero al moverlo con MovePosition en vez de escribir el
    // transform, PhysX barre el trayecto y empuja lo que encuentre, en lugar de
    // aparecer dentro del peluche y tener que expulsarlo a manotazos.
    //
    // Es el ancla de la que colgara la cabeza en el paso siguiente: una
    // articulacion necesita un cuerpo al otro lado, no un transform suelto.
    void SetUpCarriage()
    {
        if (railZ == null) return;

        carriageRb = railZ.GetComponent<Rigidbody>();
        if (carriageRb == null) carriageRb = railZ.gameObject.AddComponent<Rigidbody>();

        carriageRb.isKinematic = true;
        carriageRb.useGravity = false;

        // Interpolado porque la fisica va a 60 Hz y la pantalla puede ir a mas:
        // sin esto el carro se ve a saltos aunque se mueva perfecto.
        carriageRb.interpolation = RigidbodyInterpolation.Interpolate;

        // Especulativo: es el modo continuo que funciona en cuerpos cinematicos
        // y evita que el carro atraviese un peluche en un solo paso.
        carriageRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    // Cuelga la cabeza del carro con una articulacion real.
    //
    // Lo primero es desengancharla del carro en la jerarquia: un Rigidbody
    // dinamico no puede ser hijo de un transform que se mueve, porque tendrias
    // al carro moviendolo por transform y a la fisica moviendolo por fuerzas al
    // mismo tiempo. De ahi salen los tirones y las explosiones.
    //
    // La articulacion tiene los tres ejes lineales limitados a la misma
    // distancia, que es exactamente una cuerda: la cabeza puede pendular por
    // donde quiera dentro de una esfera, pero no alejarse mas. Y bajar la garra
    // se reduce a agrandar esa esfera. Un torno de verdad, con un cuerpo y una
    // articulacion, en vez de quince segmentos encadenados.
    void SetUpCable()
    {
        if (!usePhysicalCable || clawArm == null || carriageRb == null) return;

        // El cable cuelga de la placa de arriba, no de la propia cabeza.
        //
        // Anclarlo en la cabeza daba un cable de 5 cm, y un pendulo de 5 cm
        // oscila a mas de 2 veces por segundo con un recorrido minusculo: era
        // justo el "se balancea poco pero muy rapido". La frecuencia de un
        // pendulo solo depende de su longitud, asi que la unica forma de que se
        // balancee despacio y amplio es que el cable mida lo que mide de verdad.
        Transform anchor = swingAnchor != null ? swingAnchor : railZ;

        // El cable acaba en la bola de la garra, no en el pivote del objeto.
        // Ese pivote esta a metros de distancia del sitio donde cuelga de
        // verdad, asi que medir hasta el daba un cable de tres metros y la
        // garra se iba al suelo. Se mide hasta hingePoint y la articulacion se
        // ancla ahi mismo, que es la geometria real del cable.
        Transform cableEnd = hingePoint != null ? hingePoint : clawArm;

        cableAnchorLocal = railZ.InverseTransformPoint(anchor.position);
        cableEndLocal = clawArm.InverseTransformPoint(cableEnd.position);
        cableBaseLength = Vector3.Distance(anchor.position, cableEnd.position);

        clawArm.SetParent(transform, true);

        clawHeadRb.isKinematic = false;
        clawHeadRb.useGravity = true;
        clawHeadRb.mass = headMass;
        clawHeadRb.linearDamping = headLinearDamping;
        clawHeadRb.angularDamping = 3f;
        clawHeadRb.interpolation = RigidbodyInterpolation.Interpolate;
        clawHeadRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // El centro de masa, colgando por debajo del enganche del cable.
        //
        // Esto es lo que hace que la garra se INCLINE en vez de ir tiesa. Un
        // pendulo se ladea porque su peso esta por debajo del punto del que
        // cuelga: al frenar, la inercia le da la vuelta alrededor de ese punto.
        //
        // Sin esto, Unity le calcula el centro de masa a partir de sus
        // colliders, y si algun dedo se quedo sin collider acaba en el pivote
        // del objeto, que en este modelo esta a metros de la garra. Entonces
        // gira sobre un punto que no tiene nada que ver y se ve rarisimo.
        clawHeadRb.centerOfMass = cableEndLocal
            + clawArm.InverseTransformDirection(Vector3.down) * comDropBelowCable;

        cableJoint = clawArm.gameObject.AddComponent<ConfigurableJoint>();
        cableJoint.connectedBody = carriageRb;
        cableJoint.autoConfigureConnectedAnchor = false;
        cableJoint.anchor = cableEndLocal;
        cableJoint.connectedAnchor = cableAnchorLocal;

        cableJoint.xMotion = ConfigurableJointMotion.Limited;
        cableJoint.yMotion = ConfigurableJointMotion.Limited;
        cableJoint.zMotion = ConfigurableJointMotion.Limited;

        cableJoint.angularXMotion = ConfigurableJointMotion.Free;
        cableJoint.angularYMotion = ConfigurableJointMotion.Free;
        cableJoint.angularZMotion = ConfigurableJointMotion.Free;

        // Un muelle que la devuelve a la vertical. Una garra colgada se inclina
        // al acelerar y vuelve sola; no da vueltas de campana.
        cableJoint.rotationDriveMode = RotationDriveMode.Slerp;

        JointDrive upright = new JointDrive();
        upright.positionSpring = uprightSpring;
        upright.positionDamper = uprightDamper;
        upright.maximumForce = Mathf.Infinity;

        cableJoint.slerpDrive = upright;
        cableJoint.targetRotation = Quaternion.identity;

        ApplyCableLength();
    }

    // Los dedos no tenian ningun collider: el agarre funcionaba solo con
    // consultas de solapamiento, asi que la garra atravesaba los peluches sin
    // moverlos. Aqui se les pone uno a cada pieza, medido de su propia malla.
    //
    // Con la cabeza ya colgando de la articulacion, esto cierra el circulo: la
    // garra empuja el peluche al chocar, y el golpe la hace balancearse a ella.
    void EnsureClawColliders()
    {
        AddPieceCollider(clawArm);

        if (fingers == null) return;

        foreach (Transform finger in fingers) AddPieceCollider(finger);
    }

    void AddPieceCollider(Transform piece)
    {
        if (piece == null) return;

        foreach (Renderer rend in piece.GetComponentsInChildren<Renderer>())
        {
            if (rend == null || rend is LineRenderer) continue;
            if (rend.GetComponent<Collider>() != null) continue;

            MeshFilter filter = rend.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            // Limite de cordura, y no es paranoia: una malla puede ser varios
            // trozos repartidos, y entonces su caja envolvente abarca la garra
            // entera. Cajas invisibles de metro y medio sobre las que los
            // peluches se quedan flotando en el aire.
            Vector3 world = Vector3.Scale(filter.sharedMesh.bounds.size, rend.transform.lossyScale);
            float largest = Mathf.Max(world.x, Mathf.Max(world.y, world.z));

            if (largest > maxClawColliderSize)
            {
                Debug.LogWarning("[ClawController] Me salto el collider de \"" + rend.name +
                                 "\": mide " + largest.ToString("0.00") + " m, demasiado para un dedo. " +
                                 "Esa malla debe estar repartida en varios trozos.", rend);
                continue;
            }

            BoxCollider box = rend.gameObject.AddComponent<BoxCollider>();

            box.center = filter.sharedMesh.bounds.center;
            box.size = filter.sharedMesh.bounds.size;
        }
    }

    // La altura ordenada del brazo se reinterpreta como longitud de cable: asi
    // todo el codigo de descenso, parada y subida sigue valiendo tal cual.
    void ApplyCableLength()
    {
        if (cableJoint == null) return;

        SoftJointLimit limit = cableJoint.linearLimit;
        limit.limit = Mathf.Max(minCableLength, cableBaseLength + (armUpY - armBaseLocalPos.y));
        cableJoint.linearLimit = limit;
    }

    void HandleMovement()
    {
        float inputX = 0f;
        float inputZ = 0f;

        if (Input.GetKey(KeyCode.L)) inputX = 1f;
        if (Input.GetKey(KeyCode.J)) inputX = -1f;
        if (Input.GetKey(KeyCode.I)) inputZ = 1f;
        if (Input.GetKey(KeyCode.K)) inputZ = -1f;

        if (invertX) inputX *= -1f;
        if (invertZ) inputZ *= -1f;

        float targetVelX = inputX * moveSpeed;
        float targetVelZ = inputZ * moveSpeed;

        float accelRateX = (Mathf.Abs(targetVelX) > 0.01f) ? acceleration : deceleration;
        float accelRateZ = (Mathf.Abs(targetVelZ) > 0.01f) ? acceleration : deceleration;

        // Paso de fisica, no de fotograma: esto vive en FixedUpdate. Con
        // Time.deltaTime el carro avanzaba distinto segun los FPS y el empuje
        // sobre los peluches nunca era el mismo dos veces.
        float dt = Time.fixedDeltaTime;

        currentVelX = Mathf.MoveTowards(currentVelX, targetVelX, accelRateX * dt);
        currentVelZ = Mathf.MoveTowards(currentVelZ, targetVelZ, accelRateZ * dt);

        Vector3 posX = railX.localPosition;
        posX.x += currentVelX * dt;
        if (posX.x >= limitXMax || posX.x <= limitXMin) currentVelX = 0f;
        posX.x = Mathf.Clamp(posX.x, limitXMin, limitXMax);

        Vector3 posZ = railZ.localPosition;
        posZ.z += currentVelZ * dt;
        if (posZ.z >= limitZMax || posZ.z <= limitZMin) currentVelZ = 0f;
        posZ.z = Mathf.Clamp(posZ.z, limitZMin, limitZMax);

        // Se comprueba el estorbo ANTES de moverse, no despues. Antes se movia,
        // se preguntaba y se desandaba: eso daba un tiron visible y, con el
        // carro empujando peluches, dejaba contactos a medias.
        Vector3 hingeOffset = hingePoint.position - railZ.position;
        Vector3 intendedCarriage = railX.parent != null
            ? railX.parent.TransformPoint(new Vector3(posX.x, railX.localPosition.y, railX.localPosition.z))
            : new Vector3(posX.x, railX.localPosition.y, railX.localPosition.z);

        Vector3 intendedHinge = intendedCarriage + (railZ.position - railX.position) + hingeOffset
                              + railZ.parent.TransformVector(new Vector3(0f, 0f, posZ.z - railZ.localPosition.z));

        if (ClawBlockedAt(intendedHinge))
        {
            currentVelX = 0f;
            currentVelZ = 0f;
            return;
        }

        // El rail largo es decorado: lo unico que toca peluches es el carro.
        railX.localPosition = posX;

        // Y el carro se mueve avisando al motor de fisicas, no escribiendo el
        // transform a pelo. Esa es la diferencia entre empujar un peluche y
        // aparecer dentro de el para que PhysX lo expulse a manotazos.
        Vector3 targetWorld = railZ.parent.TransformPoint(posZ);

        if (carriageRb != null) carriageRb.MovePosition(targetWorld);
        else railZ.localPosition = posZ;
    }

    IEnumerator GrabSequence()
    {
        isBusy = true;

        if (ConMotores)
        {
            // Como una maquina de verdad: la placa decide cuanta corriente le da
            // al motor en esta partida. Casi siempre poca.
            float par = fingerMotors.ParaEstaPartida();
            Debug.Log(string.Format("[Garra] Partida con {0:F3} Nm de apriete", par));
        }

        if (useRealisticGripPhysics)
        {
            float ratingMin = IsNPCTurn ? gripForceNPCMin : gripForcePlayerMin;
            float ratingMax = IsNPCTurn ? gripForceNPCMax : gripForcePlayerMax;
            currentGripForceRating = Random.Range(ratingMin, ratingMax);
        }
        else
        {
            currentGripStrength = baseGripStrength + Random.Range(-gripRandomRange, gripRandomRange);
        }

        yield return MoveArmDownUntilPlushContact();

        Debug.Log($"[ClawDiag] Descent stopped. armBaseLocalPos.y={armBaseLocalPos.y:F3} armDownY={armDownY:F3}");

        yield return new WaitForSeconds(1f);

        Coroutine gripMonitor = null;
        Coroutine emptyCloseRoutine = null;
        bool jointExistsAfterAttempt;

        if (useRealisticGripPhysics)
        {
            Coroutine liveGripRoutine = StartCoroutine(CloseFingersLiveGrip(fingerCloseAngle));

            yield return new WaitForSeconds(closeBeforeLiftDelay);

            yield return MoveArmTo(armUpY);

            yield return new WaitForSeconds(postLiftCloseGrace);

            StopCoroutine(liveGripRoutine);

            Physics.SyncTransforms();

            int validContactsRealistic = CountValidFingerContacts();
            Debug.Log($"[ClawDiag] After live-grip closing: validContacts={validContactsRealistic}/{requiredContactCount} activeJoints={ActiveFingerJointCount()} gripForceRating={currentGripForceRating:F2}");

            if (ConMotores)
            {
                heldPlushRb = PelucheEnLaGarra();
                jointExistsAfterAttempt = heldPlushRb != null;

                Debug.Log(heldPlushRb != null
                    ? "[Garra] Sube con " + heldPlushRb.name + ", agarrado por rozamiento"
                    : "[Garra] Ha subido de vacio");
            }
            else
            {
                jointExistsAfterAttempt = ActiveFingerJointCount() > 0;
            }

            emptyCloseRoutine = StartCoroutine(FinishClosingUnjointedFingers(fingerCloseAngle));

            if (jointExistsAfterAttempt)
            {
                yield return new WaitForSeconds(grabVerificationDelay);

                bool stillHeld = ConMotores
                                 ? PelucheEnLaGarra() != null
                                 : ActiveFingerJointCount() > 0;

                if (stillHeld)
                {
                    if (heldPlushRb != null)
                    {
                        PlushItem confirmedItem = heldPlushRb.GetComponent<PlushItem>();
                        if (confirmedItem != null)
                        {
                            confirmedItem.isGrabbed = true;
                            confirmedItem.hasBeenGrabbed = true;
                        }
                    }

                    Debug.Log("[ClawDiag] Grab confirmed, joint(s) survived verification.");
                    gripMonitor = StartCoroutine(MonitorGripLossRealistic());
                }
                else
                {
                    Debug.Log("[ClawDiag] Grab failed verification, joint(s) broke.");
                    ReleaseAllFingerJoints();
                }
            }

            yield return new WaitForSeconds(0.75f);
        }
        else
        {
            yield return RotateFingersTo(fingerCloseAngle, true);

            Physics.SyncTransforms();

            int validContacts = CountValidFingerContacts();
            Debug.Log($"[ClawDiag] After closing: validContacts={validContacts}/{requiredContactCount} lastTouched={(lastTouchedPlushCollider != null ? lastTouchedPlushCollider.name : "NULL")}");

            bool grabAttempted = validContacts >= requiredContactCount;

            if (grabAttempted)
            {
                TryGrabPlush();
                jointExistsAfterAttempt = currentJoint != null;
                Debug.Log($"[ClawDiag] TryGrabPlush result: jointCreated={currentJoint != null}");
            }
            else
            {
                jointExistsAfterAttempt = false;
            }

            if (jointExistsAfterAttempt)
            {
                yield return new WaitForSeconds(grabVerificationDelay);

                if (currentJoint != null)
                {
                    if (heldPlushRb != null)
                    {
                        PlushItem confirmedItem = heldPlushRb.GetComponent<PlushItem>();
                        if (confirmedItem != null)
                        {
                            confirmedItem.isGrabbed = true;
                            confirmedItem.hasBeenGrabbed = true;
                        }
                    }

                    Debug.Log("[ClawDiag] Grab confirmed, joint survived verification.");
                    gripMonitor = StartCoroutine(MonitorGripLoss());
                }
                else
                {
                    Debug.Log("[ClawDiag] Grab failed verification, joint broke.");

                    if (heldPlushRb != null)
                    {
                        heldPlushRb.useGravity = true;
                    }

                    heldPlushRb = null;
                    emptyCloseRoutine = StartCoroutine(RotateFingersTo(fingerCloseAngle, false));
                }
            }
            else
            {
                emptyCloseRoutine = StartCoroutine(RotateFingersTo(fingerCloseAngle, false));
            }

            yield return new WaitForSeconds(1.5f);

            yield return MoveArmTo(armUpY);

            yield return new WaitForSeconds(0.75f);
        }

        yield return MoveRailsTo(prizeZone.localPosition, prizeTravelSpeed);

        yield return new WaitForSeconds(1f);

        if (gripMonitor != null)
        {
            StopCoroutine(gripMonitor);
        }

        if (emptyCloseRoutine != null)
        {
            StopCoroutine(emptyCloseRoutine);
        }

        // Quien venga en la garra al llegar aqui es el premio, caiga donde caiga.
        PlushItem prize = GetHeldPlush();

        if (useRealisticGripPhysics)
        {
            ReleaseAllFingerJoints();
            yield return RotateFingersTo(0f, false);
        }
        else
        {
            yield return RotateFingersTo(0f, false);
            ReleasePlush();
        }

        if (prize != null)
        {
            StartCoroutine(DeliverPrize(prize));
        }

        yield return MoveRailsTo(new Vector3(startPosX, railZ.localPosition.y, startPosZ), prizeTravelSpeed);

        isBusy = false;
        isControllable = false;
    }

    IEnumerator MonitorGripLoss()
    {
        Rigidbody droppedRb = heldPlushRb;

        while (currentJoint != null)
        {
            yield return null;
        }

        if (droppedRb != null)
        {
            droppedRb.useGravity = true;

            PlushItem droppedItem = droppedRb.GetComponent<PlushItem>();
            if (droppedItem != null)
            {
                droppedItem.isGrabbed = false;
            }
        }

        heldPlushRb = null;

        yield return PlaySlipReaction();
    }

    IEnumerator MonitorGripLossRealistic()
    {
        Rigidbody droppedRb = heldPlushRb;

        // Se vigila hasta que deja de estar. Con rozamiento puro esto puede
        // pasar en cualquier momento: al subir, al desplazarse o justo encima
        // del agujero, que es lo que se pidio.
        while (true)
        {
            if (ConMotores)
            {
                if (PelucheEnLaGarra() == null) break;
            }
            else
            {
                if (ActiveFingerJointCount() <= 0) break;

                if (!PlushIsInsideGrip(droppedRb))
                {
                    ReleaseAllFingerJoints();
                    break;
                }
            }

            yield return null;
        }

        if (droppedRb != null)
        {
            PlushItem droppedItem = droppedRb.GetComponent<PlushItem>();
            if (droppedItem != null)
            {
                droppedItem.isGrabbed = false;
            }
        }

        heldPlushRb = null;

        yield return PlaySlipReaction();
    }

    IEnumerator PlaySlipReaction()
    {
        float direction = Mathf.Sign(fingerCloseAngle);
        float slipTarget = fingerCloseAngle + direction * slipExtraCloseAngle;
        float safetyTimer = 0f;

        bool stillMoving = true;

        while (stillMoving)
        {
            stillMoving = false;

            for (int i = 0; i < fingers.Length; i++)
            {
                float diff = slipTarget - currentFingerAngle[i];
                if (Mathf.Abs(diff) <= 0.5f) continue;
                if (FingerBlockedByObstacle(i)) continue;

                float step = Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff), slipCloseSpeed * Time.deltaTime);
                currentFingerAngle[i] += step;
                ApplyFingerAngle(i);
                stillMoving = true;
            }

            safetyTimer += Time.deltaTime;
            if (safetyTimer > 1.5f) break;

            yield return null;
        }
    }

    // Hasta donde baja la garra, medido de lo que hay debajo.
    //
    // Como funciona una de verdad: la garra baja colgando de un cable y sigue
    // bajando hasta que se POSA sobre algo. Ahi el cable se afloja, un final de
    // carrera lo nota y para. No hay ningun sensor que vea el peluche desde
    // arriba; lo que para la bajada es el contacto.
    //
    // Lo que habia era otra cosa: paraba cuando el peluche quedaba a 5 cm de la
    // BISAGRA, que es el punto de giro y esta arriba del todo, a lo largo de un
    // dedo por encima de las puntas. La garra se quedaba encaramada sobre el
    // peluche y despues cerraba en el aire, por encima de el. Por eso no
    // agarraba: no llegaba a rodearlo nunca.
    //
    // Aqui se barre una esfera del ancho de la garra hacia abajo, se mira la
    // cima de lo primero que encuentra, y se baja hasta que las PUNTAS quedan
    // hundidas en esa cima. Hundidas y no rozandola: una garra que se posa sobre
    // un monton de peluches se hunde, y es hundiendose como los brazos acaban
    // abrazandolo por los lados en vez de resbalar por encima.
    float AlturaDeParada()
    {
        float puntasY = PuntaMasBaja();
        float alcance = Mathf.Max(0.01f, hingePoint.position.y - puntasY);
        float maxDescenso = armBaseLocalPos.y - armDownY;

        if (maxDescenso <= 0f) return armDownY;

        // El suelo, deducido: al final del recorrido las puntas quedan justo
        // encima de el, que es como se calculo armDownY al montar la maquina.
        float sueloY = puntasY - maxDescenso;

        RaycastHit hit;
        bool hay = Physics.SphereCast(hingePoint.position,
                                      Mathf.Max(0.02f, RadioGarra() * 0.75f),
                                      Vector3.down, out hit, maxDescenso + alcance,
                                      plushLayer, QueryTriggerInteraction.Ignore);

        if (!hay)
        {
            intendedTargetRb = null;
            return armDownY;   // no hay nada: hasta abajo
        }

        intendedTargetRb = hit.collider.attachedRigidbody;

        float cima = hit.collider.bounds.max.y;
        float altoPeluche = hit.collider.bounds.size.y;

        // Cuanto se hunde la garra en el peluche, medido para las PUNTAS.
        //
        // Se probo a bajar hasta que la carcasa tocase el peluche y quedaba
        // peor: la carcasa va por transform y no cede, asi que al llegar abajo
        // empuja al peluche en vez de posarse sobre el. Media altura deja las
        // puntas justo en la parte mas ancha, que es de donde hay que cogerlo.
        //
        // El tope de dos tercios del dedo es para que el peluche no le pase de
        // la bisagra: por encima de ella ya no hay brazo que lo abrace.
        float hundimiento = Mathf.Min(altoPeluche * grabDepth, alcance * 0.8f);

        float objetivo = Mathf.Max(cima - hundimiento, sueloY + 0.005f);
        float parada = Mathf.Max(armDownY, armBaseLocalPos.y - (puntasY - objetivo));

        Debug.Log(string.Format(
            "[Garra] Bajada sobre {0}: cima={1:F3} alto={2:F3} hundimiento={3:F3} "
            + "puntas acaban en {4:F3} (tope {5:F3}, suelo {6:F3})",
            hit.collider.name, cima, altoPeluche, hundimiento, objetivo, armDownY, sueloY));

        return parada;
    }

    float PuntaMasBaja()
    {
        float y = float.MaxValue;

        for (int i = 0; i < fingers.Length; i++)
        {
            Transform p = (fingerTips != null && fingerTips.Length > i && fingerTips[i] != null)
                          ? fingerTips[i] : fingers[i];

            if (p != null) y = Mathf.Min(y, p.position.y);
        }

        return y == float.MaxValue ? hingePoint.position.y : y;
    }

    // Que peluche lleva la garra, mirado en la fisica y no en las uniones.
    //
    // Aqui estaba el fallo de "a 100% de fuerza tampoco agarra". Todo el juego
    // preguntaba por las uniones invisibles para saber si habia premio, y con
    // rozamiento puro no se crea ninguna: siempre salia cero. Fisicamente la
    // garra podia estar subiendo el peluche perfectamente agarrado y el juego
    // no se enteraba, asi que no lo daba por cogido ni lo cobraba.
    //
    // Sin uniones, la unica prueba honesta es mirar si el peluche sigue ahi
    // arriba cuando la garra ya ha subido. Si esta dentro del hueco de la garra
    // con la garra en alto, es que va agarrado; no hay otra forma de que haya
    // llegado hasta ahi.
    Rigidbody PelucheEnLaGarra()
    {
        if (hingePoint == null) return null;

        float alcance = Mathf.Max(0.01f, hingePoint.position.y - PuntaMasBaja());
        float radio = Mathf.Max(0.05f, RadioGarra() * 1.2f);

        Vector3 centro = hingePoint.position + Vector3.down * (alcance * 0.5f);

        Collider[] tocando = Physics.OverlapSphere(centro, radio, plushLayer,
                                                   QueryTriggerInteraction.Ignore);

        Rigidbody mejor = null;
        float masCerca = float.MaxValue;

        foreach (Collider c in tocando)
        {
            if (c == null || c.attachedRigidbody == null) continue;

            float d = Vector3.Distance(c.ClosestPoint(centro), centro);

            if (d < masCerca)
            {
                masCerca = d;
                mejor = c.attachedRigidbody;
            }
        }

        return mejor;
    }

    float RadioGarra()
    {
        float r = 0f;

        for (int i = 0; i < fingers.Length; i++)
        {
            Transform p = (fingerTips != null && fingerTips.Length > i && fingerTips[i] != null)
                          ? fingerTips[i] : fingers[i];

            if (p == null) continue;

            Vector3 d = p.position - hingePoint.position;
            d.y = 0f;
            r = Mathf.Max(r, d.magnitude);
        }

        return r;
    }

    IEnumerator MoveArmDownUntilPlushContact()
    {
        intendedTargetRb = null;

        Vector3 pos = armBaseLocalPos;
        float safetyTimer = 0f;
        float currentVelY = 0f;

        // Se decide antes de empezar, con la garra quieta. Medir mientras baja
        // daria una lectura distinta cada fotograma y la parada saldria a una
        // altura u otra segun el momento en que se hiciese la comprobacion.
        float parada = AlturaDeParada();

        while (pos.y > parada)
        {
            float remaining = pos.y - parada;
            float targetSpeed = ApproachSpeed(remaining, armMoveSpeed, armDeceleration);
            float targetVelY = -targetSpeed;
            currentVelY = Mathf.MoveTowards(currentVelY, targetVelY, armAcceleration * Time.deltaTime);

            pos.y += currentVelY * Time.deltaTime;

            if (pos.y < parada)
            {
                pos.y = parada;
            }

            armBaseLocalPos = pos;

            safetyTimer += Time.deltaTime;
            if (safetyTimer > 6f) break;

            yield return null;
        }

        Debug.Log(string.Format("[Garra] Bajada terminada en {0:F3} (tope {1:F3})",
                                armBaseLocalPos.y, armDownY));
    }

    Collider DetectPlushBelowClaw()
    {
        Vector3 zoneCenter = hingePoint.position + Vector3.down * detectionHeightOffset;
        Collider[] hits = Physics.OverlapSphere(zoneCenter, detectionRadius, plushLayer);

        Collider closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider c in hits)
        {
            if (c is MeshCollider mc && !mc.convex) continue;

            float dist = Vector3.Distance(c.ClosestPoint(hingePoint.position), hingePoint.position);
            if (dist < closestDist)
            {
                closest = c;
                closestDist = dist;
            }
        }

        return closest;
    }

    IEnumerator MoveArmTo(float targetY)
    {
        Vector3 pos = armBaseLocalPos;
        float safetyTimer = 0f;
        float currentVelY = 0f;

        while (Mathf.Abs(pos.y - targetY) > 0.01f)
        {
            if (ClawBlockedAt(hingePoint.position))
            {
                break;
            }

            float direction = Mathf.Sign(targetY - pos.y);
            float remaining = Mathf.Abs(targetY - pos.y);
            float targetSpeed = ApproachSpeed(remaining, armMoveSpeed, armDeceleration);
            float targetVelY = direction * targetSpeed;
            currentVelY = Mathf.MoveTowards(currentVelY, targetVelY, armAcceleration * Time.deltaTime);

            pos.y += currentVelY * Time.deltaTime;

            if ((direction > 0f && pos.y > targetY) || (direction < 0f && pos.y < targetY))
            {
                pos.y = targetY;
            }

            armBaseLocalPos = pos;

            safetyTimer += Time.deltaTime;
            if (safetyTimer > 5f) break;

            yield return null;
        }

        if (!ClawBlockedAt(hingePoint.position))
        {
            pos.y = targetY;
        }
        armBaseLocalPos = pos;
    }

    float ApproachSpeed(float remainingDistance, float maxSpeed, float decel)
    {
        float brakingSpeed = Mathf.Sqrt(Mathf.Max(0f, 2f * decel * remainingDistance));
        return Mathf.Min(maxSpeed, brakingSpeed);
    }

    IEnumerator RotateFingersTo(float targetAngle, bool checkContact)
    {
        float safetyTimer = 0f;
        bool stillMoving = true;

        if (checkContact)
        {
            lastTouchedPlushCollider = null;

            for (int i = 0; i < fingers.Length; i++)
            {
                fingerStopped[i] = false;
            }
        }

        float startAngle = currentFingerAngle.Length > 0 ? currentFingerAngle[0] : 0f;

        while (stillMoving)
        {
            stillMoving = false;

            for (int i = 0; i < fingers.Length; i++)
            {
                if (checkContact && fingerStopped[i]) continue;

                float diff = targetAngle - currentFingerAngle[i];

                if (Mathf.Abs(diff) <= 0.5f) continue;

                bool hasMovedEnough = Mathf.Abs(currentFingerAngle[i] - startAngle) >= minAngleBeforeContactCheck;

                if (checkContact && hasMovedEnough && IsFingerTouchingPlush(i))
                {
                    fingerStopped[i] = true;
                    continue;
                }

                if (FingerBlockedByObstacle(i))
                {
                    if (checkContact) fingerStopped[i] = true;
                    continue;
                }

                float step = Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff), fingerSpeed * Time.deltaTime);
                currentFingerAngle[i] += step;
                ApplyFingerAngle(i);
                stillMoving = true;
            }

            safetyTimer += Time.deltaTime;
            if (safetyTimer > 3f) break;

            yield return null;
        }
    }

    bool IsFingerTouchingPlush(int i)
    {
        Vector3 basePos = fingers[i].position;
        Vector3 tipPos = (fingerTips != null && fingerTips.Length > i && fingerTips[i] != null) ? fingerTips[i].position : basePos;

        Vector3 contactPoint;
        Collider hit = SampleAlongFingerForPlush(basePos, tipPos, fingerContactDistance, out contactPoint);
        if (hit == null) return false;

        lastTouchedPlushCollider = hit;
        fingerTouchedCollider[i] = hit;
        fingerContactPoint[i] = contactPoint;
        return true;
    }

    int CountValidFingerContacts()
    {
        int count = 0;

        for (int i = 0; i < fingers.Length; i++)
        {
            bool touchedSolidly = fingerStopped[i] && Mathf.Abs(currentFingerAngle[i]) >= minClosingAngleForValidGrip;
            if (touchedSolidly) count++;
        }

        return count;
    }

    Collider SampleAlongFingerForPlush(Vector3 basePos, Vector3 tipPos, float distanceThreshold, out Vector3 contactPoint)
    {
        int samples = 8;
        float broadSearchRadius = 0.5f;

        for (int s = 0; s <= samples; s++)
        {
            float t = Mathf.Lerp(fingerContactSampleStart, 1f, (float)s / samples);
            Vector3 checkPoint = Vector3.Lerp(basePos, tipPos, t);

            Collider[] nearby = Physics.OverlapSphere(checkPoint, broadSearchRadius, plushLayer);
            foreach (Collider c in nearby)
            {
                if (c is MeshCollider mc && !mc.convex) continue;

                Vector3 closest = c.ClosestPoint(checkPoint);
                float dist = Vector3.Distance(closest, checkPoint);
                if (dist <= distanceThreshold)
                {
                    contactPoint = closest;
                    return c;
                }
            }
        }

        contactPoint = Vector3.zero;
        return null;
    }

    IEnumerator MoveRailsTo(Vector3 targetLocalPos, float speed)
    {
        bool stillMoving = true;
        float safetyTimer = 0f;
        float railDeceleration = deceleration;

        while (stillMoving)
        {
            Vector3 prevPosX = railX.localPosition;
            Vector3 prevPosZ = railZ.localPosition;

            Vector3 posX = prevPosX;
            Vector3 posZ = prevPosZ;

            float remainingX = Mathf.Abs(targetLocalPos.x - posX.x);
            float remainingZ = Mathf.Abs(targetLocalPos.z - posZ.z);

            float speedX = ApproachSpeed(remainingX, speed, railDeceleration);
            float speedZ = ApproachSpeed(remainingZ, speed, railDeceleration);

            // Paso de fisica: este viaje es justo cuando lleva el peluche
            // colgando, y es cuando mas importa que el movimiento sea regular.
            float dt = Time.fixedDeltaTime;

            posX.x = Mathf.MoveTowards(posX.x, targetLocalPos.x, speedX * dt);
            posZ.z = Mathf.MoveTowards(posZ.z, targetLocalPos.z, speedZ * dt);

            Vector3 hingeOffset = hingePoint.position - railZ.position;
            Vector3 intendedHinge = hingeOffset
                + railX.parent.TransformPoint(new Vector3(posX.x, prevPosX.y, prevPosX.z))
                + (railZ.position - railX.position)
                + railZ.parent.TransformVector(new Vector3(0f, 0f, posZ.z - prevPosZ.z));

            if (ClawBlockedAt(intendedHinge)) break;

            railX.localPosition = posX;

            if (carriageRb != null) carriageRb.MovePosition(railZ.parent.TransformPoint(posZ));
            else railZ.localPosition = posZ;

            stillMoving = Vector3.Distance(new Vector3(posX.x, 0, posZ.z), new Vector3(targetLocalPos.x, 0, targetLocalPos.z)) > 0.02f;

            safetyTimer += dt;
            if (safetyTimer > 8f) break;

            yield return new WaitForFixedUpdate();
        }
    }

    void TryGrabPlush()
    {
        if (lastTouchedPlushCollider == null) return;

        Rigidbody plushRb = lastTouchedPlushCollider.attachedRigidbody;
        if (plushRb == null) return;

        if (plushRb.mass > maxGrabMass) return;

        plushRb.linearVelocity = Vector3.zero;
        plushRb.angularVelocity = Vector3.zero;
        plushRb.useGravity = false;

        currentJoint = clawHead.gameObject.AddComponent<FixedJoint>();
        currentJoint.connectedBody = plushRb;
        currentJoint.breakForce = currentGripStrength;
        currentJoint.breakTorque = currentGripStrength;

        heldPlushRb = plushRb;
    }

    // Que peluche lleva la garra ahora mismo, sea con joints o con FixedJoint.
    PlushItem GetHeldPlush()
    {
        if (activeFingerJoints != null)
        {
            foreach (ConfigurableJoint joint in activeFingerJoints)
            {
                if (joint == null || joint.connectedBody == null) continue;

                PlushItem item = joint.connectedBody.GetComponent<PlushItem>();
                if (item != null) return item;
            }
        }

        if (currentJoint != null && currentJoint.connectedBody != null)
        {
            PlushItem item = currentJoint.connectedBody.GetComponent<PlushItem>();
            if (item != null) return item;
        }

        if (heldPlushRb != null)
        {
            return heldPlushRb.GetComponent<PlushItem>();
        }

        return null;
    }

    // La rampa y el agujero de salida cambian de sitio segun la maquina, asi que
    // no dependemos de que el peluche caiga dentro de un trigger concreto:
    // damos un margen para que ruede y lo entregamos nosotros.
    IEnumerator DeliverPrize(PlushItem plush)
    {
        yield return new WaitForSeconds(prizeDeliverDelay);

        if (plush == null || plush.collected) yield break;

        // Con el agarre por joints el flag no siempre se limpia solo, y aqui ya
        // sabemos con certeza que la garra lo ha soltado.
        plush.isGrabbed = false;
        plush.hasBeenGrabbed = true;

        PlushDropZone zone = transform.root.GetComponentInChildren<PlushDropZone>(true);

        if (zone != null)
        {
            zone.Collect(plush);
            yield break;
        }

        // Sin zona de premio configurada seguimos la misma regla: el jugador cobra.
        plush.collected = true;

        if (activeCarrySpot == null && GameManager.Instance != null)
        {
            GameManager.Instance.AddMoney(fallbackPrizeReward);
        }

        Destroy(plush.gameObject);
    }

    void ReleasePlush()
    {
        heldPlushRb = null;

        if (currentJoint != null)
        {
            Rigidbody plushRb = currentJoint.connectedBody;
            Destroy(currentJoint);

            if (plushRb != null)
            {
                plushRb.useGravity = true;

                PlushItem item = plushRb.GetComponent<PlushItem>();
                if (item != null)
                {
                    item.isGrabbed = false;
                }
            }

            currentJoint = null;
        }
    }

    Transform GetJointHost(int i)
    {
        if (fingerTips != null && fingerTips.Length > i && fingerTips[i] != null)
        {
            return fingerTips[i];
        }

        return fingers[i];
    }

    IEnumerator CloseFingersLiveGrip(float targetAngle)
    {
        float safetyTimer = 0f;

        lastTouchedPlushCollider = null;
        currentGrabTargetRb = intendedTargetRb;

        for (int i = 0; i < fingers.Length; i++)
        {
            fingerStopped[i] = false;
        }

        float startAngle = currentFingerAngle.Length > 0 ? currentFingerAngle[0] : 0f;

        while (safetyTimer <= liveGripMaxDuration)
        {
            for (int i = 0; i < fingers.Length; i++)
            {
                bool hasMovedEnough = Mathf.Abs(currentFingerAngle[i] - startAngle) >= minAngleBeforeContactCheck;
                bool touchingNow = hasMovedEnough && IsFingerTouchingPlush(i) && TouchIsOnGrabTarget(i);

                if (touchingNow)
                {
                    fingerStopped[i] = true;

                    if (activeFingerJoints[i] == null)
                    {
                        TryCreateJointForFinger(i);
                    }

                    continue;
                }

                if (FingerBlockedByObstacle(i))
                {
                    fingerStopped[i] = true;
                    continue;
                }

                fingerStopped[i] = false;

                float diff = targetAngle - currentFingerAngle[i];
                if (Mathf.Abs(diff) <= 0.5f) continue;

                float step = Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff), fingerSpeed * Time.deltaTime);
                currentFingerAngle[i] += step;
                ApplyFingerAngle(i);
            }

            safetyTimer += Time.deltaTime;

            yield return null;
        }
    }

    bool TouchIsOnGrabTarget(int i)
    {
        if (fingerTouchedCollider[i] == null) return false;

        Rigidbody rb = fingerTouchedCollider[i].attachedRigidbody;
        if (rb == null) return false;

        if (currentGrabTargetRb == null) return true;

        return rb == currentGrabTargetRb;
    }

    void TryCreateJointForFinger(int i)
    {
        // Con motores no se crea ninguna union. Lo que sostiene al peluche es el
        // rozamiento contra los dedos apretando, que es lo que se pidio. Una
        // union invisible aqui haria que el agarre no fallase nunca.
        if (ConMotores) return;

        if (fingerTouchedCollider[i] == null) return;

        Rigidbody rb = fingerTouchedCollider[i].attachedRigidbody;
        if (rb == null) return;

        if (currentGrabTargetRb == null)
        {
            currentGrabTargetRb = rb;
        }

        if (rb != currentGrabTargetRb) return;
        if (currentGrabTargetRb.mass > maxGrabMass) return;
        if (!PlushIsInsideGrip(currentGrabTargetRb)) return;

        if (ActiveFingerJointCount() == 0)
        {
            currentGrabTargetRb.linearVelocity = Vector3.zero;
            currentGrabTargetRb.angularVelocity = Vector3.zero;
        }

        Rigidbody hostRb = fingerJointHostRb[i];
        if (hostRb == null) return;

        Transform jointHost = GetJointHost(i);
        float gripNewtons = currentGripForceRating * forceNewtonsPerUnit;

        ConfigurableJoint joint = jointHost.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = currentGrabTargetRb;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedAnchor = currentGrabTargetRb.transform.InverseTransformPoint(fingerContactPoint[i]);

        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Free;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;

        JointDrive drive = new JointDrive
        {
            positionSpring = gripNewtons * 12f,
            positionDamper = gripNewtons * 0.5f,
            maximumForce = gripNewtons
        };
        joint.xDrive = drive;
        joint.yDrive = drive;
        joint.zDrive = drive;

        joint.breakForce = gripNewtons * breakForceMultiplier;
        joint.breakTorque = Mathf.Infinity;

        activeFingerJoints[i] = joint;
        heldPlushRb = currentGrabTargetRb;
    }

    IEnumerator FinishClosingUnjointedFingers(float targetAngle)
    {
        float safetyTimer = 0f;
        bool stillMoving = true;

        while (stillMoving)
        {
            stillMoving = false;

            for (int i = 0; i < fingers.Length; i++)
            {
                if (activeFingerJoints[i] != null) continue;

                if (IsFingerTouchingPlush(i) && TouchIsOnGrabTarget(i))
                {
                    fingerStopped[i] = true;
                    TryCreateJointForFinger(i);
                    continue;
                }

                if (FingerBlockedByObstacle(i))
                {
                    fingerStopped[i] = true;
                    continue;
                }

                fingerStopped[i] = false;

                float diff = targetAngle - currentFingerAngle[i];
                if (Mathf.Abs(diff) <= 0.5f) continue;

                float step = Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff), fingerSpeed * Time.deltaTime);
                currentFingerAngle[i] += step;
                ApplyFingerAngle(i);
                stillMoving = true;
            }

            safetyTimer += Time.deltaTime;
            if (safetyTimer > 3f) break;

            yield return null;
        }
    }

    int ActiveFingerJointCount()
    {
        if (activeFingerJoints == null) return 0;

        int count = 0;
        foreach (ConfigurableJoint joint in activeFingerJoints)
        {
            if (joint != null) count++;
        }

        return count;
    }

    void ReleaseAllFingerJoints()
    {
        if (activeFingerJoints != null)
        {
            for (int i = 0; i < activeFingerJoints.Length; i++)
            {
                if (activeFingerJoints[i] != null)
                {
                    Destroy(activeFingerJoints[i]);
                    activeFingerJoints[i] = null;
                }
            }
        }

        if (heldPlushRb != null)
        {
            PlushItem item = heldPlushRb.GetComponent<PlushItem>();
            if (item != null)
            {
                item.isGrabbed = false;
            }
        }

        heldPlushRb = null;
    }

    public Transform toySpawnPoint;

    // Los peluches caen repartidos por el area de juego, no todos en el mismo
    // punto. Antes salian del mismo sitio exacto y se apilaban en una columna.
    //
    // El area sale de los propios limites del carro: donde puede llegar la
    // garra es justo donde tiene sentido que haya peluches. Y se descartan los
    // puntos que caen sobre la boca del premio, que si no se regalan solos.
    public void SpawnToyInside(GameObject toyPrefab)
    {
        if (toyPrefab == null) return;

        Transform reference = toySpawnPoint != null ? toySpawnPoint : (hingePoint != null ? hingePoint : transform);

        Vector3 point = reference.position + Vector3.up * toyDropHeight;

        // El punto de reserva tambien por debajo del techo.
        point.y = Mathf.Min(point.y, MachineBounds.max.y - toyBoundsMargin * 2f);

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 candidate = RandomPlayAreaPoint(reference);

            if (prizeZone == null || FlatDistance(candidate, prizeZone.position) > prizeZoneClearance)
            {
                point = candidate;
                break;
            }
        }

        // Girados al azar: si todos caen igual orientados se nota muchisimo, y
        // ademas se apilan demasiado ordenados para ser un monton de peluches.
        Instantiate(toyPrefab, point, Random.rotation);
    }

    Vector3 RandomPlayAreaPoint(Transform reference)
    {
        if (railX == null || railZ == null) return reference.position;

        // Medido desde el CENTRO del area, no desde donde este el carro ahora.
        //
        // Antes restaba la posicion actual del carro y se lo sumaba al punto de
        // suelta, que es otro sitio distinto: dos origenes mezclados. Con unos
        // limites que no estan centrados en cero (los tuyos van de -0,16 a 1,3)
        // eso mandaba peluches hasta metro y medio fuera de la maquina.
        float centerX = (limitXMin + limitXMax) * 0.5f;
        float centerZ = (limitZMin + limitZMax) * 0.5f;

        float halfX = (limitXMax - limitXMin) * 0.5f * toyScatterSpread;
        float halfZ = (limitZMax - limitZMin) * 0.5f * toyScatterSpread;

        // Se parte de donde esta la garra AHORA, no de un punto suelto.
        //
        // La garra esta dentro del cristal por definicion, y el desplazamiento
        // que se le suma es exactamente el que haria el carro para llegar a ese
        // punto. Asi el resultado es siempre un sitio al que la garra puede ir,
        // o sea, dentro. Antes partia de toySpawnPoint y recortaba contra la
        // caja EXTERIOR del mueble, que es mas grande que el hueco de cristal:
        // por eso seguian apareciendo peluches fuera.
        Vector3 basePoint = hingePoint != null ? hingePoint.position : reference.position;

        float dx = Random.Range(-halfX, halfX) + centerX - railX.localPosition.x;
        float dz = Random.Range(-halfZ, halfZ) + centerZ - railZ.localPosition.z;

        Vector3 offset = railX.parent.TransformVector(new Vector3(dx, 0f, 0f))
                       + railZ.parent.TransformVector(new Vector3(0f, 0f, dz));

        Vector3 point = basePoint + offset;

        point.y = reference.position.y + toyDropHeight;

        // Red de seguridad: pase lo que pase con las cuentas, dentro de la
        // maquina. Si el punto de suelta no estuviera centrado en el area, el
        // reparto se descentraria, pero ningun peluche acabaria en el suelo.
        Bounds inside = MachineBounds;
        inside.Expand(-toyBoundsMargin * 2f);

        point.x = Mathf.Clamp(point.x, inside.min.x, inside.max.x);
        point.z = Mathf.Clamp(point.z, inside.min.z, inside.max.z);

        // Y sobre todo por debajo del techo. Antes solo recortaba en horizontal,
        // asi que un peluche soltado desde arriba podia aparecer sobre el tejado
        // de la maquina y quedarse ahi encima, fuera del cristal.
        point.y = Mathf.Min(point.y, inside.max.y);

        return point;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    public void PlayAutomatically(System.Action onComplete)
    {
        StartCoroutine(AutoPlayRoutine(onComplete));
    }

    IEnumerator AutoPlayRoutine(System.Action onComplete)
    {
        isBusy = true;

        float targetX = Random.Range(limitXMin, limitXMax);
        float targetZ = Random.Range(limitZMin, limitZMax);

        yield return MoveRailsTo(new Vector3(targetX, 0f, targetZ), moveSpeed);

        yield return new WaitForSeconds(0.4f);

        yield return GrabSequence();

        onComplete?.Invoke();
    }
}
