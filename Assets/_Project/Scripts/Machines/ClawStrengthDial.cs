using UnityEngine;

// Mando de fuerza del cuadro de servicio, en la trasera de la maquina.
//
// Es lo que regula el dueno de un salon recreativo de verdad: no la dificultad
// del juego, sino cuanta corriente recibe el motor de la garra. Poca corriente
// y la garra roza el peluche sin poder con el; mucha y lo levanta casi siempre.
// De ahi que este DETRAS, mirando a la pared: es del dueno, no del jugador.
//
// Aqui el dueno eres tu, asi que se regula igual pero desde el juego.
public class ClawStrengthDial : MonoBehaviour
{
    public ClawFingerMotors motores;

    [Tooltip("La aguja de la esfera. Su origen esta en el eje, asi que basta con "
             + "girarla.")]
    public Transform aguja;

    [Tooltip("Sobre que eje gira la aguja, en su espacio local.")]
    public Vector3 ejeAguja = Vector3.forward;

    [Header("Escala")]
    public float anguloMin = -120f;
    public float anguloMax = 120f;

    [Tooltip("Cuanto cambia por muesca de rueda.")]
    public float paso = 0.05f;

    private bool cerca = false;
    private Quaternion agujaReposo;
    private float mostrado = -1f;

    void Awake()
    {
        if (aguja != null) agujaReposo = aguja.localRotation;
        if (motores == null) motores = GetComponentInParent<ClawFingerMotors>();
    }

    void Start()
    {
        ColocarAguja();
    }

    void Update()
    {
        if (motores == null) return;

        // La aguja marca la fuerza EFECTIVA, no solo lo que pusiste en el
        // mando: sube sola con cada partida sin premio y cae de golpe cuando la
        // maquina paga. Asi el cuadro cuenta lo que esta haciendo la maquina en
        // vez de repetir lo que ya sabes que ajustaste.
        if (!Mathf.Approximately(mostrado, motores.FuerzaEfectiva)) ColocarAguja();

        if (!cerca) return;
        if (PricePanel.IsOpen) return;

        float rueda = Input.GetAxis("Mouse ScrollWheel");
        float teclas = 0f;

        if (Input.GetKeyDown(KeyCode.RightArrow)) teclas = 1f;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) teclas = -1f;

        float delta = (Mathf.Abs(rueda) > 0.01f ? Mathf.Sign(rueda) : teclas) * paso;

        if (Mathf.Abs(delta) > 0.0001f)
        {
            motores.ajuste = Mathf.Clamp01(motores.ajuste + delta);
            ColocarAguja();
        }

        InteractionUI.Prompt(string.Format(
            "Mando: {0:F0}%   ·   ahora aprieta al {1:F0}% ({2} sin premio)   ·   rueda o flechas",
            motores.ajuste * 100f, motores.FuerzaEfectiva * 100f,
            motores.jugadasSinPremio));
    }

    void ColocarAguja()
    {
        if (motores == null) return;

        mostrado = motores.FuerzaEfectiva;

        if (aguja == null) return;

        float ang = Mathf.Lerp(anguloMin, anguloMax, mostrado);
        aguja.localRotation = agujaReposo * Quaternion.AngleAxis(ang, ejeAguja);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) cerca = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        cerca = false;
        InteractionUI.Hide();
    }
}
