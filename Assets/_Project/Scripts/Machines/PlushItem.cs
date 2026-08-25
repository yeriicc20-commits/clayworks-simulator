using System.Collections.Generic;
using UnityEngine;

public class PlushItem : MonoBehaviour
{
    public enum WeightCategory { Ligero, Medio, Pesado }

    public WeightCategory weightCategory = WeightCategory.Medio;

    [Tooltip("Cuanto se encoge el collider respecto a lo que se ve. Un peluche es blando.")]
    [Range(0.6f, 1f)] public float colliderShrink = 0.94f;

    [Tooltip("Material fisico del peluche: friccion alta y sin rebote.")]
    public PhysicsMaterial physicsMaterial;

    [Tooltip("Frena la deriva del peluche cuando ya esta apoyado.")]
    public float linearDamping = 0.2f;
    [Tooltip("Evita que ruede eternamente al caer.")]
    public float angularDamping = 0.6f;
    [HideInInspector] public bool isGrabbed = false;
    [HideInInspector] public bool hasBeenGrabbed = false;

    // Evita que la garra y la zona de premio lo cobren dos veces.
    [HideInInspector] public bool collected = false;

    void Awake()
    {
        EnsureAccurateColliders();
    }

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = GetWeightValue();

            // A cuanto puede salir de dentro de otra cosa cuando ya esta metido.
            //
            // Estaba en 0,15 m/s para que dos peluches que aparecen solapados no
            // salieran disparados. Pero eso es 67 ms para deshacer un centimetro,
            // y mientras tanto el motor de la garra sigue empujando: el dedo se
            // hunde mas rapido de lo que el peluche puede salir, y se ve el dedo
            // metido dentro. A 0,5 el centimetro se deshace en 20 ms, que a la
            // vista es al momento, y sigue siendo un empujon suave.
            rb.maxDepenetrationVelocity = 0.5f;

            // Mas pasadas del solver que las 10 de la configuracion global.
            //
            // Un motor de articulacion y un contacto se resuelven en el mismo
            // solver, y con pocas pasadas el motor gana: por eso al subir el par
            // los dedos empezaron a meterse dentro. No es que sobre fuerza, es
            // que faltaban pasadas para repartirla.
            rb.solverIterations = 20;
            rb.solverVelocityIterations = 8;

            // Un peluche apoyado tiene que quedarse quieto. Sin amortiguamiento
            // se pasa segundos deslizandose y rodando, y ademas tarda mucho mas
            // en dormirse, que con veinte dentro de la maquina se nota.
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    // Colliders medidos en el espacio DEL PELUCHE, una caja por parte.
    //
    // El fallo que habia aqui era sutil y explica el hueco invisible que se veia
    // entre peluches: se median con renderer.bounds, que es la caja alineada con
    // los ejes DEL MUNDO. Y los peluches nacen con Random.rotation. Un oso
    // girado 45 grados en los tres ejes tiene una caja de mundo hasta un 73 por
    // ciento mas grande que el, asi que su collider salia inflado en esa misma
    // proporcion. Y como el giro es aleatorio, cada peluche se inflaba distinto:
    // de ahi que el hueco pareciese caprichoso.
    //
    // Midiendo los vertices en el espacio del propio peluche, el giro deja de
    // importar y la caja sale del tamano que es.
    //
    // Ademas se pone una caja POR PARTE en vez de una sola para todo. Un oso no
    // es un ladrillo: con una caja unica, dos osos se tocan por las esquinas de
    // sus cajas y se ven separados. Con cabeza y cuerpo por separado encajan
    // como encajan de verdad. Las piezas pequenas (ojos, morro) se descartan:
    // no cambian nada y cada una seria un par de contacto mas.
    void EnsureAccurateColliders()
    {
        if (GetComponent<Collider>() != null) return;

        MeshFilter[] mallas = GetComponentsInChildren<MeshFilter>();

        var partes = new List<Bounds>();
        float mayor = 0f;

        foreach (MeshFilter mf in mallas)
        {
            if (mf == null || mf.sharedMesh == null) continue;

            Bounds b = EnEspacioLocal(mf);
            if (b.size.sqrMagnitude < 1e-8f) continue;

            partes.Add(b);
            mayor = Mathf.Max(mayor, Volumen(b));
        }

        if (partes.Count == 0) return;

        int puestos = 0;

        foreach (Bounds b in partes)
        {
            // Se compara por VOLUMEN y no por la diagonal de la caja.
            //
            // Con la diagonal, una oreja larga y plana contaba como pieza
            // grande: mide poco de ancho y de fondo, pero es larga, asi que su
            // diagonal se acerca a la del cuerpo. Le tocaba collider, y una
            // esfera colgando a un lado de la cabeza ensancha el peluche y lo
            // deja sin caber en la garra.
            //
            // El volumen si distingue: una oreja abulta un 9% de lo que abulta
            // el cuerpo, aunque sea casi igual de larga.
            if (partes.Count > 1 && Volumen(b) < mayor * 0.2f) continue;

            Esfera(b);
            puestos++;
        }

        // Red de seguridad: si el filtro se lo ha comido todo, una esfera para
        // todo el peluche antes que dejarlo sin collider.
        if (puestos == 0)
        {
            Bounds todo = partes[0];
            for (int i = 1; i < partes.Count; i++) todo.Encapsulate(partes[i]);

            Esfera(todo);
        }
    }

