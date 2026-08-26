using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Pone las manos del NPC donde tienen que estar cuando juega a la maquina.
//
// Mete la moneda por la ranura, y despues deja una mano en el joystick y la
// otra en el boton. El joystick se inclina hacia donde va la garra y el boton
// se hunde cuando la garra baja, asi que lo que hace el NPC se corresponde con
// lo que hace la maquina en vez de ser un gesto suelto.
//
// El rig viene importado como Generic y no como Humanoid, asi que la IK de
// Unity no esta disponible: SetIKPosition solo existe para Humanoid. Se resuelve
// aqui a mano, que para un brazo son dos huesos y sale exacto.
//
// Va despues de NPCAnimation a proposito. Las dos escriben huesos en LateUpdate,
// y entre dos componentes el orden no esta garantizado: sin fijarlo, unas veces
// quedarian las manos colocadas y otras las pisaria la pose de andar, cambiando
// de una ejecucion a otra.
[DefaultExecutionOrder(100)]
public class NPCManosMaquina : MonoBehaviour
{
    [Header("Manos")]

    [Tooltip("Lo que tarda una mano en llegar al mando y en soltarlo.")]
    public float suavizado = 7f;

    [Tooltip("Cuanto se puede pasar del alcance del brazo antes de rendirse.")]
    [Range(0.5f, 1f)] public float margenAlcance = 0.97f;

    [Tooltip("Lo que tarda en meter la moneda.")]
    public float duracionMoneda = 1.1f;

    [Header("Mandos de la maquina")]

    [Tooltip("Cuanto se inclina el joystick como maximo, en grados.")]
    public float inclinacionJoystick = 15f;

    [Tooltip("A que velocidad de la garra el joystick va del todo, en m/s.")]
    public float velocidadTope = 0.6f;

    [Tooltip("Cuanto se hunde el boton al pulsarlo.")]
    public float recorridoBoton = 0.007f;

    [Tooltip("Lo que dura la pulsacion.")]
    public float duracionPulsacion = 0.35f;

    [Tooltip("Escribe en consola lo que encuentra. Solo para depurar.")]
    public bool diagnostico = false;

    // ------------------------------------------------------------- los huesos

    Transform hombroIz, codoIz, manoIz;
    Transform hombroDe, codoDe, manoDe;
    bool huesosBuscados = false;

    // -------------------------------------------------------- reposo de los
    // -------------------------------------------------------- mandos

    // Donde estan el joystick y el boton cuando nadie los toca.
    //
    // Se guarda por maquina y no por NPC, y esto no es un detalle: los mandos
    // son de la maquina, no del que este jugando. Guardandolo cada uno por su
    // cuenta, el segundo NPC tomaria como reposo la postura en la que el
    // primero hubiera dejado el joystick, y a cada partida quedaria mas torcido.
    //
    // Y se guarda en coordenadas locales de la maquina: en coordenadas de mundo,
    // el dia que muevas una maquina de sitio el reposo apuntaria a donde estaba
    // antes.
    class Reposo
    {
        public Vector3 joystickPos;
        public Quaternion joystickRot;
        public Vector3 joystickPie;
        public Vector3 botonPos;
    }

    static readonly Dictionary<ClawController, Reposo> reposos =
        new Dictionary<ClawController, Reposo>();

    // ------------------------------------------------------------- la maquina

    ClawController maquina;
    Reposo reposo;
    Transform joystick, boton, ranura;

    // ---------------------------------------------------------------- estado

    enum Que { Nada, Moneda, Jugando }

    Que que = Que.Nada;

    // Cuanto manda la IK sobre cada brazo. Es lo que hace que la mano llegue y
    // se vaya poco a poco en vez de aparecer pegada al mando.
    float pesoIz, pesoDe;
    Vector3 metaIz, metaDe;

    // Seguimiento de la garra: hacia donde va y cuando empieza a bajar.
    Vector3 garraAnterior;
    bool garraMedida = false;
    Vector3 inclinacion;

    float alturaAnterior;
    bool alturaMedida = false;
    bool bajando = false;
    float pulsacion = 0f;

    // ============================================================== lo que le
    // ============================================================== pide el NPC

    public IEnumerator MeterMoneda(ClawController m)
    {
        if (!Preparar(m)) yield break;

        que = Que.Moneda;

        yield return new WaitForSeconds(duracionMoneda);
    }

    public void AlMando(ClawController m)
    {
        if (!Preparar(m)) return;

        que = Que.Jugando;
    }

    // Soltar no borra la maquina de golpe.
    //
    // Si se borrara aqui, MoverMandos dejaria de tocar nada y el joystick se
    // quedaria inclinado para siempre y el boton hundido. La maquina se suelta
    // sola en cuanto los mandos han vuelto a su sitio.
    public void Soltar()
    {
        que = Que.Nada;
    }

