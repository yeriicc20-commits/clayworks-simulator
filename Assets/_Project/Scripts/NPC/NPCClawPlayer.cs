using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class NPCClawPlayer : MonoBehaviour
{
    public ClawController clawController;
    public Transform machineSpot;
    public Transform carryPoint;
    public Animator animator;

    public int cost = 5;

    public float moveSpeed = 1.2f;
    public float rotationSpeed = 8f;
    public float arriveDistance = 0.15f;
    public float waitBeforePlaying = 1.5f;
    public float waitAfterPlaying = 2f;
    public float waitBetweenRetries = 1f;

    [Tooltip("Probabilidad de volver a intentarlo despues de perder.\n\n"
             + "Estaba en un tercio: la mayoria se iba a la primera derrota, y "
             + "como perder es lo normal, casi ninguno llegaba a ganar nada. Un "
             + "cliente de verdad no se va a la primera; lo pica.")]
    [Range(0f, 1f)] public float retryChance = 0.7f;

    [Tooltip("Correccion de giro del modelo al mirar a la maquina. Tiene que "
             + "ser 0: el resto del script gira con LookRotation a secas y el "
             + "NPC anda mirando bien, asi que sumarle 180 aqui es lo que le "
             + "daba la vuelta justo al ponerse a jugar.")]
    public float modelYawOffset = 0f;

    [Tooltip("Cuanto se aleja mientras decide que hacer. Es el radio del paseo, "
             + "no una distancia recorrida: da vueltas por ahi dentro.")]
    public float wanderRadius = 2f;

    [Header("Visita")]
    public int extraMachineChanceOneIn = 3;
    public int maxMachinesPerVisit = 3;
    public float prizeVanishDelay = 0.6f;
    public float prizeArrivalTimeout = 4f;
    public float maxWaitInQueue = 90f;
    public float turnDelayMin = 1f;
    public float turnDelayMax = 2f;
    public float stepAsideDistance = 1.3f;
    public float moveTimeoutExtra = 6f;
    public bool despawnWhenLeaving = true;

    [Header("Movimiento")]
    public float gravity = -9.81f;
    public float agentRadius = 0.35f;
    public float agentHeight = 2f;
    public float navSampleDistance = 5f;
    public float pathTimeoutFactor = 3f;
    [Tooltip("Cuanto se desvia cada NPC de la ruta comun, en metros.")]
    public float routeSpread = 0.9f;

    [Header("Local sin nada que jugar")]
    [Tooltip("Si no hay maquina, entran igual, echan un vistazo y se van.")]
    public bool browseWhenEmpty = true;
    [Tooltip("Cuantas paradas dan antes de marcharse.")]
    public int browseStops = 2;
    [Tooltip("Como de adentro se meten, en metros.")]
    public float browseDepth = 5f;
    [Tooltip("Radio en el que buscan cada parada.")]
    public float browseRadius = 4f;
    [Tooltip("Lo que se paran a mirar en cada parada.")]
    public float browseLookSeconds = 1.2f;

    [Header("Seguridad")]
    public float maxLifetime = 240f;

    [Header("Quejas")]
    public Vector3 complaintOffset = new Vector3(0f, 2.1f, 0f);
    public Color complaintColor = new Color(1f, 0.35f, 0.3f);
    public float complaintSeconds = 2f;

    [HideInInspector] public Vector3 spawnPosition;
    [HideInInspector] public Transform entranceWaypoint;
    [HideInInspector] public List<Transform> entryPath = new List<Transform>();

    public static readonly List<NPCClawPlayer> Active = new List<NPCClawPlayer>();
    public static int ActiveCount { get { return Active.Count; } }

    private bool leaving = false;

    private bool spawnPositionSet = false;
    private ClawController reservedMachine;
    private CharacterController controller;
    private NavMeshAgent agent;
    private Vector3 lastAgentDestination = Vector3.positiveInfinity;
    private float verticalVelocity = 0f;
    private int lastStepFrame = -1;
    private float bornAt;
    private bool lastMoveReached = false;
    private float entranceSpread = 0f;
    private Vector3 routeOffset = Vector3.zero;
    private bool lastVisitUnreachable = false;
    private bool lastVisitRefused = false;
    private readonly List<ClawController> refused = new List<ClawController>();
    private readonly List<ClawController> unreachable = new List<ClawController>();

    private bool UsingAgent { get { return agent != null && agent.enabled && agent.isOnNavMesh; } }

    public void ConfigureVisit(Vector3 origin, Transform entrance, List<Transform> path = null)
    {
        spawnPosition = origin;
        spawnPositionSet = true;
        entranceWaypoint = entrance;

        entryPath.Clear();

        if (path != null) entryPath.AddRange(path);
    }

    void Awake()
    {
        Active.Add(this);
        bornAt = Time.time;
        controller = GetComponent<CharacterController>();

        SetUpAgent();
    }

    // El agente es lo que hace que rodeen paredes en vez de empotrarse. Si no
    // hay NavMesh horneado nos quedamos con el CharacterController de siempre.
    void SetUpAgent()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = moveSpeed;
        agent.acceleration = 12f;
        agent.angularSpeed = 360f;
        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.stoppingDistance = arriveDistance;
        agent.autoBraking = true;
        agent.updateRotation = false;

        // Con todos a la misma prioridad la evitacion es simetrica: ninguno cede
        // y se quedan bailando. Con prioridades distintas uno aparta y el otro
        // sigue, que es lo que hace la gente al cruzarse.
        agent.avoidancePriority = Random.Range(20, 80);
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;

        agent.enabled = false;

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(transform.position, out hit, navSampleDistance, NavMesh.AllAreas))
        {
            return;
        }

        if (controller != null) controller.enabled = false;

        agent.enabled = true;
        agent.Warp(hit.position);
    }

    void OnDestroy()
    {
        Active.Remove(this);

        ReleaseMachine();
    }

    void ReleaseMachine()
    {
        if (reservedMachine == null) return;

        if (reservedMachine.activeCarrySpot == carryPoint) reservedMachine.activeCarrySpot = null;

        reservedMachine.LeaveQueue(this);
        reservedMachine = null;
    }

    // Cierre del local: sueltan lo que estuvieran haciendo y se van por donde vinieron.
    public static void SendEveryoneHome()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i] != null) Active[i].GoHomeNow();
        }
    }

    public static void DespawnEveryone()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i] != null) Destroy(Active[i].gameObject);
        }
    }

    public void GoHomeNow()
    {
        if (leaving) return;

        leaving = true;

        StopAllCoroutines();
        ReleaseMachine();
        ClearPrize();

        StartCoroutine(LeaveRoutine());
    }

    IEnumerator LeaveRoutine()
    {
        if (entranceWaypoint != null)
        {
            yield return MoveTo(entranceWaypoint.position);
        }

        yield return WalkPath(true);
        yield return MoveTo(spawnPosition);

        SetWalking(false);
        Destroy(gameObject);
    }

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (!spawnPositionSet) spawnPosition = transform.position;

        StartCoroutine(Routine());
    }

    // Cada uno cruza la puerta por un punto ligeramente distinto: si todos van
    // al mismo sitio exacto se estorban entre ellos y se quedan bloqueados.
    Vector3 EntrancePoint()
    {
        if (entranceWaypoint == null) return transform.position;

        return entranceWaypoint.position + entranceWaypoint.right * entranceSpread;
    }

    string RefusalLine(MachinePricing pricing)
    {
        string[] lines =
        {
            "¿" + pricing.price + "€? Ni loco",
            "Que caro...",
            "Al lado esta a " + pricing.competitionPrice + "€",
            "Paso, es un robo"
        };

        return lines[Random.Range(0, lines.Length)];
    }

    // Desvio propio de este NPC. Aplicado a los puntos de paso hace que cada uno
    // trace su propia ruta en vez de ir todos en fila por la misma linea.
    Vector3 Detour(Vector3 point)
    {
        return point + routeOffset;
    }

    IEnumerator Routine()
    {
        entranceSpread = Random.Range(-0.6f, 0.6f);
        routeOffset = new Vector3(Random.Range(-routeSpread, routeSpread), 0f, Random.Range(-routeSpread, routeSpread));

        yield return WalkPath(false);

        if (entranceWaypoint != null)
        {
            yield return MoveTo(EntrancePoint());
        }

        ClawController lastPlayed = null;
        int played = 0;

        while (played < maxMachinesPerVisit)
        {
            ClawController machine = PickMachine(lastPlayed);
            if (machine == null) break;

            yield return PlayAtMachine(machine);

            // No se pudo llegar: se prueba otra sin gastar la visita. La lista de
            // inalcanzables crece, asi que el bucle siempre termina.
            // Ni por no llegar ni por precio se gasta la visita: prueban otra.
            if (lastVisitUnreachable || lastVisitRefused)
            {
                lastPlayed = null;
                continue;
            }

            lastPlayed = machine;
            played++;

            if (CountUsableMachines() < 2) break;
            if (Random.Range(0, Mathf.Max(1, extraMachineChanceOneIn)) != 0) break;
        }

        // Local vacio, o todas las maquinas caras o inalcanzables: en vez de
        // dar media vuelta en la puerta, entran, echan un vistazo y se van.
        if (played == 0 && browseWhenEmpty) yield return BrowseAround();

        // Aqui es donde decide si repite maquina o se va. Paseando.
        yield return EsperarPaseando(waitAfterPlaying, wanderRadius);

        if (entranceWaypoint != null)
        {
            yield return MoveTo(entranceWaypoint.position);
        }

        yield return WalkPath(true);

        yield return MoveTo(spawnPosition);
        SetWalking(false);

        ClearPrize();

        if (despawnWhenLeaving) Destroy(gameObject);
    }

    // Una vuelta corta por el local cuando no hay nada que jugar. No es relleno:
    // sin esto el cliente frena en el umbral y se da la vuelta, que desde fuera
    // parece que el juego se ha atascado en vez de que la tienda esta vacia.
    IEnumerator BrowseAround()
    {
        Vector3 center = BrowseCenter();

        for (int stop = 0; stop < browseStops; stop++)
        {
            Vector3 target;

            if (!PointNear(center, browseRadius, out target)) break;

            yield return MoveTo(target);

            SetWalking(false);

            yield return new WaitForSeconds(browseLookSeconds);
        }
    }

    // El centro de la vuelta se mete hacia dentro siguiendo la direccion por la
    // que entraron, para que no se queden mirando la puerta.
    Vector3 BrowseCenter()
    {
        Vector3 door = entranceWaypoint != null ? entranceWaypoint.position : transform.position;

        Vector3 inward = door - spawnPosition;
        inward.y = 0f;

        if (inward.sqrMagnitude < 0.01f) inward = transform.forward;

        return door + inward.normalized * browseDepth;
    }

    bool PointNear(Vector3 center, float radius, out Vector3 point)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(circle.x, 0f, circle.y);

            NavMeshHit hit;

            if (NavMesh.SamplePosition(candidate, out hit, navSampleDistance, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }

        point = center;
        return false;
    }

    // Waypoints opcionales entre el punto de aparicion y la puerta.
    IEnumerator WalkPath(bool reversed)
    {
        if (entryPath.Count == 0) yield break;

        for (int i = 0; i < entryPath.Count; i++)
        {
            Transform waypoint = entryPath[reversed ? entryPath.Count - 1 - i : i];
            if (waypoint == null) continue;

            yield return MoveTo(Detour(waypoint.position));
        }
    }

    int CountUsableMachines()
    {
        int count = 0;

        foreach (ClawController machine in ClawController.AllMachines)
        {
            if (machine != null && machine.npcSpot != null) count++;
        }

        return count;
    }

    ClawController PickMachine(ClawController avoid)
    {
        List<ClawController> candidates = new List<ClawController>();

        foreach (ClawController machine in ClawController.AllMachines)
        {
            if (machine == null || machine.npcSpot == null) continue;
            if (machine == avoid) continue;
            if (unreachable.Contains(machine)) continue;
            if (refused.Contains(machine)) continue;

            candidates.Add(machine);
        }

        if (candidates.Count == 0)
        {
            if (avoid != null && avoid.npcSpot != null) return null;

            foreach (ClawController machine in ClawController.AllMachines)
            {
                if (machine == null || machine.npcSpot == null) continue;
                if (unreachable.Contains(machine)) continue;
                if (refused.Contains(machine)) continue;

                candidates.Add(machine);
            }
        }

        if (candidates.Count == 0) return null;

        // Prefer the shortest queue, breaking ties at random.
        int shortest = int.MaxValue;
        List<ClawController> best = new List<ClawController>();

        foreach (ClawController machine in candidates)
        {
            int queue = machine.NPCQueueCount;

            if (queue < shortest)
            {
                shortest = queue;
                best.Clear();
                best.Add(machine);
            }
            else if (queue == shortest)
            {
                best.Add(machine);
            }
        }

        return best[Random.Range(0, best.Count)];
    }

    IEnumerator PlayAtMachine(ClawController machine)
    {
        clawController = machine;
        machineSpot = machine.npcSpot;

        lastVisitUnreachable = false;
        lastVisitRefused = false;

        // Si esta cara se va a buscar otra, y lo dice en alto.
        MachinePricing pricing = MachinePricing.For(machine);

        if (pricing != null && Random.value > pricing.AcceptanceChance)
        {
            FloatingText.Show(RefusalLine(pricing), transform, complaintOffset, complaintColor, complaintSeconds);

            if (DayCycleManager.Instance != null) DayCycleManager.Instance.ReportUnhappyCustomer();

            refused.Add(machine);
            lastVisitRefused = true;

            yield break;
        }

        machine.JoinQueue(this);
        reservedMachine = machine;

        // Siempre nos ponemos primero delante de la maquina, nunca la abordamos de lado.
        yield return MoveTo(machine.GetApproachPosition());

        // Si no hay camino hasta ella, no nos quedamos plantados a medias.
        if (!lastMoveReached)
        {
            Debug.LogWarning("[NPC] No hay camino hasta \"" + machine.name + "\". " +
                             "Revisa que el NavMesh llegue hasta delante de la maquina.", this);

            machine.LeaveQueue(this);
            reservedMachine = null;
            unreachable.Add(machine);
            lastVisitUnreachable = true;

            yield break;
        }

        float waitStarted = Time.time;
        bool queued = false;

        while (!machine.IsTurnOf(this))
        {
            queued = true;

            if (Time.time - waitStarted > maxWaitInQueue)
            {
                machine.LeaveQueue(this);
                reservedMachine = null;
                yield break;
            }

            Vector3 waitSpot = machine.GetWaitPosition(this);

            if (FlatDistance(transform.position, waitSpot) > arriveDistance)
            {
                yield return MoveStep(waitSpot);
            }
            else
            {
                SetWalking(false);
                yield return null;
            }
        }

        // Dejamos que el de delante se aparte antes de avanzar.
        if (queued)
        {
            // Sin alejarse: es su turno y no puede perder el sitio, pero
            // moverse un poco es lo que hace cualquiera esperando en una cola.
            yield return EsperarPaseando(Random.Range(turnDelayMin, turnDelayMax),
                                         wanderRadius * 0.35f);
        }

        machine.currentNPCUser = this;

        yield return MoveTo(machine.GetPlayPosition());
        SetWalking(false);
        yield return FaceMachine(machine);

        yield return new WaitForSeconds(waitBeforePlaying);

        bool keepTrying = true;

        while (keepTrying)
        {
            if (machine == null) break;

            // Pagan lo que marque la maquina, no un precio fijo del NPC.
            GameManager.Instance.AddMoney(pricing != null ? pricing.price : cost);

            // Cada partida da un pellizco de experiencia a la tienda.
            LevelManager manager = LevelManager.EnsureExists();
            if (manager != null) manager.Add(manager.xpNpcPlays);

            machine.activeCarrySpot = carryPoint;

            bool playFinished = false;
            machine.PlayAutomatically(() => playFinished = true);

            yield return new WaitUntil(() => playFinished);

            // El peluche puede tardar en caer dentro de la zona de premio, asi que
            // seguimos esperando (con el carry spot todavia asignado) antes de decidir.
            float waited = 0f;
            bool won = false;

            while (waited < prizeArrivalTimeout)
            {
                if (carryPoint != null && carryPoint.childCount > 0)
                {
                    won = true;
                    break;
                }

                waited += Time.deltaTime;
                yield return null;
            }

            machine.activeCarrySpot = null;

            if (won)
            {
                keepTrying = false;

                yield return new WaitForSeconds(prizeVanishDelay);
                ClearPrize();
            }
            else
            {
                keepTrying = Random.value <= retryChance;

                if (keepTrying)
                {
                    yield return new WaitForSeconds(waitBetweenRetries);
                }
            }
        }

        if (machine != null)
        {
            // Liberamos la maquina ya, y nos apartamos a un lado mientras el
            // siguiente de la cola cuenta su segundo o dos de cortesia.
            ReleaseMachine();

            Vector3 front = machine.NPCFrontDirection;
            Vector3 side = Vector3.Cross(Vector3.up, front).normalized;

            yield return MoveTo(machine.GetApproachPosition() + side * stepAsideDistance);
            SetWalking(false);
        }

        reservedMachine = null;
    }

    void ClearPrize()
    {
        if (carryPoint == null) return;

        for (int i = carryPoint.childCount - 1; i >= 0; i--)
        {
            Destroy(carryPoint.GetChild(i).gameObject);
        }
    }

    float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Un paso de un frame hacia el destino. Devuelve true cuando ya ha llegado.
    // Si hay CharacterController el movimiento pasa por el, asi que las paredes
    // y las maquinas lo frenan de verdad en vez de atravesarlas.
    bool StepTowards(Vector3 destination)
    {
        Vector3 toTarget = destination - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        bool arrived = distance <= arriveDistance;

        Vector3 horizontal = Vector3.zero;

        if (!arrived)
        {
            Vector3 direction = toTarget / distance;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            float step = Mathf.Min(moveSpeed * Time.deltaTime, distance);
            horizontal = direction * step;
        }

        if (controller != null && controller.enabled)
        {
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;

            verticalVelocity += gravity * Time.deltaTime;

            controller.Move(horizontal + Vector3.up * verticalVelocity * Time.deltaTime);
            lastStepFrame = Time.frameCount;
        }
        else
        {
            transform.position += horizontal;
        }

        return arrived;
    }

    // Mientras espera en la cola o juega no llamamos a StepTowards, asi que la
    // gravedad la aplicamos aqui para que no se quede flotando.
    void LateUpdate()
    {
        // Por si alguno se queda atascado contra una pared: nadie se queda
        // dando vueltas por el local para siempre.
        if (despawnWhenLeaving && maxLifetime > 0f && Time.time - bornAt > maxLifetime)
        {
            ClearPrize();
            Destroy(gameObject);
            return;
        }

        if (UsingAgent) return;
        if (controller == null || !controller.enabled) return;
        if (lastStepFrame == Time.frameCount) return;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    Vector3 SnapToNavMesh(Vector3 destination)
    {
        NavMeshHit hit;

        if (NavMesh.SamplePosition(destination, out hit, navSampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return destination;
    }

    void SetAgentDestination(Vector3 destination)
    {
        if (!UsingAgent) return;

        // Reemitir el destino cada frame es caro y ademas reinicia el path.
        if (FlatDistance(lastAgentDestination, destination) < 0.2f) return;

        lastAgentDestination = destination;
        agent.stoppingDistance = arriveDistance;
        agent.isStopped = false;
        agent.SetDestination(SnapToNavMesh(destination));
    }

    void FaceAgentVelocity()
    {
        if (!UsingAgent) return;

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    bool AgentArrived()
    {
        if (!UsingAgent) return true;
        if (agent.pathPending) return false;

        return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, arriveDistance);
    }

    // Espera CAMINANDO en vez de quedarse tieso.
    //
    // Una persona que esta decidiendo que hacer no se queda clavada mirando al
    // vacio: da un par de pasos, se acerca a mirar otra cosa, cambia de idea.
    // Plantarlos inmoviles unos segundos es lo que los delata como maniquies,
    // y encima justo en el momento en el que el jugador los esta mirando.
    IEnumerator EsperarPaseando(float segundos, float radio)
    {
        float fin = Time.time + segundos;
        Vector3 centro = transform.position;

        while (Time.time < fin)
        {
            Vector3 destino;

            // Sin sitio donde ir (un rincon, o el NavMesh no llega) se espera
            // quieto, que es mejor que empujar contra una pared.
            if (!PointNear(centro, radio, out destino))
            {
                SetWalking(false);
                yield return null;
                continue;
            }

            while (Time.time < fin && FlatDistance(transform.position, destino) > arriveDistance)
            {
                yield return MoveStep(destino);
            }
        }

        StopAgent();
        SetWalking(false);
    }

    IEnumerator MoveTo(Vector3 destination)
    {
        SetWalking(true);

        // Si algo bloquea el camino no queremos que el NPC se quede colgado para siempre.
        float speed = Mathf.Max(0.01f, moveSpeed);
        float straightLine = FlatDistance(transform.position, destination);

        if (UsingAgent)
        {
            // El camino real rodea obstaculos, asi que damos margen de sobra.
            float deadline = Time.time + (straightLine / speed) * pathTimeoutFactor + moveTimeoutExtra;

            lastAgentDestination = Vector3.positiveInfinity;
            SetAgentDestination(destination);

            lastMoveReached = false;

            while (true)
            {
                if (AgentArrived())
                {
                    lastMoveReached = true;
                    break;
                }

                if (Time.time > deadline) break;

                // Camino incompleto: no hay NavMesh que conecte con el destino.
                // Sin esto se quedan plantados donde se corta, tipicamente en la
                // puerta, creyendo que estan yendo a algun sitio.
                if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete) break;

                FaceAgentVelocity();
                yield return null;
            }

            StopAgent();
            yield break;
        }

        float fallbackDeadline = Time.time + (straightLine / speed) + moveTimeoutExtra;
        lastMoveReached = false;

        while (!StepTowards(destination))
        {
            if (Time.time > fallbackDeadline) yield break;

            yield return null;
        }

        lastMoveReached = true;
    }

    void StopAgent()
    {
        if (!UsingAgent) return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        lastAgentDestination = Vector3.positiveInfinity;
    }

    IEnumerator MoveStep(Vector3 destination)
    {
        SetWalking(true);

        if (UsingAgent)
        {
            SetAgentDestination(destination);
            FaceAgentVelocity();
        }
        else
        {
            StepTowards(destination);
        }

        yield return null;
    }

    IEnumerator FaceMachine(ClawController machine)
    {
        Transform spot = machine != null ? machine.npcSpot : machineSpot;
        if (spot == null) yield break;

        Vector3 lookTarget = spot.parent != null ? spot.parent.position : spot.position;
        Vector3 direction = lookTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(0f, modelYawOffset, 0f);

        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    void SetWalking(bool walking)
    {
        if (animator != null)
        {
            animator.speed = walking ? 1f : 0f;
        }
    }
}
