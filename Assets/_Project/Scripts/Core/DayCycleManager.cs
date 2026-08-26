using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DayRecord
{
    public int day;
    public float earned;
    public float spent;
    public float moneyAtStart;
    public float moneyAtEnd;
    public int unhappyCustomers;
}

public class DayCycleManager : MonoBehaviour
{
    public static DayCycleManager Instance;

    [Header("Reloj")]
    [Tooltip("Cuanto dura un dia en segundos reales. 7 minutos = 420.")]
    public float dayDurationSeconds = 420f;
    public int startHour = 8;
    public int endHour = 21;

    // Las teclas ya no son campos: las elige el jugador en el menu de
    // ajustes. Se dejan como propiedades con el mismo nombre para que todo
    // lo que las usa siga leyendose igual, carteles de ayuda incluidos.
    static KeyCode openKey { get { return AjustesControles.Tecla(AjustesControles.Accion.AbrirLocal); } }
    static KeyCode closeKey { get { return AjustesControles.Tecla(AjustesControles.Accion.CerrarLocal); } }

    [Header("Jugador")]
    [Tooltip("Donde aparece el jugador cada manana. Vacio = donde empezo la partida.")]
    public Transform playerStartPoint;
    public bool movePlayerAtDayStart = true;

    [Header("Resumen")]
    [Tooltip("Segundos entre linea y linea del resumen.")]
    public float lineRevealDelay = 0.28f;
    [Tooltip("Tirar los peluches que queden por el suelo al cerrar.")]
    public bool clearLoosePlushes = true;

    [Header("UI")]
    public TextMeshProUGUI clockText;
    public TextMeshProUGUI hintText;
    public GameObject summaryPanel;
    public TextMeshProUGUI summaryText;

    public int CurrentDay { get; private set; }
    public bool DayRunning { get; private set; }
    public bool DayFinished { get; private set; }
    public bool ShowingSummary { get; private set; }

    public readonly List<DayRecord> History = new List<DayRecord>();

    private float elapsed;
    private float earnedToday;
    private float spentToday;
    private float moneyAtDayStart;
    private int unhappyToday;

    private Transform playerTransform;
    private Coroutine revealRoutine;
    private bool revealing = false;
    private readonly List<string> summaryLines = new List<string>();
    private Vector3 defaultPlayerPosition;
    private Quaternion defaultPlayerRotation;

    void Awake()
    {
        Instance = this;
        CurrentDay = 1;
    }

    void Start()
    {
        if (summaryPanel != null) summaryPanel.SetActive(false);

        FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
        if (player != null)
        {
            playerTransform = player.transform;
            defaultPlayerPosition = playerTransform.position;
            defaultPlayerRotation = playerTransform.rotation;
        }

        BeginDay();
    }

    // Cada manana el jugador amanece fuera, como si llegara a abrir.
    void SendPlayerToStart()
    {
        if (!movePlayerAtDayStart || playerTransform == null) return;

        Vector3 position = playerStartPoint != null ? playerStartPoint.position : defaultPlayerPosition;
        Quaternion rotation = playerStartPoint != null ? playerStartPoint.rotation : defaultPlayerRotation;

        // El CharacterController pisa la posicion si no lo apagamos antes.
        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        playerTransform.SetPositionAndRotation(position, rotation);

        if (controller != null) controller.enabled = true;
    }

    void BeginDay()
    {
        elapsed = 0f;
        earnedToday = 0f;
        spentToday = 0f;
        unhappyToday = 0;
        moneyAtDayStart = GameManager.Instance != null ? GameManager.Instance.money : 0f;

        DayRunning = false;
        DayFinished = false;
        ShowingSummary = false;

        UpdateClockUI();
        SetHint("Dia " + CurrentDay + " - Pulsa " + AjustesControles.NombreTecla(openKey) + " para abrir el local");
    }

    void Update()
    {
        if (ShowingSummary)
        {
            // La primera pulsacion termina de mostrar el resumen, la segunda
            // pasa de dia. Asi no se salta sin querer con un doble Enter.
            if (AjustesControles.Pulsando(AjustesControles.Accion.CerrarLocal))
            {
                if (revealing) RevealEverything();
                else CloseSummary();
            }

            return;
        }

        if (!DayRunning && !DayFinished)
        {
            if (AjustesControles.Pulsando(AjustesControles.Accion.AbrirLocal)) OpenShop();
            return;
        }

        if (DayRunning)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= dayDurationSeconds)
            {
                elapsed = dayDurationSeconds;
                DayRunning = false;
                DayFinished = true;

                // Se cierra: no entra nadie mas y los que quedan dentro se van.
                NPCClawPlayer.SendEveryoneHome();

                SetHint("Cierre - Pulsa " + AjustesControles.NombreTecla(closeKey) + " para terminar el dia");
            }

