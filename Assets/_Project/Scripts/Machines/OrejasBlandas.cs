using UnityEngine;

// Orejas que cuelgan, se balancean y se DOBLAN.
//
// Antes eran barras rigidas que giraban sobre su costura. Eso hacia bien el
// balanceo, pero una barra rigida atraviesa todo lo que se le cruce: en la
// maquina se veian orejas saliendo por debajo del cristal. Y no habia forma de
// arreglarlo girando, porque el problema no era hacia donde apuntaba la oreja
// sino que no podia amoldarse a nada.
//
// Asi que la oreja es ahora un cable: una cadena de nudos que cae por su peso,
// se estira hasta su largo y se aparta de lo que toca. La malla se deforma
// siguiendo la cadena, con lo que se doble donde se doble el cable, se dobla la
// oreja.
//
// Sigue sin haber Rigidbodies ni articulaciones, y sigue siendo a proposito. Una
// oreja con cuerpo propio colgada del peluche seria un cuerpo dentro de otro
// cuerpo, que es exactamente lo que dejo la garra dando tumbos durante media
// sesion. Ademas en la maquina hay veinte peluches: serian cuarenta cuerpos mas.
// Verlet no puede reventar, no le pide nada a PhysX salvo consultas, y cuando el
// peluche se duerme deja de costar nada.
public class OrejasBlandas : MonoBehaviour
{
    public Transform[] orejas;

    [Header("Cable")]
    [Tooltip("En cuantos trozos se parte la oreja. Mas = se dobla mas fino.")]
    public int nudos = 6;

    [Tooltip("Cuanto se corrige el largo en cada pasada, de 0 a 1.")]
    [Range(0.1f, 1f)] public float rigidez = 0.8f;

    [Tooltip("Pasadas de correccion por fotograma. Mas = cable menos elastico.")]
    [Range(1, 8)] public int pasadas = 3;

    [Tooltip("Cuanto conserva el movimiento de un fotograma al siguiente.")]
    [Range(0.5f, 1f)] public float inercia = 0.92f;

    [Tooltip("Lo mas que puede doblarse en cada nudo. Bajo = oreja mas tiesa.")]
    public float anguloPorNudo = 34f;

    [Tooltip("Grosor del cable para chocar. La mitad del canto de la oreja.")]
    public float radio = 0.012f;

    [Tooltip("Contra que choca. Los peluches NO van aqui: cada oreja solo mira "
             + "el escenario y su propio peluche.")]
    public LayerMask contra = ~0;

    class Cable
    {
        public Transform t;
        public Mesh malla;

        public Vector3[] perp;      // el vertice, apartado del eje
        public float[] altura;      // donde cae a lo largo de la oreja, de 0 a 1
        public Vector3[] normal;

        public Vector3[] nudo;
        public Vector3[] previo;
        public Vector3[] local;
        public Quaternion[] marco;

        public Vector3[] vs;
        public Vector3[] ns;

        public float tramo;
        public float freno;

        // Hacia donde cuelga en reposo, en su propio espacio. Medido, no supuesto.
        public Vector3 eje;
    }

    Cable[] cables;
    Collider[] propios;
    readonly Collider[] cerca = new Collider[8];

    Vector3 posPrevia;
    Quaternion rotPrevia;
    Rigidbody cuerpo;
    int quieto;

    void Start()
    {
        if (orejas == null || orejas.Length == 0) { enabled = false; return; }

        // El prefab guarda estos numeros dentro, y antes este componente era un
        // muelle: alli "rigidez" valia 90 y aqui es un factor de 0 a 1. Un
        // prefab que no se haya rehecho traeria el 90, la correccion de largo se
        // pasaria noventa veces de rosca y el cable saldria disparado. El
        // [Range] del inspector no protege de eso, porque solo recorta lo que se
        // teclea, no lo que se carga.
        rigidez = Mathf.Clamp(rigidez, 0.1f, 1f);
        inercia = Mathf.Clamp(inercia, 0.5f, 1f);
        pasadas = Mathf.Clamp(pasadas, 1, 8);
        anguloPorNudo = Mathf.Clamp(anguloPorNudo, 5f, 90f);
        radio = Mathf.Clamp(radio, 0.001f, 0.1f);

        cuerpo = GetComponent<Rigidbody>();
        propios = GetComponentsInChildren<Collider>(true);

        var lista = new System.Collections.Generic.List<Cable>();

        foreach (Transform t in orejas)
        {
            Cable c = Montar(t);
            if (c != null) lista.Add(c);
        }

        cables = lista.ToArray();

        if (cables.Length == 0) { enabled = false; return; }

        posPrevia = transform.position;
        rotPrevia = transform.rotation;
    }

