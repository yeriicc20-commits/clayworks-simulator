using UnityEngine;

// Las ampliaciones del local. Se compran en orden y cada una cuesta mas que la
// anterior, con el salto creciendo tambien: 350, 750, 1250, 1850...
public class ExpansionManager : MonoBehaviour
{
    public static ExpansionManager Instance;

    [Header("Precios")]
    public int levels = 20;
    public int firstPrice = 350;
    [Tooltip("Cuanto sube la segunda respecto a la primera.")]
    public int firstIncrement = 400;
    [Tooltip("Cuanto crece el salto en cada nivel.")]
    public int incrementGrowth = 100;

    [Tooltip("Ampliaciones ya compradas.")]
    public int currentLevel = 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // El local necesita saber redimensionarse antes de la primera compra.
        LocalLayout.EnsureExists();
    }

    public static ExpansionManager EnsureExists()
    {
        if (Instance != null) return Instance;

        ExpansionManager existing = FindAnyObjectByType<ExpansionManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("ExpansionManager").AddComponent<ExpansionManager>();
    }

    public int PriceFor(int level)
    {
        if (level < 1) return 0;

        int price = firstPrice;
        int increment = firstIncrement;

        for (int i = 2; i <= level; i++)
        {
            price += increment;
            increment += incrementGrowth;
        }

        return price;
    }

    public bool IsBought(int level)
    {
        return level <= currentLevel;
    }

    // Solo se puede comprar la siguiente: nada de saltarse ampliaciones.
    public bool IsAvailable(int level)
    {
        return level == currentLevel + 1;
    }

    public bool Buy(int level)
    {
        if (!IsAvailable(level)) return false;
        if (GameManager.Instance == null) return false;

        if (!GameManager.Instance.SpendMoney(PriceFor(level))) return false;

        currentLevel = level;

        // Aqui es donde la ampliacion deja de ser un numero: el local crece.
        LocalLayout layout = LocalLayout.EnsureExists();
        if (layout != null) layout.ApplyLevel(currentLevel);

        return true;
    }
}