            UpdateClockUI();
            return;
        }

        if (DayFinished && AjustesControles.Pulsando(AjustesControles.Accion.CerrarLocal))
        {
            ShowSummary();
        }
    }

    void OpenShop()
    {
        DayRunning = true;
        SetHint("");
    }

    // Hora del juego repartida entre startHour y endHour a lo largo del dia real.
    public float CurrentHourFloat
    {
        get
        {
            float progress = dayDurationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / dayDurationSeconds);

            return startHour + progress * (endHour - startHour);
        }
    }

    void UpdateClockUI()
    {
        if (clockText == null) return;

        float current = CurrentHourFloat;

        int hour = Mathf.FloorToInt(current);
        int minute = Mathf.FloorToInt((current - hour) * 60f);

        if (hour >= endHour)
        {
            hour = endHour;
            minute = 0;
        }

        clockText.text = FormatClock(hour, minute);
    }

    public static string FormatClock(int hour, int minute)
    {
        string suffix = hour < 12 ? "AM" : "PM";

        int display = hour % 12;
        if (display == 0) display = 12;

        return display + ":" + minute.ToString("00") + " " + suffix;
    }

    void SetHint(string message)
    {
        if (hintText == null) return;

        hintText.text = message;
        hintText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    public void ReportEarned(float amount)
    {
        if (amount > 0) earnedToday += amount;
    }

    public void ReportSpent(float amount)
    {
        if (amount > 0) spentToday += amount;
    }

    // Un cliente que se ha ido sin jugar porque le parecio caro.
    public void ReportUnhappyCustomer()
    {
        unhappyToday++;
    }

    void ShowSummary()
    {
        DayFinished = false;
        ShowingSummary = true;

        float moneyNow = GameManager.Instance != null ? GameManager.Instance.money : 0f;

        DayRecord record = new DayRecord();
        record.day = CurrentDay;
        record.earned = earnedToday;
        record.spent = spentToday;
        record.moneyAtStart = moneyAtDayStart;
        record.moneyAtEnd = moneyNow;
        record.unhappyCustomers = unhappyToday;

        History.Add(record);

        // La luz del dia se suma a lo pendiente; se paga desde el ordenador.
        EconomyManager economy = EconomyManager.EnsureExists();
        float todaysBill = economy.CurrentDailyBill;

        economy.ChargeDay(record.day);

        // Y detras, la cuota del dia de cada prestamo que sigas debiendo.
        BankManager.EnsureExists().ChargeDay(record.day);

        BuildSummaryLines(record, todaysBill, economy.PendingTotal);

        if (summaryText != null) summaryText.text = "";
        if (summaryPanel != null) summaryPanel.SetActive(true);

        SetHint("");

        Time.timeScale = 0f;
        CursorMode.Free(this);

        revealRoutine = StartCoroutine(RevealSummary());
    }

    void BuildSummaryLines(DayRecord record, float todaysBill, float pendingBill)
    {
        float balance = record.earned - record.spent;
        string balanceLabel = (balance >= 0 ? "+" : "") + GameManager.Format(balance);

        summaryLines.Clear();

        summaryLines.Add("<size=54><b>DIA " + record.day + "</b></size>");
        summaryLines.Add("<size=22>Resumen de la jornada</size>");
        summaryLines.Add("");
        summaryLines.Add("Dinero ganado:    <color=#4CAF50>+" + GameManager.Format(record.earned) + "</color>");
        summaryLines.Add("Dinero gastado:   <color=#E53935>-" + GameManager.Format(record.spent) + "</color>");
        summaryLines.Add("Balance del dia:  " + balanceLabel);
        summaryLines.Add("");
        summaryLines.Add("Caja al abrir:    " + GameManager.Format(record.moneyAtStart));
        summaryLines.Add("Caja al cerrar:   " + GameManager.Format(record.moneyAtEnd));
        summaryLines.Add("");
        summaryLines.Add("Luz de hoy:       <color=#E53935>" + GameManager.Format(todaysBill) + "</color>");
        summaryLines.Add("<size=18>Pendiente de pagar: " + GameManager.Format(pendingBill) + " (en el ordenador)</size>");
        summaryLines.Add("");
        summaryLines.Add(UnhappyLine(record.unhappyCustomers));
        summaryLines.Add("");
        summaryLines.Add("<size=20>Pulsa " + AjustesControles.NombreTecla(closeKey) + " para empezar el dia " + (record.day + 1) + "</size>");
    }

    static string UnhappyLine(int count)
    {
        if (count == 0) return "Clientes insatisfechos:  <color=#4CAF50>0</color>";

        return "Clientes insatisfechos:  <color=#E53935>" + count + "</color>"
             + "\n<size=18><color=#888888>Se fueron sin jugar por el precio</color></size>";
    }

    // Las lineas van cayendo de arriba abajo. En tiempo real, que el juego esta
    // pausado y con Time.timeScale a 0 un WaitForSeconds normal no avanzaria.
    IEnumerator RevealSummary()
    {
        revealing = true;

        string shown = "";

        foreach (string line in summaryLines)
        {
            shown += line + "\n";

            if (summaryText != null) summaryText.text = shown;

            yield return new WaitForSecondsRealtime(lineRevealDelay);
        }

        revealing = false;
        revealRoutine = null;
    }

    void RevealEverything()
    {
        if (revealRoutine != null) StopCoroutine(revealRoutine);

        revealRoutine = null;
        revealing = false;

        if (summaryText != null) summaryText.text = string.Join("\n", summaryLines);
    }

    // Nada cruza de un dia a otro: ni clientes rezagados ni peluches por el suelo.
    void CleanUpForNewDay()
    {
        NPCClawPlayer.DespawnEveryone();

        if (!clearLoosePlushes) return;

        PlushItem[] plushes = FindObjectsByType<PlushItem>(FindObjectsInactive.Exclude);

        foreach (PlushItem plush in plushes)
        {
            if (plush == null) continue;

            // Los que estan dentro de una maquina son su stock, no basura.
            if (IsInsideAnyMachine(plush.transform.position)) continue;

            Destroy(plush.gameObject);
        }
    }

    bool IsInsideAnyMachine(Vector3 position)
    {
        foreach (ClawController machine in ClawController.AllMachines)
        {
            if (machine == null) continue;

            if (machine.MachineBounds.Contains(position)) return true;
        }

        return false;
    }

    void CloseSummary()
    {
        if (summaryPanel != null) summaryPanel.SetActive(false);

        Time.timeScale = 1f;
        CursorMode.Release(this);

        CleanUpForNewDay();

        SendPlayerToStart();

        CurrentDay++;
        BeginDay();
    }
}