    Cable Montar(Transform t)
    {
        if (t == null) return null;

        MeshFilter mf = t.GetComponent<MeshFilter>();

        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("[Orejas] " + t.name + " no tiene malla, la dejo quieta.");
            return null;
        }

        Cable c = new Cable();
        c.t = t;

        // Copia propia de la malla: se va a deformar, y la del asset la comparten
        // todos los peluches de la maquina.
        c.malla = mf.mesh;

        // Sin Read/Write en el importador del modelo, vertices devuelve un
        // array VACIO en ejecucion. No falla, no avisa: simplemente no hay
        // nada. Las orejas llevaban asi desde que existen, y el aviso que
        // salia culpaba al eje de la malla en vez de a esto.
        if (!c.malla.isReadable)
        {
            Debug.LogWarning("[Orejas] La malla de " + t.name + " no se puede "
                             + "leer. Marca Read/Write en el importador del "
                             + "modelo del peluche o las orejas no se moveran.", t);
            return null;
        }

        Vector3[] v = c.malla.vertices;
        c.normal = c.malla.normals;

        // Hacia donde cuelga se MIDE: es el vertice mas lejos de la costura, y
        // la costura es el origen del objeto.
        //
        // Estaba fijado a -Y porque asi sale del exportador, y no salia asi: la
        // condicion no se cumplia nunca, las orejas se quedaban quietas y cada
        // peluche soltaba dos avisos al aparecer. Con veinte en la maquina eso
        // es la consola llena de lo mismo tapando lo que si importa. Y encima el
        // aviso decia la verdad, asi que era facil leerlo y seguir de largo.
        //
        // Midiendolo da igual como venga la malla, y no hay condicion que
        // incumplir.
        Vector3 punta = Vector3.zero;
        float largo = 0f;

        foreach (Vector3 p in v)
        {
            if (p.magnitude <= largo) continue;

            largo = p.magnitude;
            punta = p;
        }

        if (largo < 1e-4f)
        {
            Debug.LogWarning("[Orejas] " + t.name + " tiene la malla toda en su "
                             + "origen: no hay oreja que colgar.");
            return null;
        }

        c.eje = punta / largo;

        int n = Mathf.Max(3, nudos);

        c.perp = new Vector3[v.Length];
        c.altura = new float[v.Length];
        c.vs = new Vector3[v.Length];
        c.ns = new Vector3[v.Length];

        for (int i = 0; i < v.Length; i++)
        {
            float alo = Vector3.Dot(v[i], c.eje);

            c.altura[i] = Mathf.Clamp01(alo / largo);
            c.perp[i] = v[i] - c.eje * alo;
        }

        c.tramo = largo / (n - 1);
        c.nudo = new Vector3[n];
        c.previo = new Vector3[n];
        c.local = new Vector3[n];
        c.marco = new Quaternion[n];

        for (int k = 0; k < n; k++)
        {
            c.nudo[k] = t.TransformPoint(c.eje * (c.tramo * k));
            c.previo[k] = c.nudo[k];
        }

        // Que no se muevan las dos exactamente igual: se nota que es un truco.
        c.freno = Mathf.Clamp01(inercia * Random.Range(0.96f, 1.0f));

