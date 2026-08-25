using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // En decimal: los precios van con centimos (5,05€) y con enteros se perdian.
    public float money = 10f;
    public TextMeshProUGUI moneyText;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // El mezclador de sonido, listo desde el primer fotograma.
        PanelSonido.EnsureExists();

        UpdateMoneyUI();

        // El nivel y su barra se montan solos al lado del dinero.
        LevelHUD.EnsureExists();
    }

    // Formato unico para todo el juego, para no tener cada sitio el suyo.
    public static string Format(float amount)
    {
        return amount.ToString("0.00") + "€";
    }

    public bool SpendMoney(float amount)
    {
        // Margen de un centimo: comparar floats a pelo deja compras imposibles
        // cuando tienes justo el importe exacto.
        if (money + 0.001f < amount) return false;

        money -= amount;

        if (DayCycleManager.Instance != null) DayCycleManager.Instance.ReportSpent(amount);

        UpdateMoneyUI();
        return true;
    }

    public void AddMoney(float amount)
    {
        money += amount;

        if (DayCycleManager.Instance != null) DayCycleManager.Instance.ReportEarned(amount);

        UpdateMoneyUI();
    }

    void UpdateMoneyUI()
    {
        if (moneyText != null) moneyText.text = "Dinero: " + Format(money);
    }
}
