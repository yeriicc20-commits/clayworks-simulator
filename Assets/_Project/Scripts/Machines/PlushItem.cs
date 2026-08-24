using UnityEngine;

public class PlushItem : MonoBehaviour
{
    public enum WeightCategory { Ligero, Medio, Pesado }

    public WeightCategory weightCategory = WeightCategory.Medio;

    [Tooltip("Por encima de estos vertices se usa una primitiva en vez de la malla.")]
    public int convexVertexLimit = 255;
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
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    void EnsureAccurateColliders()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            AddCollider(mf.gameObject, mf.sharedMesh);
        }

        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in skinnedRenderers)
        {
            AddCollider(smr.gameObject, smr.sharedMesh);
        }
    }

    // PhysX no sabe hacer un casco convexo de mas de 255 poligonos: con mallas
    // mas finas se inventa una aproximacion y avisa por consola. En esos casos
    // se pone una primitiva a la medida de la malla, que ademas es mas barata.
    //
    // Un peluche no necesita colision exacta: la garra lo detecta por su propio
    // radio, y el collider solo decide como se apilan unos sobre otros.
    void AddCollider(GameObject target, Mesh mesh)
    {
        if (mesh == null || target.GetComponent<Collider>() != null) return;

        if (mesh.vertexCount <= convexVertexLimit)
        {
            MeshCollider meshCollider = target.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = true;
            return;
        }

        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;

        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));

        // Los peluches son bolas y la esfera les sienta mejor que la caja, pero
        // si la pieza es alargada la caja se ajusta mucho mas.
        if (smallest > 0f && largest / smallest < 1.6f)
        {
            SphereCollider sphere = target.AddComponent<SphereCollider>();
            sphere.center = bounds.center;
            sphere.radius = largest * 0.5f;
            return;
        }

        BoxCollider box = target.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = size;
    }

    public float GetWeightValue()
    {
        switch (weightCategory)
        {
            case WeightCategory.Ligero: return 1f;
            case WeightCategory.Medio: return 2.5f;
            case WeightCategory.Pesado: return 4f;
            default: return 2.5f;
        }
    }
}
