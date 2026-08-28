using UnityEngine;

namespace Hashi
{
    // El mando de fuerza del cuadro de servicio, en la trasera de la maquina.
    //
    // Hace lo mismo y se maneja igual que ClawStrengthDial, el de la maquina de
    // garra: rueda del raton o flechas, con la aguja marcando la fuerza que la
    // maquina esta aplicando de verdad. Dos maquinas del mismo local que se
    // regulan distinto son dos maquinas que hay que aprender dos veces.
    //
    // Va DETRAS, mirando a la pared, como en las de verdad: es del dueno, no del
    // jugador.
    public class HashiStrengthDial : MonoBehaviour
    {
        [SerializeField] ClawStrength motor;

        [Tooltip("La aguja de la esfera. Su origen esta en el eje, asi que basta "
                 + "con girarla.")]
        [SerializeField] Transform aguja;

        [Tooltip("Sobre que eje gira la aguja, en su espacio local.")]
        [SerializeField] Vector3 ejeAguja = Vector3.forward;

        [Header("Escala")]
        [SerializeField] float anguloMin = -120f;
        [SerializeField] float anguloMax = 120f;

        [Tooltip("Cuanto cambia por muesca de rueda.")]
        [SerializeField] float paso = 0.05f;

        bool cerca;
        Quaternion agujaReposo;
        float mostrado = -1f;

        void Awake()
        {
            if (aguja != null) agujaReposo = aguja.localRotation;
            if (motor == null) motor = GetComponentInParent<ClawStrength>();
        }

        void Start()
        {
            ColocarAguja();
        }

        void Update()
        {
            if (motor == null) return;

            // La aguja marca la fuerza EFECTIVA, no solo lo que pusiste en el
            // mando: sube sola con cada partida sin premio y cae de golpe cuando
            // la maquina paga. Asi el cuadro cuenta lo que la maquina esta
            // haciendo, en vez de repetir lo que ya sabes que ajustaste.
            if (!Mathf.Approximately(mostrado, motor.FuerzaEfectiva)) ColocarAguja();

            if (!cerca || PricePanel.IsOpen) return;

            float rueda = Input.GetAxis("Mouse ScrollWheel");
            float teclas = 0f;

            if (Input.GetKeyDown(KeyCode.RightArrow)) teclas = 1f;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) teclas = -1f;

            float delta = (Mathf.Abs(rueda) > 0.01f ? Mathf.Sign(rueda) : teclas) * paso;

            if (Mathf.Abs(delta) > 0.0001f)
            {
                motor.ajuste = Mathf.Clamp01(motor.ajuste + delta);
                ColocarAguja();
            }

            InteractionUI.Prompt(string.Format(
                "Mando: {0:F0}%   ·   ahora aprieta al {1:F0}% ({2} sin premio)   ·   rueda o flechas",
                motor.ajuste * 100f, motor.FuerzaEfectiva * 100f,
                motor.jugadasSinPremio));
        }

        void ColocarAguja()
        {
            if (motor == null) return;

            mostrado = motor.FuerzaEfectiva;

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
}