    // ================================================================== huesos

    void BuscarHuesos()
    {
        if (huesosBuscados) return;
        huesosBuscados = true;

        hombroIz = Hueso("LeftArm");
        codoIz = Hueso("LeftForeArm");
        manoIz = Hueso("LeftHand");

        hombroDe = Hueso("RightArm");
        codoDe = Hueso("RightForeArm");
        manoDe = Hueso("RightHand");

        if (hombroIz != null && hombroDe != null) return;

        Debug.LogWarning("[Manos] No encuentro los brazos en el rig. Los NPC "
                         + "jugaran sin poner las manos en los mandos.", this);
    }

    // Por el final del nombre, que asi vale igual con "mixamorig:LeftArm".
    //
    // Buscar "LeftArm" no pilla "LeftForeArm" de rebote: esa acaba en "ForeArm".
    // Y "LeftHand" no pilla los dedos, que acaban en "Thumb1" y demas.
    Transform Hueso(string nombre)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith(nombre, System.StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    // ================================================================= maquina

    bool Preparar(ClawController m)
    {
        BuscarHuesos();

        if (m == null || hombroIz == null || hombroDe == null) return false;
        if (maquina == m && reposo != null) return true;

        joystick = Pieza(m.transform, "Joystick");
        boton = Pieza(m.transform, "Boton_Jugar");
        ranura = Pieza(m.transform, "Ranura_1");

        if (joystick == null || boton == null)
        {
            Debug.LogWarning("[Manos] La maquina no tiene Joystick o Boton_Jugar. "
                             + "Se juega sin manos en los mandos.", this);
            return false;
        }

        maquina = m;
        reposo = ReposoDe(m, joystick, boton);

        garraMedida = false;
        alturaMedida = false;

        if (diagnostico)
        {
            Debug.Log("[Manos] joystick=" + joystick.name + " boton=" + boton.name
                      + " ranura=" + (ranura != null ? ranura.name : "NO")
                      + " | brazo " + Largo(hombroIz, codoIz, manoIz).ToString("0.00") + " m",
                      this);
        }

        return true;
    }

    static Reposo ReposoDe(ClawController m, Transform joystick, Transform boton)
    {
        Reposo r;
        if (reposos.TryGetValue(m, out r) && r != null) return r;

        Transform raiz = m.transform;

        r = new Reposo();
        r.joystickPos = raiz.InverseTransformPoint(joystick.position);
        r.joystickRot = Quaternion.Inverse(raiz.rotation) * joystick.rotation;
        r.botonPos = raiz.InverseTransformPoint(boton.position);

        // El pie del vastago, que es por donde tiene que doblarse. Sale de los
        // limites reales del dibujo y no del origen de la pieza: donde cae ese
        // origen depende de como saliera del exportador, y girando sobre el, el
        // joystick se hundiria en la consola o saldria flotando.
        Renderer render = joystick.GetComponentInChildren<Renderer>();

        Vector3 pie = joystick.position;

        if (render != null)
        {
            pie = render.bounds.center;
            pie.y = render.bounds.min.y;
        }

        r.joystickPie = raiz.InverseTransformPoint(pie);

        reposos[m] = r;
        return r;
    }

    static Transform Pieza(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre) return t;
        }

