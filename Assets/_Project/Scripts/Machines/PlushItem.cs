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
            rb.maxDepenetrationVelocity = 0.15f;

            // Un peluche apoyado tiene que quedarse quieto. Sin amortiguamiento
            // se pasa segundos deslizandose y rodando, y ademas tarda mucho mas
            // en dormirse, que con veinte dentro de la maquina se nota.
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    // UN collider para todo el peluche, medido de lo que se ve.
    //
    // Antes se le ponia uno por malla, y ahi estaba el fallo: las mallas de
    // estos modelos son varios trozos repartidos por el muneco, asi que la caja
    // de cada una abarcaba el peluche entero. Cuatro cajas grandes solapadas =
    // un cubo invisible alrededor, y los peluches flotando sin tocarse.
    //
    // Un peluche no necesita colision por piezas: lo que decide como se apilan
    // es su volumen general. Uno solo es ademas cuatro veces mas barato, que
    // con veinte dentro de la maquina se nota.
    void EnsureAccurateColliders()
    {
        if (GetComponent<Collider>() != null) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        Bounds combined = new Bounds();
        bool any = false;

        foreach (Renderer rend in renderers)
        {
            if (rend == null || rend is ParticleSystemRenderer) continue;

            if (!any)
            {
                combined = rend.bounds;
                any = true;
            }
            else
            {
                combined.Encapsulate(rend.bounds);
            }
        }

        if (!any) return;

        BoxCollider box = gameObject.AddComponent<BoxCollider>();

        box.center = transform.InverseTransformPoint(combined.center);

        // Encogido a proposito. Un peluche es blando: se aplasta un poco al
        // apoyarse, y con el collider justo a su silueta se ven separados por
        // un hueco. Encogerlo les hace parecer que se tocan y se hunden un
        // poco unos en otros, que es lo que hace un monton de peluches.
        Vector3 size = transform.InverseTransformVector(combined.size);

        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * colliderShrink;

        box.sharedMaterial = physicsMaterial;
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
