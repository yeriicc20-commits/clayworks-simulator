using UnityEngine;
using System;

// El nivel de la tienda. Se sube haciendo negocio: cada partida de un cliente
// suma un poco y cada compra bastante mas. Cada nivel pide mas experiencia que
// el anterior, y el salto tambien va creciendo.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Progreso")]
    public int level = 1;
    public float xp = 0f;

    [Header("Cuanto cuesta cada nivel")]
    [Tooltip("Experiencia para pasar del nivel 1 al 2.")]
    public int firstLevelXP = 100;
    [Tooltip("Cuanto mas pide el nivel 3 respecto al 2.")]
    public int xpIncrement = 50;
    [Tooltip("Cuanto crece ese salto en cada nivel.")]
    public int incrementGrowth = 25;

    [Header("Que da experiencia")]
    public int xpNpcPlays = 1;
    public int xpNpcWinsPrize = 2;
    public int xpMachineBought = 10;
    public int xpToyBought = 2;
    public int xpPrizeSold = 3;

    // Para que la barra se entere sin tener que preguntarlo cada frame.
    public event Action Changed;

    void Awake()
    {
        Instance = this;
    }

    public static LevelManager EnsureExists()
    {
        if (Instance != null) return Instance;

        LevelManager existing = FindAnyObjectByType<LevelManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("LevelManager").AddComponent<LevelManager>();
    }

    // Experiencia para pasar de ese nivel al siguiente: 100, 150, 225, 325...
    public int XPForLevel(int from)
    {
        if (from < 1) from = 1;

        int need = firstLevelXP;
        int step = xpIncrement;

        for (int i = 2; i <= from; i++)
        {
            need += step;
            step += incrementGrowth;
        }

        return need;
    }

    public int XPToNext { get { return XPForLevel(level); } }

    public float Progress
    {
        get
        {
            int need = XPToNext;

            return need <= 0 ? 0f : Mathf.Clamp01(xp / need);
        }
    }

    public void Add(float amount)
    {
        if (amount <= 0f) return;

        xp += amount;

        // El "+1 XP" que asoma al lado de la barra. Va antes de subir de nivel
        // para que se vea lo que has ganado aunque la barra se reinicie.
        if (LevelHUD.Instance != null) LevelHUD.Instance.ShowGain(amount);

        // En bucle: un premio gordo puede saltar mas de un nivel de golpe.
        while (xp >= XPToNext)
        {
            xp -= XPToNext;
            level++;

            if (NotificationManager.Instance != null)
            {
                NotificationManager.Instance.ShowMessage("¡Tienda de nivel " + level + "!");
            }
        }

        if (Changed != null) Changed();
    }

    // Atajos para no tener que comprobar el singleton en cada sitio.
    public static void Award(int amount)
    {
        LevelManager manager = EnsureExists();

        if (manager != null) manager.Add(amount);
    }
}
