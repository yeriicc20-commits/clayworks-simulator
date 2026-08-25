using UnityEngine;

// Dedos de la garra movidos por motores de verdad, no escribiendo su rotacion.
//
// Por que hace falta: hasta ahora los dedos se cerraban asignandoles la
// rotacion cada fotograma. Un transform no empuja: se mete DENTRO del peluche y
// PhysX resuelve la interpenetracion expulsandolo de un manotazo. Por eso la
// garra no agarraba nada aunque visualmente cerrase bien.
//
// Con una bisagra real y un motor con tope de par, el dedo empuja hasta que el
// peluche lo frena y se queda ahi apretando. Esa fuerza de apriete, contra el
// rozamiento del material, es lo unico que sostiene al peluche. Es el modelo
// que se pidio: rozamiento puro, sin uniones invisibles.
//
// El apriete flojo no es un defecto, es el diseno: una maquina real regula la
// corriente del motor para pagar cada tantas partidas. Aqui se hace igual, con
// ParaEstaPartida().
[DefaultExecutionOrder(-50)]
public class ClawFingerMotors : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("De donde cuelgan los dedos. Suele ser el brazo o la cabeza.")]
    public Rigidbody soporte;

    public Transform[] fingers;

    [Tooltip("Eje de giro de cada dedo, EN COORDENADAS DE MUNDO. Lo calcula "
             + "ClawController cruzando la direccion radial con la vertical. "
             + "Aqui se convierte al espacio del dedo, que es donde lo quiere "
             + "HingeJoint.")]
    public Vector3[] ejes;

    [Header("Mecanica")]
    [Tooltip("Masa de cada dedo, en kg. Un brazo de acero de esta talla ronda esto.")]
    public float fingerMass = 0.12f;

    [Tooltip("Angulo con la garra cerrada del todo, en grados. Negativo cierra.")]
    public float closedAngle = -36.5f;

    [Tooltip("A que velocidad gira el motor mientras no encuentra resistencia, "
             + "en grados por segundo. No subirlo sin rehacer la cuenta: un dedo "
             + "que avanza mas que el margen de contacto en un solo paso de "
             + "fisica se mete dentro de lo que toque antes de que nadie lo "
             + "detecte.")]
    public float motorSpeed = 100f;

    [Tooltip("Cuantos grados MAS del reposo puede abrirse un brazo al posarse "
             + "sobre algo. Es lo que le permite ceder y resbalar alrededor de "
             + "un peluche en vez de quedarse atrapado contra el.")]
    public float openOvershoot = 14f;

    [Tooltip("Par del motor al ABRIR. Generoso: abrir siempre tiene que funcionar.")]
    public float openTorque = 4f;

    [Header("Fuerza de apriete, en newton-metro")]
    [Tooltip("Lo minimo que puede tocar en una partida. Con esto no levanta ni "
             + "el peluche mas ligero: es la partida que no paga.")]
    public float torqueMin = 0.04f;

    [Tooltip("Par al 80% del mando. De ahi para arriba sube hasta torqueMax.")]
    public float torqueNormal = 0.45f;

    [Tooltip("Par al 100% del mando. Con esto NO se cae, y esa es la condicion "
             + "que fija el numero: no es 'mucho par', es el par que hace falta.")]
    public float torqueMax = 0.85f;

    [Tooltip("A partir de que punto del mando se deja de sortear la fuerza y "
             + "empieza a mandar el maximo.")]
    [Range(0.5f, 1f)] public float mandoFirme = 0.8f;

    [Tooltip("Cuantas partidas de cada diez salen con fuerza de sobra. El resto "
             + "se reparten por abajo. 0,12 = una de cada ocho, mas o menos.")]
    [Range(0f, 1f)] public float generousChance = 0.12f;

    [Tooltip("Lo que marca el mando del cuadro trasero, de 0 a 1. Es la corriente "
             + "que el dueno le da al motor, no la dificultad del juego: es el "
             + "punto de partida del que sube con cada partida sin premio.")]
    [Range(0f, 1f)] public float ajuste = 0.35f;

    [Header("Progresion entre premios")]
    [Tooltip("A las cuantas partidas sin premio llega al maximo del motor. Es el "
             + "intervalo de pago de una recreativa: la maquina va apretando un "
             + "poco mas cada jugada hasta que suelta uno, y ahi vuelve a empezar.")]
    public int jugadasParaGarantizar = 10;

    [HideInInspector] public int jugadasSinPremio = 0;

    [Header("Rozamiento")]
    [Tooltip("Material de los dedos. Sin rozamiento alto no sujeta nada, por "
             + "mucho par que tenga el motor.")]
    public PhysicsMaterial gripMaterial;

    HingeJoint[] joints;
    Rigidbody[] cuerpos;
    float parActual;

    public bool Listo { get { return joints != null && joints.Length > 0; } }
    public float ParActual { get { return parActual; } }

    // Techo de par de la proxima partida. Parte de lo que marca el mando y sube
    // con cada jugada sin premio hasta el maximo del motor.
    public float TechoActual
    {
        get
        {
            // El mando no va en linea recta hasta el maximo, sino con un codo.
            //
            // Hasta mandoFirme se mueve por donde se movia siempre, que es el
            // tramo con el que se juega. El ultimo tramo dispara el par, porque
            // para que agarre A VECES basta con poco y para que no se caiga
            // NUNCA hace falta bastante mas.
            float a = Mathf.Clamp01(ajuste);

            float baseTecho = a <= mandoFirme
                ? Mathf.Lerp(torqueMin, torqueNormal, a / mandoFirme)
                : Mathf.Lerp(torqueNormal, torqueMax, (a - mandoFirme) / (1f - mandoFirme));

            float progreso = jugadasParaGarantizar <= 0
                             ? 1f
                             : Mathf.Clamp01((float)jugadasSinPremio / jugadasParaGarantizar);

            // Al cuadrado, no en linea recta. Subiendo en linea recta la maquina
            // llegaba a agarrar seguro por la cuarta partida y el mando de la
            // trasera dejaba de importar. Asi se mantiene tacana un buen rato y
            // se suelta al final, que ademas es como se recuerda una maquina de
            // estas: muchas seguidas sin nada y de pronto una que si.
            return Mathf.Lerp(baseTecho, torqueMax, progreso * progreso);
        }
    }

    // Lo mismo pero de 0 a 1, que es lo que marca la aguja de la esfera.
    public float FuerzaEfectiva
    {
        get { return Mathf.InverseLerp(torqueMin, torqueMax, TechoActual); }
    }

    // Se ha llevado uno: la cuenta vuelve a cero, como en una maquina de verdad.
    public void Premiado()
    {
        jugadasSinPremio = 0;
    }

    // ------------------------------------------------------------------ montaje

    public bool Construir()
    {
        if (soporte == null || fingers == null || fingers.Length == 0)
        {
            Debug.LogWarning("[Garra] Faltan piezas para montar los motores.", this);
            return false;
        }

        joints = new HingeJoint[fingers.Length];
        cuerpos = new Rigidbody[fingers.Length];

        for (int i = 0; i < fingers.Length; i++)
        {
            if (fingers[i] == null) continue;

            Rigidbody rb = fingers[i].GetComponent<Rigidbody>();
            if (rb == null) rb = fingers[i].gameObject.AddComponent<Rigidbody>();

            rb.mass = fingerMass;
            rb.useGravity = false;          // la sujeta la bisagra, no cae sola
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Especulativa y no continua: el dedo es una pieza pequena unida por
            // una bisagra, no puede recorrer distancias grandes en un paso.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Sin esto, un dedo que quede pillado contra el cristal sale
            // disparado al liberarse.
            rb.maxDepenetrationVelocity = 0.4f;

            // Muchas mas pasadas del solver que las 10 de la configuracion
            // global. El motor de la bisagra y el contacto contra el peluche se
            // resuelven en el mismo sitio, y con pocas pasadas gana el motor:
            // el dedo se mete dentro del peluche en vez de empujarlo. Al subir
            // el par eso paso de no verse a verse. Son tres dedos: pagar
            // pasadas de solver aqui no cuesta practicamente nada.
            rb.solverIterations = 32;
            rb.solverVelocityIterations = 12;

            rb.linearDamping = 0.05f;
            rb.angularDamping = 1.5f;

            cuerpos[i] = rb;

            if (gripMaterial != null)
            {
                foreach (Collider col in fingers[i].GetComponentsInChildren<Collider>())
                {
                    if (!col.isTrigger) col.sharedMaterial = gripMaterial;
                }
            }

            HingeJoint j = fingers[i].GetComponent<HingeJoint>();
            if (j == null) j = fingers[i].gameObject.AddComponent<HingeJoint>();

            j.connectedBody = soporte;

            // El origen del dedo ESTA en su bisagra: por eso el modelo se hizo
            // asi en Blender. El ancla es literalmente el cero local, sin
            // medir nada ni inventarse puntos auxiliares.
            j.anchor = Vector3.zero;
            j.autoConfigureConnectedAnchor = true;

            // El eje llega en mundo; HingeJoint lo quiere en el espacio del
            // propio dedo. Pasarlo sin convertir deja las tres bisagras girando
            // sobre el mismo eje y la garra se abre de lado.
            j.axis = (ejes != null && i < ejes.Length && ejes[i].sqrMagnitude > 0.001f)
                     ? fingers[i].InverseTransformDirection(ejes[i]).normalized
                     : Vector3.right;

            // El limite abierto se pasa unos grados del reposo a proposito.
            //
            // Al posarse sobre un monton, los brazos de una garra de verdad se
            // abren un poco y resbalan alrededor de lo que tocan. Sin ese
            // margen, el brazo no puede ceder: la carcasa sigue bajando porque
            // va por transform y no cede, el dedo queda atrapado entre ella y el
            // peluche, y quien acaba saliendo por donde no debe es el peluche.
            j.useLimits = true;
            JointLimits lim = j.limits;
            lim.min = Mathf.Min(-openOvershoot, closedAngle);
            lim.max = Mathf.Max(openOvershoot, closedAngle);
            lim.bounciness = 0f;
            lim.contactDistance = 1f;
            j.limits = lim;

            j.useMotor = true;
            j.enableCollision = false;      // los dedos no chocan entre ellos
            j.enablePreprocessing = false;  // deja al solver hacer su trabajo

            joints[i] = j;
        }

        Abrir();
        return true;
    }

    public void Soltar()
    {
        joints = null;
        cuerpos = null;
    }

    // ------------------------------------------------------------- movimiento

    // Fuerza de esta partida. Casi siempre floja: es como hace trampa una
    // maquina real, bajando la corriente del motor salvo cada tantas jugadas.
    public float ParaEstaPartida()
    {
        // El mando pone el punto de partida y la cuenta de partidas sin premio
        // lo va subiendo. Las generosas se saltan ese techo y tiran hacia el
        // maximo, que es lo que hace que a veces pague antes de tiempo.
        float techo = TechoActual;

        bool generosa = Random.value < generousChance;

        // El sorteo va del 60% del techo al techo, no del minimo al techo.
        // Repartiendo desde el minimo, ni con el mando al maximo se notaba:
        // seguian saliendo partidas flojas la mitad de las veces.
        float suelo = Mathf.Lerp(torqueMin, techo, 0.6f);

        // Y el sorteo se estrecha segun sube el mando, hasta desaparecer en el
        // tope. Al 100% no puede haber loteria: si el dueno ha puesto el maximo
        // y el jugador ha cogido bien el peluche, se lo lleva. Antes al 100%
        // seguia saliendo cualquier cosa entre 0,29 y 0,45, y con la de abajo el
        // peluche se escurria aunque el mando dijera que no podia pasar.
        float firme = Mathf.Clamp01((Mathf.Clamp01(ajuste) - mandoFirme)
                                    / Mathf.Max(1e-4f, 1f - mandoFirme));

        suelo = Mathf.Lerp(suelo, techo, firme);

        parActual = generosa
            ? Random.Range(Mathf.Lerp(techo, torqueMax, 0.5f), torqueMax)
            : Random.Range(suelo, techo);

        jugadasSinPremio++;

        return parActual;
    }

    public void Cerrar(float par)
    {
        parActual = par;
        Motor(Mathf.Sign(closedAngle) * motorSpeed, par);
    }

    public void Abrir()
    {
        Motor(-Mathf.Sign(closedAngle) * motorSpeed, openTorque);
    }

    void Motor(float velocidad, float par)
    {
        if (joints == null) return;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;

            JointMotor m = joints[i].motor;
            m.targetVelocity = velocidad;
            m.force = par;
            m.freeSpin = false;
            joints[i].motor = m;

            if (cuerpos[i] != null) cuerpos[i].WakeUp();
        }
    }

    // Cuanto ha cerrado el dedo que MENOS ha cerrado, en grados.
    //
    // Se mira el peor y no la media a proposito: si un dedo se queda a medias
    // porque el peluche esta descentrado, la garra agarra mal aunque los otros
    // dos hayan cerrado del todo.
    //
    // Va con signo, en el sentido de cierre, y no en valor absoluto. En valor
    // absoluto los 14 grados que los brazos se abren DE MAS al posarse contaban
    // como catorce grados de cierre, o sea que una garra abierta de par en par
    // puntuaba mejor que una a medio cerrar.
    public float CierreMinimo()
    {
        if (joints == null || joints.Length == 0) return 0f;

        float sentido = Mathf.Sign(closedAngle);
        float peor = Mathf.Abs(closedAngle);

        foreach (HingeJoint j in joints)
        {
            if (j == null) continue;
            peor = Mathf.Min(peor, j.angle * sentido);
        }

        return Mathf.Max(0f, peor);
    }
}