        return c;
    }

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        if (dt <= 0f) return;

        // Si el peluche esta dormido y el cable ya se ha parado, no hay nada que
        // recalcular. En un monton de veinte peluches quietos esto es la
        // diferencia entre costar algo y no costar nada.
        bool movido = (transform.position - posPrevia).sqrMagnitude > 1e-10f
                      || Quaternion.Angle(transform.rotation, rotPrevia) > 0.02f;

        posPrevia = transform.position;
        rotPrevia = transform.rotation;

        if (movido || (cuerpo != null && !cuerpo.IsSleeping())) quieto = 0;

        if (quieto > 2) return;

        Vector3 g = Physics.gravity * (dt * dt);
        float maxAngulo = anguloPorNudo * Mathf.Deg2Rad;
        float moviendose = 0f;

        foreach (Cable c in cables)
        {
            int n = c.nudo.Length;

            // La costura no se simula: va donde va el peluche.
            c.nudo[0] = c.t.position;
            c.previo[0] = c.nudo[0];

            for (int k = 1; k < n; k++)
            {
                Vector3 paso = (c.nudo[k] - c.previo[k]) * c.freno;
                c.previo[k] = c.nudo[k];
                c.nudo[k] += paso + g;

                moviendose = Mathf.Max(moviendose, paso.sqrMagnitude);
            }

            for (int p = 0; p < pasadas; p++)
            {
                // Largo. Solo se mueve el nudo de abajo: el de arriba manda, y
                // asi la cadena queda mandada desde la costura y no se estira.
                for (int k = 1; k < n; k++)
                {
                    Vector3 d = c.nudo[k] - c.nudo[k - 1];
                    float len = d.magnitude;

                    if (len > 1e-6f)
                        c.nudo[k] -= d * ((len - c.tramo) / len * rigidez);
                }

                // Doblado. Sin esto la oreja se pliega sobre si misma en cuanto
                // toca algo, y una oreja de trapo rellena no hace eso.
                for (int k = 2; k < n; k++)
                {
                    Vector3 antes = c.nudo[k - 1] - c.nudo[k - 2];
                    Vector3 ahora = c.nudo[k] - c.nudo[k - 1];

                    if (antes.sqrMagnitude < 1e-10f || ahora.sqrMagnitude < 1e-10f)
                        continue;

                    Vector3 tope = Vector3.RotateTowards(antes.normalized,
                                                         ahora.normalized,
                                                         maxAngulo, 0f);

                    c.nudo[k] = c.nudo[k - 1] + tope * ahora.magnitude;
                }
            }

            // Los choques van una sola vez por fotograma, despues de cuadrar la
            // forma. Metidos dentro de las pasadas serian tres veces mas
            // consultas a PhysX por nudo y no se nota la diferencia.
            for (int k = 1; k < n; k++) Apartar(c, k);

            Deformar(c);
        }

        quieto = moviendose < 1e-9f ? quieto + 1 : 0;
    }

    // Saca el nudo de dentro de lo que sea que este tocando.
    void Apartar(Cable c, int k)
    {
        Vector3 p = c.nudo[k];

        Empujar(propios, propios.Length, ref p, c.previo[k]);

        int n = Physics.OverlapSphereNonAlloc(p, radio, cerca, contra,
                                              QueryTriggerInteraction.Ignore);
        Empujar(cerca, n, ref p, c.previo[k]);

        c.nudo[k] = p;
    }

    void Empujar(Collider[] lista, int cuantos, ref Vector3 p, Vector3 venia)
    {
        for (int j = 0; j < cuantos; j++)
        {
            Collider col = lista[j];
            if (col == null || !col.enabled || col.isTrigger) continue;

            Vector3 cp = col.ClosestPoint(p);
            Vector3 d = p - cp;
            float dist = d.magnitude;

            if (dist > 1e-5f)
            {
                // Fuera pero rozando: se separa hasta el grosor del cable.
                if (dist < radio) p = cp + d * (radio / dist);
                continue;
            }

            // ClosestPoint devuelve el mismo punto: esta DENTRO, y entonces no
            // dice por donde salir. Se sale por donde se entro, que es de donde
            // venia el nudo el fotograma anterior.
            Vector3 salida = venia - p;

            if (salida.sqrMagnitude < 1e-10f) salida = p - col.bounds.center;
            if (salida.sqrMagnitude < 1e-10f) salida = Vector3.up;

            p += salida.normalized * radio;
        }
    }

    // Dobla la malla para que siga a la cadena.
    void Deformar(Cable c)
    {
        int n = c.nudo.Length;

        for (int k = 0; k < n; k++) c.local[k] = c.t.InverseTransformPoint(c.nudo[k]);

        // Un marco por tramo, encadenados. Se propaga el giro de uno al
        // siguiente en vez de calcular cada uno por su cuenta: asi la oreja no
        // se retuerce sobre su eje al doblarse.
        Quaternion acumulado = Quaternion.identity;

        for (int k = 0; k < n - 1; k++)
        {
            Vector3 d = c.local[k + 1] - c.local[k];

            if (d.sqrMagnitude > 1e-10f)
            {
                acumulado = Quaternion.FromToRotation(acumulado * c.eje,
                                                      d.normalized) * acumulado;
            }

            c.marco[k] = acumulado;
        }

        c.marco[n - 1] = c.marco[n - 2];

        // Y uno por nudo, promediando, para que la curva salga suave y no a
        // codos entre tramo y tramo.
        for (int k = n - 2; k >= 1; k--)
            c.marco[k] = Quaternion.Slerp(c.marco[k - 1], c.marco[k], 0.5f);

        int tramos = n - 1;

        for (int i = 0; i < c.vs.Length; i++)
        {
            float r = c.altura[i] * tramos;
            int k = Mathf.Min((int)r, tramos - 1);
            float f = r - k;

            Quaternion q = Quaternion.Slerp(c.marco[k], c.marco[k + 1], f);

            c.vs[i] = Vector3.Lerp(c.local[k], c.local[k + 1], f) + q * c.perp[i];
            c.ns[i] = q * c.normal[i];
        }

        c.malla.SetVertices(c.vs);
        c.malla.SetNormals(c.ns);
        c.malla.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (cables == null) return;

        // La malla es una copia hecha en Start. Si no se destruye, cada peluche
        // que aparece deja dos sueltas y en una sesion larga se van juntando.
        foreach (Cable c in cables)
        {
            if (c != null && c.malla != null) Destroy(c.malla);
        }
    }
}