    // Una esfera por parte, no una caja.
    //
    // Este fue el fallo que dejaba a los peluches separados con aire entre
    // ellos. Las cajas tenian el tamano correcto, medido, pero un oso no es una
    // caja: sus esquinas sobresalen unos tres centimetros del pelo por cada
    // lado. Dos osos se tocaban esquina contra esquina y quedaban separados esa
    // distancia sin que hubiera nada visible en medio. Y como se apoyaban sobre
    // esas esquinas, todo el monton quedaba mas alto de lo que parecia.
    //
    // Una esfera encaja con la forma de un peluche y ademas rueda por encima de
    // sus vecinos hasta acomodarse, que es como se amontonan de verdad.
    void Esfera(Bounds b)
    {
        SphereCollider s = gameObject.AddComponent<SphereCollider>();
        s.center = b.center;

        // El radio sale de la media de los tres semiejes, no del mayor ni del
        // menor. Del mayor sobresaldria por los lados estrechos, y del menor
        // quedaria un muneco de trapo dentro de una canica.
        float medio = (b.extents.x + b.extents.y + b.extents.z) / 3f;

        // Y encogido a proposito. Un peluche es blando: dejandolo justo a su
        // silueta se ven separados, y encogiendolo se hunden un poco unos en
        // otros, que es exactamente lo que hace un monton de peluches.
        s.radius = medio * colliderShrink;
        s.sharedMaterial = physicsMaterial;
    }

    static float Volumen(Bounds b)
    {
        return b.size.x * b.size.y * b.size.z;
    }

    // Caja de una malla en el espacio del peluche, pasando sus ocho esquinas.
    // No vale con escalar el tamano: si la pieza viene girada respecto al
    // peluche, la caja hay que rehacerla a partir de los puntos.
    Bounds EnEspacioLocal(MeshFilter mf)
    {
        Bounds local = mf.sharedMesh.bounds;
        Bounds fuera = new Bounds();
        bool primero = true;

        for (int i = 0; i < 8; i++)
        {
            Vector3 signo = new Vector3((i & 1) == 0 ? -1f : 1f,
                                        (i & 2) == 0 ? -1f : 1f,
                                        (i & 4) == 0 ? -1f : 1f);

            Vector3 esquina = local.center + Vector3.Scale(local.extents, signo);
            Vector3 p = transform.InverseTransformPoint(mf.transform.TransformPoint(esquina));

            if (primero) { fuera = new Bounds(p, Vector3.zero); primero = false; }
            else fuera.Encapsulate(p);
        }

        return fuera;
    }

    // Masas de peluche de verdad, en kilos. Antes eran 1, 2,5 y 4 kg: eso es lo
    // que pesa un perro pequeno, no un peluche de 20 cm, que anda por los 200
    // gramos. Con aquellas masas ninguna garra con fuerza realista levantaba
    // nada, y lo que mas importa no es el valor suelto sino la proporcion con
    // la cabeza de la garra: 1,5 kg de garra contra 0,2 kg de peluche es sano.
    public float GetWeightValue()
    {
        switch (weightCategory)
        {
            case WeightCategory.Ligero: return 0.15f;
            case WeightCategory.Medio: return 0.25f;
            case WeightCategory.Pesado: return 0.4f;
            default: return 0.25f;
        }
    }
}
