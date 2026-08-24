using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ShopItem
{
    public string itemName;
    public int price;
    public Sprite icon;
    public GameObject boxPrefab;
    public GameObject machinePrefab;
}

[System.Serializable]
public class ToyShopItem
{
    public string itemName;
    public int price;
    public Sprite icon;
    public GameObject toyPrefab;
    public int boxToyCount = 10;

    [Tooltip("Caja en la que llega. Vacio = la mediana por defecto.")]
    public GameObject boxPrefab;
}

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    public ShopItem[] items;
    public ToyShopItem[] toyItems;
    public Transform spawnPoint;
    public GameObject toyBoxPrefab;

    public Transform itemsContainer;
    public GameObject itemCardPrefab;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GenerateShopUI();
        WarmUpToyPhysics();
    }

    [Tooltip("Debe coincidir con el mismo campo de PlushItem.")]
    public int convexVertexLimit = 255;

    // La primera vez que aparece un peluche, PhysX tiene que cocinar un collider
    // convexo por cada malla suya y el juego se congela un par de segundos. Aqui
    // lo precocinamos en segundo plano, que es justo para lo que existe BakeMesh.
    void WarmUpToyPhysics()
    {
        List<EntityId> meshIds = new List<EntityId>();

        foreach (ToyShopItem toy in toyItems)
        {
            CollectMeshIds(toy.toyPrefab, meshIds);
        }

        if (meshIds.Count == 0) return;

        EntityId[] ids = meshIds.ToArray();

        System.Threading.Tasks.Task.Run(() =>
        {
            foreach (EntityId id in ids) Physics.BakeMesh(id, true);
        });
    }

    void CollectMeshIds(GameObject prefab, List<EntityId> into)
    {
        if (prefab == null) return;

        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            AddMeshId(filter.sharedMesh, into);
        }

        foreach (SkinnedMeshRenderer skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            AddMeshId(skinned.sharedMesh, into);
        }
    }

    void AddMeshId(Mesh mesh, List<EntityId> into)
    {
        // Sin Read/Write activado en el modelo no se puede precocinar.
        if (mesh == null || !mesh.isReadable) return;

        // Las mallas muy finas no llegan a tener collider de malla: PlushItem
        // les pone una primitiva. Precocinarlas solo servia para que PhysX se
        // quejase por consola de un casco convexo que luego no se usa.
        if (mesh.vertexCount > convexVertexLimit) return;

        EntityId id = mesh.GetEntityId();

        if (!into.Contains(id)) into.Add(id);
    }

    public void GenerateShopUI()
    {
        foreach (Transform child in itemsContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < items.Length; i++)
        {
            int index = i;

            GameObject card = Instantiate(itemCardPrefab, itemsContainer);
            ItemCardUI cardUI = card.GetComponent<ItemCardUI>();

            cardUI.Setup(items[index].itemName, items[index].price, items[index].icon, () => AddItemToCart(index));
        }
    }

    public void AddItemToCart(int index)
    {
        ShopItem item = items[index];
        ShoppingCart.Instance.AddItem(item.itemName, item.price, item.icon, (spawnIndex) => SpawnMachineBox(item, spawnIndex));
    }

    // Donde aparece el pedido. Si no hay punto de entrega puesto en la escena,
    // se deja al lado del jugador en vez de reventar despues de cobrar.
    void GetDeliverySpot(int spawnIndex, out Vector3 position, out Quaternion rotation)
    {
        Transform reference = spawnPoint;

        if (reference == null)
        {
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            reference = player != null ? player.transform : transform;

            Debug.LogWarning("[ShopManager] Sin spawnPoint asignado: entrego el pedido junto al jugador.", this);
        }

        Vector3 side = reference.right;
        side.y = 0f;

        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;

        Vector3 forward = reference.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

        // Delante del jugador para no dejarle la caja dentro del cuerpo.
        Vector3 origin = spawnPoint != null
            ? reference.position
            : reference.position + forward.normalized * 1.8f;

        position = origin + side.normalized * (spawnIndex * 1.5f);

        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.6f;
        }

        rotation = Quaternion.Euler(0f, reference.eulerAngles.y, 0f);
    }

    void SpawnMachineBox(ShopItem item, int spawnIndex)
    {
        Vector3 pos;
        Quaternion rot;
        GetDeliverySpot(spawnIndex, out pos, out rot);

        GameObject box = Instantiate(item.boxPrefab, pos, rot);

        PickupBox pickupBox = box.GetComponent<PickupBox>();
        if (pickupBox != null) pickupBox.machinePrefab = item.machinePrefab;

        // Montar maquina nueva es lo que mas hace crecer la tienda.
        LevelManager levels = LevelManager.EnsureExists();
        if (levels != null) levels.Add(levels.xpMachineBought);
    }

    public void AddToyToCart(int index)
    {
        ToyShopItem item = toyItems[index];
        ShoppingCart.Instance.AddItem(item.itemName, item.price, item.icon, (spawnIndex) => SpawnToyBox(item, spawnIndex));
    }

    void SpawnToyBox(ToyShopItem item, int spawnIndex)
    {
        Vector3 pos;
        Quaternion rot;
        GetDeliverySpot(spawnIndex, out pos, out rot);

        // Cada juguete puede traer su propia caja; si no, la mediana de siempre.
        GameObject prefab = item.boxPrefab != null ? item.boxPrefab : toyBoxPrefab;

        if (prefab == null)
        {
            Debug.LogError("[ShopManager] \"" + item.itemName + "\" no tiene caja asignada.", this);
            return;
        }

        GameObject box = Instantiate(prefab, pos, rot);

        ToyBox toyBox = box.GetComponent<ToyBox>();
        if (toyBox == null) return;

        toyBox.toyPrefab = item.toyPrefab;
        toyBox.toyCount = item.boxToyCount;

        LevelManager levels = LevelManager.EnsureExists();
        if (levels != null) levels.Add(levels.xpToyBought);
    }
}
