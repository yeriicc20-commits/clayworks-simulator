using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections;

// Genera el NavMesh del local en tiempo de ejecucion, para no tener que
// hornearlo a mano en el editor cada vez que cambia el escenario.
public class NavMeshBaker : MonoBehaviour
{
    public static NavMeshBaker Instance;

    [Tooltip("Capas que cuentan como suelo o pared. Los peluches se excluyen.")]
    public LayerMask includeLayers = ~(1 << 9);

    public bool rebuildOnMachineChange = true;

    private NavMeshSurface surface;
    private bool building = false;

    public bool IsReady { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        // Las maquinas se anaden sus MeshColliders en su propio Start, asi que
        // esperamos un frame para que existan antes de hornear.
        yield return null;

        Rebuild();
    }

    public void Rebuild()
    {
        if (building) return;

        building = true;

        if (surface == null)
        {
            surface = GetComponent<NavMeshSurface>();
            if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = includeLayers;
        }

        surface.BuildNavMesh();

        IsReady = true;
        building = false;
    }

    public void RequestRebuild()
    {
        if (!rebuildOnMachineChange) return;

        Rebuild();
    }
}
