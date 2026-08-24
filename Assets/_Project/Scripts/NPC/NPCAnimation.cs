using UnityEngine;
using System.Collections.Generic;

// Mueve las piernas del NPC al ritmo al que camina de verdad.
//
// Vale para las dos formas de animar de Unity, porque el modelo puede venir
// importado de cualquiera de las dos:
//
//  - Legacy (componente Animation): el clip viene dentro del propio modelo, no
//    hace falta referenciar nada. Es lo que usa Walking.fbx.
//  - Mecanim (componente Animator): necesita un controlador, y se le pasa la
//    velocidad por el parametro Speed.
//
// Se prueban en ese orden y se usa el que haya. Asi, si mas adelante pasas el
// modelo a Humanoid para meter clips de Mixamo, este script sigue valiendo sin
// tocar nada.
public class NPCAnimation : MonoBehaviour
{
    [Tooltip("Solo para Mecanim: controlador con el estado de andar.")]
    public RuntimeAnimatorController controller;

    [Tooltip("Clips candidatos de caminar. Se usa el mas largo que exista.")]
    public AnimationClip[] walkClips;

    [Tooltip("A que velocidad la animacion va a ritmo normal, en m/s.")]
    public float nominalSpeed = 1.4f;

    [Tooltip("Lo suave que arranca y para la animacion.")]
    public float smoothing = 8f;

    [Tooltip("Por debajo de esto se considera parado.")]
    public float stopThreshold = 0.08f;

    [Tooltip("Variacion de ritmo entre NPC, para que no anden todos igual.")]
    [Range(0f, 0.4f)] public float paceVariation = 0.12f;

    [Tooltip("Anula el avance del clip para que camine en el sitio.")]
    public bool andarEnElSitio = true;

    [Tooltip("Escribe en consola que encuentra y a que ritmo va. Solo para depurar.")]
    public bool diagnostico = false;

    private Transform cadera;
    private Vector3 caderaEnReposo;
    private Transform[] cadenaRaiz;
    private Vector3[] cadenaEnReposo;
    private float derivaMedida = 0f;
    private float saltoMayor = 0f;
    private float retrocesoMayor = 0f;
    private float frameMasLargo = 0f;
    private float avanceMin = 999f;
    private float avanceMax = -999f;
    private float alturaMin = 999f;
    private float alturaMax = -999f;
    private Transform modelo;
    private Vector3 referencia;
    private bool referenciaTomada = false;

    // Solo uno escribe: seis NPC soltando un mensaje por segundo, con la consola
    // del editor redibujando cada uno, ya es bastante carga como para provocar
    // justo el tiron que estamos buscando.
    private static NPCAnimation elQueInforma;

    private bool diagnosticoHecho = false;
    private float diagnosticoSiguiente = 0f;

    private Animator animator;
    private Animation legacy;
    private string legacyClip;

    private Vector3 lastPosition;
    private float speedValue = 0f;
    private float pace = 1f;

    void Awake()
    {
        lastPosition = transform.position;

        pace = 1f + Random.Range(-paceVariation, paceVariation);

        BuscarCadera();

        SetUpLegacy();

        if (legacy == null) SetUpMecanim();
    }

    void OnDestroy()
    {
        // Si se va el que informaba, que el siguiente tome el relevo.
        if (elQueInforma == this) elQueInforma = null;
    }

    void SetUpLegacy()
    {
        legacy = GetComponentInChildren<Animation>();

        if (legacy == null) return;

        // El clip lo trae el modelo: se coge el primero que haya y se deja en
        // bucle. Nada de nombres a mano ni referencias que se pierdan.
        foreach (AnimationState state in legacy)
        {
            state.wrapMode = WrapMode.Loop;

            if (string.IsNullOrEmpty(legacyClip)) legacyClip = state.name;
        }

        legacy.wrapMode = WrapMode.Loop;

        if (string.IsNullOrEmpty(legacyClip))
        {
            legacy = null;
            return;
        }

        legacy.Play(legacyClip);

        // Cada uno arranca en un punto distinto del paso: si no, los de la cola
        // mueven las piernas a la vez y cantan mucho.
        legacy[legacyClip].normalizedTime = Random.value;
    }