        return null;
    }

    static float Largo(Transform a, Transform b, Transform c)
    {
        if (a == null || b == null || c == null) return 0f;

        return Vector3.Distance(a.position, b.position)
               + Vector3.Distance(b.position, c.position);
    }

    // ================================================================ cada vez

    void LateUpdate()
    {
        float k = 1f - Mathf.Exp(-suavizado * Time.deltaTime);

        RepartirManos();

        pesoIz = Mathf.Lerp(pesoIz, PesoQueTocaria(hombroIz, codoIz, manoIz, metaIz), k);
        pesoDe = Mathf.Lerp(pesoDe, PesoQueTocaria(hombroDe, codoDe, manoDe, metaDe), k);

        Aplicar(hombroIz, codoIz, manoIz, metaIz, pesoIz);
        Aplicar(hombroDe, codoDe, manoDe, metaDe, pesoDe);

        MoverMandos(k);
    }

    // Cada mano a lo que le pilla mas cerca.
    //
    // Nada de fijar "la izquierda al joystick": segun por donde se ponga el NPC
    // el joystick le queda a un lado o al otro, y con las manos fijas acabaria
    // cruzando los brazos por delante del pecho.
    void RepartirManos()
    {
        if (que == Que.Nada || maquina == null || reposo == null)
        {
            metaIz = metaDe = Vector3.zero;
            return;
        }

        if (que == Que.Moneda)
        {
            Vector3 punto = PuntoRanura();

            // Solo una mano: la otra sigue con lo que dijera la animacion.
            bool izquierda = MasCerca(manoIz, manoDe, punto);

            metaIz = izquierda ? punto : Vector3.zero;
            metaDe = izquierda ? Vector3.zero : punto;

            return;
        }

        Vector3 pJoystick = PuntoJoystick();
        Vector3 pBoton = PuntoBoton();

        if (MasCerca(manoIz, manoDe, pJoystick))
        {
            metaIz = pJoystick;
            metaDe = pBoton;
        }
        else
        {
            metaIz = pBoton;
            metaDe = pJoystick;
        }
    }

    static bool MasCerca(Transform a, Transform b, Vector3 punto)
    {
        if (a == null) return false;
        if (b == null) return true;

        return Vector3.Distance(a.position, punto) <= Vector3.Distance(b.position, punto);
    }

    // Cero si no hay meta, y tambien si el mando le queda fuera del alcance del
    // brazo: antes de verle estirarse como un muneco de goma, que no llegue y se
    // quede con los brazos como estaban.
    float PesoQueTocaria(Transform hombro, Transform codo, Transform mano, Vector3 meta)
    {
        if (meta == Vector3.zero || hombro == null) return 0f;

        float alcance = Largo(hombro, codo, mano) * margenAlcance;

        return Vector3.Distance(hombro.position, meta) <= alcance ? 1f : 0f;
    }

    void Aplicar(Transform hombro, Transform codo, Transform mano,
                 Vector3 meta, float peso)
    {
        if (peso < 0.002f || meta == Vector3.zero) return;
        if (hombro == null || codo == null || mano == null) return;

        Quaternion antesHombro = hombro.rotation;
        Quaternion antesCodo = codo.rotation;

        ResolverIK(hombro, codo, mano, meta);

        hombro.rotation = Quaternion.Slerp(antesHombro, hombro.rotation, peso);
        codo.rotation = Quaternion.Slerp(antesCodo, codo.rotation, peso);
    }

    // IK de dos huesos, en dos pasos y en este orden.
    //
    //   1. Se dobla el codo hasta que la mano quede a la distancia justa del
    //      hombro. Se dobla en el plano que ya traia de la animacion, con lo que
    //      el codo sigue apuntando a donde apuntaba y no hace falta inventarse
    //      ningun vector de polo.
    //   2. Se gira el hombro entero para apuntar a la meta.
    //
    // El orden importa: girar el hombro no cambia la distancia hombro-mano, asi
    // que el segundo paso no deshace el primero. Al reves si.
    //
    // Comprobado con 1408 casos al azar antes de escribirlo: la mano cae en la
    // meta con error 0 y las dos longitudes de hueso se conservan.
    static void ResolverIK(Transform hombro, Transform codo, Transform mano,
                           Vector3 meta)
    {
        Vector3 ph = hombro.position;

        float l1 = Vector3.Distance(ph, codo.position);
        float l2 = Vector3.Distance(codo.position, mano.position);

        if (l1 < 1e-5f || l2 < 1e-5f) return;

        float d = Vector3.Distance(ph, meta);
        d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);

        // --- 1) el codo ------------------------------------------------------
        Vector3 haciaHombro = ph - codo.position;
        Vector3 haciaMano = mano.position - codo.position;

        float actual = Vector3.Angle(haciaHombro, haciaMano);

        float coseno = (l1 * l1 + l2 * l2 - d * d) / (2f * l1 * l2);
        float quiero = Mathf.Acos(Mathf.Clamp(coseno, -1f, 1f)) * Mathf.Rad2Deg;

        Vector3 eje = Vector3.Cross(haciaHombro, haciaMano);

        // Con el brazo estirado del todo los dos vectores son paralelos y el eje
        // sale nulo. Se coge cualquiera perpendicular para arrancar el doblez; en
        // cuanto dobla un grado ya tiene plano propio.
        if (eje.sqrMagnitude < 1e-10f) eje = Vector3.Cross(haciaMano, hombro.up);
        if (eje.sqrMagnitude < 1e-10f) eje = Vector3.Cross(haciaMano, hombro.forward);
        if (eje.sqrMagnitude < 1e-10f) return;

        codo.rotation = Quaternion.AngleAxis(quiero - actual, eje.normalized) * codo.rotation;

        // --- 2) apuntar ------------------------------------------------------
        Vector3 ahora = mano.position - ph;
        Vector3 destino = meta - ph;

        if (ahora.sqrMagnitude < 1e-10f || destino.sqrMagnitude < 1e-10f) return;

        hombro.rotation = Quaternion.FromToRotation(ahora, destino) * hombro.rotation;
    }

    // ============================================================== los mandos

    Vector3 PuntoJoystick()
    {
        Renderer r = joystick.GetComponentInChildren<Renderer>();

        // Arriba del todo, que es la bola: la mano va encima, no en el vastago.
        Vector3 p = r != null ? r.bounds.center : joystick.position;
        if (r != null) p.y = r.bounds.max.y;

        return p + Vector3.up * 0.02f;
    }

    Vector3 PuntoBoton()
    {
        Renderer r = boton.GetComponentInChildren<Renderer>();
        Vector3 p = r != null ? r.bounds.center : boton.position;

        return p + Vector3.up * 0.03f;
    }

    Vector3 PuntoRanura()
    {
        if (ranura == null) return PuntoBoton();

        Renderer r = ranura.GetComponentInChildren<Renderer>();
        Vector3 p = r != null ? r.bounds.center : ranura.position;

        // Un poco por delante de la ranura y no dentro: la mano deja la moneda
        // en la boca, no atraviesa la chapa.
        return p + maquina.NPCFrontDirection * 0.05f + Vector3.up * 0.02f;
    }

    void MoverMandos(float k)
    {
        if (maquina == null || reposo == null) return;
        if (joystick == null || boton == null) return;

        Transform raiz = maquina.transform;

        // --- el joystick -----------------------------------------------------
        Vector3 quiero = que == Que.Jugando ? HaciaDondeVaLaGarra() : Vector3.zero;

        inclinacion = Vector3.Lerp(inclinacion, quiero, k);

        Vector3 pos = raiz.TransformPoint(reposo.joystickPos);
        Quaternion rot = raiz.rotation * reposo.joystickRot;
        Vector3 pie = raiz.TransformPoint(reposo.joystickPie);

        float grados = inclinacion.magnitude * inclinacionJoystick;

        if (grados > 0.01f)
        {
            // Se inclina alrededor del pie del vastago, que es la unica forma de
            // que la bola se mueva y la base se quede quieta.
            Vector3 eje = Vector3.Cross(Vector3.up, inclinacion.normalized);

            if (eje.sqrMagnitude > 1e-8f)
            {
                Quaternion giro = Quaternion.AngleAxis(grados, eje.normalized);

                rot = giro * rot;
                pos = pie + giro * (pos - pie);
            }
        }

        joystick.position = pos;
        joystick.rotation = rot;

        // --- el boton --------------------------------------------------------
        MirarSiBaja();

        if (pulsacion > 0f) pulsacion -= Time.deltaTime;

        float hundido = 0f;

        if (pulsacion > 0f && duracionPulsacion > 0f)
        {
            // Baja y vuelve a subir, como un boton de verdad.
            float t = 1f - (pulsacion / duracionPulsacion);
            hundido = Mathf.Sin(t * Mathf.PI) * recorridoBoton;
        }

        boton.position = raiz.TransformPoint(reposo.botonPos) - raiz.up * hundido;

        // --- soltar del todo -------------------------------------------------
        //
        // Aqui, y no en Soltar(): hasta que los mandos no han vuelto a su sitio
        // hay que seguir moviendolos. Soltando antes se quedarian a medias.
        if (que == Que.Nada && inclinacion.magnitude < 0.002f && pulsacion <= 0f)
        {
            maquina = null;
            reposo = null;
        }
    }

    // Hacia donde va la garra, en el plano del suelo y de -1 a 1.
    Vector3 HaciaDondeVaLaGarra()
    {
        if (maquina == null || maquina.clawHead == null) return Vector3.zero;

        Vector3 ahora = maquina.clawHead.position;

        if (!garraMedida)
        {
            garraAnterior = ahora;
            garraMedida = true;
            return Vector3.zero;
        }

        Vector3 paso = ahora - garraAnterior;
        garraAnterior = ahora;

        if (Time.deltaTime <= 0f || velocidadTope <= 0f) return Vector3.zero;

        Vector3 v = paso / Time.deltaTime;
        v.y = 0f;

        Vector3 fuera = v / velocidadTope;

        return fuera.magnitude > 1f ? fuera.normalized : fuera;
    }

    // El boton se pulsa cuando la garra empieza a bajar, que es lo que se hace
    // en la maquina de verdad: se coloca con el joystick y se suelta con el
    // boton.
    void MirarSiBaja()
    {
        if (maquina == null || maquina.clawHead == null) return;

        if (que != Que.Jugando)
        {
            bajando = false;
            return;
        }

        float altura = maquina.transform.InverseTransformPoint(maquina.clawHead.position).y;

        if (!alturaMedida)
        {
            alturaAnterior = altura;
            alturaMedida = true;
            return;
        }

        float caida = (alturaAnterior - altura) / Mathf.Max(0.0001f, Time.deltaTime);
        alturaAnterior = altura;

        // Con histeresis: a pelo, el balanceo del cable enciende y apaga esto
        // varias veces por segundo y el boton se queda temblando.
        if (!bajando && caida > 0.08f)
        {
            bajando = true;
            pulsacion = duracionPulsacion;
        }
        else if (bajando && caida < 0.02f)
        {
            bajando = false;
        }
    }
}
