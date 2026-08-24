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

    [Header("Viaje a zona de premio")]
    public float prizeTravelSpeed = 0.6f;
    [Tooltip("Margen para que el premio caiga por la rampa antes de retirarlo.")]
    public float prizeDeliverDelay = 1.5f;
    public int fallbackPrizeReward = 20;

    [Header("Balanceo (Swing)")]
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

        clawHeadRb = clawHead.GetComponent<Rigidbody>();
        if (clawHeadRb == null)
        {
            clawHeadRb = clawHead.gameObject.AddComponent<Rigidbody>();
        }
        clawHeadRb.isKinematic = true;
        clawHeadRb.useGravity = false;

        startPosX = railX.localPosition.x;
        startPosZ = railZ.localPosition.z;

        lastRailPosForSwing = new Vector3(railX.localPosition.x, 0f, railZ.localPosition.z);
        armBaseLocalPos = clawArm.localPosition;

        currentFingerAngle = new float[fingers.Length];
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
        if (isControllable && !isBusy)
        {
            HandleMovement();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                StartCoroutine(GrabSequence());
            }
        }

        ApplyArmPosition();
    }

    void ApplyArmPosition()
    {
        if (enableSwing)
        {
            UpdateSwing();
        }
        else
        {
            clawArm.localPosition = armBaseLocalPos;
            clawArm.localRotation = Quaternion.identity;
        }

        Physics.SyncTransforms();
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // La pose del dedo es funcion pura de su angulo, calculada desde el reposo.
    // Antes se acumulaba con RotateAround en espacio de mundo y cualquier
    // desfase se quedaba para siempre: los dedos acababan sueltos por el aire.
    void ApplyFingerAngle(int i)
    {
        if (fingers == null || i >= fingers.Length || fingers[i] == null) return;

        if (!IsFinite(currentFingerAngle[i])) currentFingerAngle[i] = 0f;

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

        if (swingAnchor != null)
        {
            Vector3 anchorLocalPos = swingAnchor.localPosition;
            Vector3 offset = armBaseLocalPos - anchorLocalPos;
            clawArm.localPosition = anchorLocalPos + swingRotation * offset;
        }
        else
        {
            clawArm.localPosition = armBaseLocalPos;
        }

        clawArm.localRotation = swingRotation;
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

        currentVelX = Mathf.MoveTowards(currentVelX, targetVelX, accelRateX * Time.deltaTime);
        currentVelZ = Mathf.MoveTowards(currentVelZ, targetVelZ, accelRateZ * Time.deltaTime);

        Vector3 prevPosX = railX.localPosition;
        Vector3 prevPosZ = railZ.localPosition;

        Vector3 posX = railX.localPosition;
        posX.x += currentVelX * Time.deltaTime;
        if (posX.x >= limitXMax || posX.x <= limitXMin)
        {
            currentVelX = 0f;
        }
        posX.x = Mathf.Clamp(posX.x, limitXMin, limitXMax);
        railX.localPosition = posX;

        Vector3 posZ = railZ.localPosition;
        posZ.z += currentVelZ * Time.deltaTime;
        if (posZ.z >= limitZMax || posZ.z <= limitZMin)
        {
            currentVelZ = 0f;
        }
        posZ.z = Mathf.Clamp(posZ.z, limitZMin, limitZMax);
        railZ.localPosition = posZ;

        Physics.SyncTransforms();

        if (ClawBlockedAt(hingePoint.position))
        {
            railX.localPosition = prevPosX;
            railZ.localPosition = prevPosZ;
            currentVelX = 0f;
            currentVelZ = 0f;
            Physics.SyncTransforms();
        }
    }

    IEnumerator GrabSequence()
    {
        isBusy = true;

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

            jointExistsAfterAttempt = ActiveFingerJointCount() > 0;

            emptyCloseRoutine = StartCoroutine(FinishClosingUnjointedFingers(fingerCloseAngle));

            if (jointExistsAfterAttempt)
            {
                yield return new WaitForSeconds(grabVerificationDelay);

                bool stillHeld = ActiveFingerJointCount() > 0;

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

        while (ActiveFingerJointCount() > 0)
        {
            if (!PlushIsInsideGrip(droppedRb))
            {
                ReleaseAllFingerJoints();
                break;
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

    IEnumerator MoveArmDownUntilPlushContact()
    {
        intendedTargetRb = null;

        Vector3 pos = armBaseLocalPos;
        float startY = pos.y;
        float safetyTimer = 0f;
        float currentVelY = 0f;
        bool loggedNoDetection = false;

        while (pos.y > armDownY)
        {
            bool hasDescendedEnough = (startY - pos.y) >= minDescentBeforeStop;

            if (hasDescendedEnough)
            {
                Collider detected = DetectPlushBelowClaw();

                if (detected != null)
                {
                    float ballDistance = Vector3.Distance(detected.ClosestPoint(hingePoint.position), hingePoint.position);
                    if (ballDistance <= gripHeightOffset)
                    {
                        intendedTargetRb = detected.attachedRigidbody;
                        Debug.Log($"[ClawDiag] Ball stop triggered: {detected.name} ballDistance={ballDistance:F3} pos.y={pos.y:F3}");
                        break;
                    }
                }
                else if (!loggedNoDetection)
                {
                    loggedNoDetection = true;
                    Debug.Log($"[ClawDiag] DetectPlushBelowClaw found nothing yet at pos.y={pos.y:F3}");
                }
            }

            float remaining = pos.y - armDownY;
            float targetSpeed = ApproachSpeed(remaining, armMoveSpeed, armDeceleration);
            float targetVelY = -targetSpeed;
            currentVelY = Mathf.MoveTowards(currentVelY, targetVelY, armAcceleration * Time.deltaTime);

            pos.y += currentVelY * Time.deltaTime;

            if (pos.y < armDownY)
            {
                pos.y = armDownY;
            }

            armBaseLocalPos = pos;

            safetyTimer += Time.deltaTime;
            if (safetyTimer > 6f) break;

            yield return null;
        }
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

            posX.x = Mathf.MoveTowards(posX.x, targetLocalPos.x, speedX * Time.deltaTime);
            railX.localPosition = posX;

            posZ.z = Mathf.MoveTowards(posZ.z, targetLocalPos.z, speedZ * Time.deltaTime);
            railZ.localPosition = posZ;

            Physics.SyncTransforms();

            if (ClawBlockedAt(hingePoint.position))
            {
                railX.localPosition = prevPosX;
                railZ.localPosition = prevPosZ;
                Physics.SyncTransforms();
                break;
            }

            stillMoving = Vector3.Distance(new Vector3(posX.x, 0, posZ.z), new Vector3(targetLocalPos.x, 0, targetLocalPos.z)) > 0.02f;

            safetyTimer += Time.deltaTime;
            if (safetyTimer > 8f) break;

            yield return null;
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

    public void SpawnToyInside(GameObject toyPrefab)
    {
        if (toyPrefab == null) return;

        Transform reference = toySpawnPoint != null ? toySpawnPoint : (hingePoint != null ? hingePoint : transform);

        Instantiate(toyPrefab, reference.position, Quaternion.identity);
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