    void SetUpMecanim()
    {
        animator = GetComponentInChildren<Animator>();

        // Si el modelo viene importado sin Avatar, Unity no le pone Animator y
        // no hay nada que reproduzca el clip. Se le pone aqui, sobre el objeto
        // del modelo, que es desde donde cuelgan las rutas de los huesos.
        if (animator == null) animator = BuildAnimator();

        if (animator == null) return;

        // Solo si trae clips de verdad. Un controlador vacio es peor que no
        // tener ninguno: con Write Defaults activo el Animator resetea todos
        // los huesos a la pose de reposo, y el modelo se queda en T y hundido,
        // porque en los rigs de Mixamo las caderas estan en el origen y es la
        // animacion la que los levanta a su altura.
        if (animator.runtimeAnimatorController == null && HasClips(controller))
        {
            animator.runtimeAnimatorController = controller;
        }

        if (animator.runtimeAnimatorController != null && !HasClips(animator.runtimeAnimatorController))
        {
            animator.runtimeAnimatorController = null;
        }

        // El agente ya mueve al NPC: si ademas lo moviera la animacion, el
        // cuerpo se iria por su cuenta y patinaria respecto al suelo.
        animator.applyRootMotion = false;

        // Animar siempre. Con el modo de recorte, Unity decide si evaluar la
        // animacion segun si los renderers estan a la vista, y en un modelo
        // importado esos limites pueden venir mal calculados: el NPC se queda
        // sin animar aunque lo tengas delante. Para un punado de clientes el
        // ahorro no compensa el riesgo.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        SwapInWalkClip();

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[NPCAnimation] El NPC se queda quieto: no hay animacion. Revisa que " +
                             "Walking.fbx este importado con Animation Type en Legacy, o asigna un " +
                             "controlador que tenga el clip de andar.", this);
        }
    }

    // El controlador trae un clip vacio de relleno, y aqui se cambia por el de
    // caminar de verdad. Se hace asi porque los clips viven dentro del .fbx y su
    // identificador interno no se puede saber de antemano: si una referencia no
    // resuelve, Unity la deja en nulo y se prueba la siguiente. Con el clip
    // metido directamente en el controlador, fallar la referencia dejaba al NPC
    // sin animacion y sin manera de arreglarlo desde el juego.
    void SwapInWalkClip()
    {
        if (animator.runtimeAnimatorController == null) return;

        AnimationClip walk = LongestWalkClip();

        if (walk == null)
        {
            Debug.LogWarning("[NPCAnimation] Ninguna referencia de clip resuelve. Arrastra a mano el " +
                             "clip de andar del Walking.fbx al campo Walk Clips de este componente.", this);
            return;
        }

        AnimationClip placeholder = FirstControllerClip();

        if (placeholder == null || placeholder == walk) return;

        AnimatorOverrideController swapped = new AnimatorOverrideController(animator.runtimeAnimatorController);

        swapped[placeholder] = walk;

        animator.runtimeAnimatorController = swapped;
    }

    // El mas largo es el ciclo de caminar. Los otros sub-assets del modelo que
    // se cuelan como candidatos salen nulos o con duracion cero.
    AnimationClip LongestWalkClip()
    {
        AnimationClip best = null;

        if (walkClips == null) return null;

        foreach (AnimationClip clip in walkClips)
        {
            if (clip == null || clip.length <= 0.01f) continue;

            if (best == null || clip.length > best.length) best = clip;
        }

        return best;
    }

    // Los clips de Mixamo llevan el avance metido en la cadera: el personaje
    // camina hacia delante de verdad y al repetir el ciclo vuelve al origen de
    // un tiron. Se le puede quitar en la importacion, pero solo si el modelo
    // trae configurado su nodo raiz, y en un rig generico eso viene vacio.
    //
    // Aqui se ataja sin depender de nada: cada frame, despues de que el Animator
    // escriba los huesos, se le devuelve a la cadera su posicion horizontal de
    // reposo. Se conserva la altura, que es el balanceo del paso; solo se anula
    // el desplazamiento. Quien mueve al NPC es el NavMeshAgent, no el clip.
    void BuscarCadera()
    {
        SkinnedMeshRenderer piel = GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (piel != null && piel.rootBone != null) cadera = piel.rootBone;

        if (cadera == null)
        {
            foreach (Transform hueso in GetComponentsInChildren<Transform>(true))
            {
                if (hueso.name.IndexOf("hips", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cadera = hueso;
                    break;
                }
            }
        }

        if (cadera == null) return;

        caderaEnReposo = cadera.localPosition;

        // No basta con la cadera. Este clip viene de Blender, y se nota en su
        // nombre: "Armature|Armature|mixamo.com|Layer0". En esos exports el
        // avance suele ir en el nodo Armature, un escalon por encima de la
        // cadera, asi que bloquearla a ella sola no sirve de nada.
        //
        // Se recoge toda la cadena desde la cadera hasta la raiz del modelo y se
        // bloquean todas. Son nodos de montaje que no deben moverse en horizontal
        // por su cuenta, asi que fijarlos no deforma la pose.
        List<Transform> cadena = new List<Transform>();
        List<Vector3> reposo = new List<Vector3>();

        Transform nodo = cadera;

        while (nodo != null && nodo != transform)
        {
            cadena.Add(nodo);
            reposo.Add(nodo.localPosition);

            nodo = nodo.parent;
        }

        cadenaRaiz = cadena.ToArray();
        cadenaEnReposo = reposo.ToArray();

        // La raiz del modelo: el hijo directo del NPC del que cuelga todo.
        if (cadena.Count > 0) modelo = cadena[cadena.Count - 1];
    }

    // En LateUpdate: el Animator escribe los huesos durante Update, asi que
    // corregirlo antes no serviria de nada.
    void LateUpdate()
    {
        CompensarAvance();
    }

    // Deja al personaje caminando en el sitio, venga el avance de donde venga.
    //
    // Antes fijaba la posicion local de la cadera y de sus padres, pero eso solo
    // funciona si aciertas en que nodo esta metido el movimiento. Medido, el
    // clip mueve la cadera 1,87 m por ciclo y seguia colandose.
    //
    // Ahora no se adivina nada: se mide donde ha acabado la cadera respecto al
    // NPC, se compara con donde deberia estar, y ese desvio se le resta al
    // modelo entero. Da igual que el avance venga de la cadera, del Armature o
    // de la raiz: el resultado es el mismo y la pose no se toca, solo se
    // desplaza el conjunto. La altura se respeta, que es el balanceo del paso.
    void CompensarAvance()
    {
        if (!andarEnElSitio || cadera == null || modelo == null) return;

        Vector3 relativo = transform.InverseTransformPoint(cadera.position);

        // Donde acaba la cadera ya compensada: si el arreglo funciona, este
        // margen tiene que ser de centimetros, no de metros.
        if (relativo.z < avanceMin) avanceMin = relativo.z;
        if (relativo.z > avanceMax) avanceMax = relativo.z;
        if (relativo.y < alturaMin) alturaMin = relativo.y;
        if (relativo.y > alturaMax) alturaMax = relativo.y;

        if (!referenciaTomada)
        {
            referencia = relativo;
            referenciaTomada = true;
            return;
        }

        Vector3 desvio = relativo - referencia;
        desvio.y = 0f;

        derivaMedida = desvio.magnitude;

        if (derivaMedida < 0.0001f) return;

        modelo.localPosition -= modelo.parent.InverseTransformVector(transform.TransformVector(desvio));
    }

    // El Animator tiene que ir en la raiz del modelo importado, no en la del
    // NPC: los clips guardan las rutas de los huesos relativas a ella, y desde
    // un sitio mas alto no encontrarian ningun hueso que mover.
    Animator BuildAnimator()
    {
        Renderer sample = GetComponentInChildren<Renderer>(true);

        if (sample == null) return null;

        Transform model = sample.transform;

        while (model.parent != null && model.parent != transform) model = model.parent;

        if (model == transform) return null;

        return model.gameObject.AddComponent<Animator>();
    }

    AnimationClip FirstControllerClip()
    {
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        return clips != null && clips.Length > 0 ? clips[0] : null;
    }

    static bool HasClips(RuntimeAnimatorController candidate)
    {
        return candidate != null && candidate.animationClips != null && candidate.animationClips.Length > 0;
    }

    void Update()
    {
        if (elQueInforma == null) elQueInforma = this;

        if (Time.deltaTime > frameMasLargo) frameMasLargo = Time.deltaTime;

        float target = CurrentSpeed() / Mathf.Max(0.1f, nominalSpeed);

        if (target < stopThreshold) target = 0f;

        speedValue = Mathf.Lerp(speedValue, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        if (legacy != null && !string.IsNullOrEmpty(legacyClip))
        {
            // Parado el clip se congela; andando va al ritmo de sus piernas.
            legacy[legacyClip].speed = speedValue * pace;
        }
        else if (animator != null)
        {
            animator.SetFloat("Speed", speedValue * pace);
        }

        Diagnostico();
    }

    // Solo el primer NPC escribe, y una vez por segundo: lo justo para ver que
    // encontro y a que ritmo cree que va, sin volver a llenar el log.
    void Diagnostico()
    {
        if (!diagnostico || elQueInforma != this) return;

        if (!diagnosticoHecho)
        {
            diagnosticoHecho = true;

            string ctrl = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name : "ninguno";

            AnimationClip walk = LongestWalkClip();

            Debug.Log("[NPCAnimation] animator=" + (animator != null ? animator.name : "NULO") +
                      " | legacy=" + (legacy != null ? legacyClip : "no") +
                      " | controlador=" + ctrl +
                      " | clipAndar=" + (walk != null ? walk.name + " (" + walk.length.ToString("0.00") + "s)" : "NINGUNO") +
                      " | applyRootMotion=" + (animator != null && animator.applyRootMotion) +
                      " | cadera=" + (cadera != null ? cadera.name : "NO ENCONTRADA"), this);
        }

        if (Time.time < diagnosticoSiguiente) return;

        diagnosticoSiguiente = Time.time + 1f;

        if (animator == null) return;

        Debug.Log("[NPCAnimation] medido=" + CurrentSpeedPeek().ToString("0.00") + " m/s" +
                  " | parametro Speed=" + animator.GetFloat("Speed").ToString("0.00") +
                  " | animator.speed=" + animator.speed.ToString("0.00") +
                  " | deriva=" + derivaMedida.ToString("0.000") +
                  " | SALTO MAYOR=" + saltoMayor.ToString("0.000") + "m" +
                  " | RETROCESO=" + retrocesoMayor.ToString("0.000") + "m" +
                  " | FRAME=" + (frameMasLargo * 1000f).ToString("0") + "ms" +
                  " | AVANCE cadera=" + (avanceMax - avanceMin).ToString("0.000") + "m" +
                  " | ALTURA cadera=" + (alturaMax - alturaMin).ToString("0.000") + "m" +
                  " | bloqueo=" + andarEnElSitio, this);

        frameMasLargo = 0f;
        avanceMin = 999f; avanceMax = -999f;
        alturaMin = 999f; alturaMax = -999f;

        saltoMayor = 0f;
        retrocesoMayor = 0f;
    }

    // Solo para el diagnostico: no consume la medida de Update.
    float CurrentSpeedPeek()
    {
        return speedValue * Mathf.Max(0.1f, nominalSpeed);
    }

    // Se mide cuanto se ha movido el NPC de verdad, no lo que diga nadie.
    //
    // Antes se le preguntaba al NavMeshAgent, y ahi estaba el fallo: el agente
    // se lo anade el propio NPC en su arranque, asi que cuando este script
    // despierta todavia no existe y quedaba guardado en nulo para siempre. Se
    // caia al CharacterController, que no es quien lo mueve y da cero. Con
    // velocidad cero el clip se queda congelado: el NPC se desliza sin animar,
    // que es exactamente lo que se veia.
    float CurrentSpeed()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        delta.y = 0f;

        if (Time.deltaTime <= 0f) return 0f;

        // Para el diagnostico: el mayor salto en un solo frame, y si fue hacia
        // atras. Un paso normal a 1,2 m/s son unos 2 cm por frame; cualquier
        // cosa mucho mayor no es caminar, es un teletransporte.
        float salto = delta.magnitude;

        if (salto > saltoMayor) saltoMayor = salto;

        if (salto > 0.001f && Vector3.Dot(delta.normalized, transform.forward) < -0.5f)
        {
            if (salto > retrocesoMayor) retrocesoMayor = salto;
        }

        float speed = salto / Time.deltaTime;

        // Un Warp del agente teletransporta al NPC y dispara la medida un frame.
        return Mathf.Min(speed, nominalSpeed * 3f);
    }
}
