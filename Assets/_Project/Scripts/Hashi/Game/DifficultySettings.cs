using UnityEngine;

namespace Hashi
{
    // Ajustes de dificultad de la maquina de puente (hashi-watashi).
    //
    // No es una tabla de adorno: cada valor entra directo en la fisica. Subir la
    // dificultad no hace que "salga menos veces", hace que la caja sea mas
    // estable de verdad (mas masa, mas rozamiento, centro de masas mas bajo) y
    // que la garra empuje con menos fuerza. Asi lo que aprende el jugador en
    // Facil le sigue valiendo en Extremo, solo que con menos margen.
    [CreateAssetMenu(fileName = "Dificultad_Normal",
                     menuName = "Hashi/Ajustes de dificultad", order = 0)]
    public class DifficultySettings : ScriptableObject
    {
        [Header("Identificacion")]
        [Tooltip("Lo que se ensena en la interfaz.")]
        public string nombre = "Normal";

        [TextArea(2, 4)]
        [Tooltip("Para acordarse de por que este preajuste es mas dificil.")]
        public string notas = "";

        [Header("Garra: pinzas")]
        [Tooltip("Par del motor de la bisagra, en newton-metro. Es lo que puede "
                 + "empujar cada pinza antes de que la caja la frene.")]
        [Range(0.05f, 10f)] public float clawGripForce = 0.45f;

        [Tooltip("Velocidad de cierre de las pinzas, en grados por segundo. "
                 + "Cerrar rapido da un golpe seco; cerrar lento empuja.")]
        [Range(20f, 400f)] public float clawCloseSpeed = 120f;

        [Header("Garra: movimiento")]
        [Tooltip("Velocidad horizontal del carro, en metros por segundo.")]
        [Range(0.05f, 2f)] public float clawMoveSpeed = 0.35f;

        [Tooltip("Velocidad de bajada, en metros por segundo.")]
        [Range(0.05f, 2f)] public float dropSpeed = 0.30f;

        [Tooltip("Velocidad de subida, en metros por segundo.")]
        [Range(0.05f, 2f)] public float riseSpeed = 0.35f;

        [Header("Premio")]
        // Estos dos son FACTORES sobre lo que traiga cada caja, no valores
        // fijos. Puestos fijos, las cinco cajas pasarian a pesar lo mismo y a
        // rozar igual, y sobrarian cuatro: la larga, la pesada y la ligera se
        // juegan distinto precisamente porque no comparten numeros.
        [Tooltip("Multiplica la masa de la caja. 1 = la suya. Mas masa = mas "
                 + "cuesta girarla.")]
        [Range(0.2f, 4f)] public float prizeMass = 1f;

        [Tooltip("Multiplica el rozamiento contra las barras. 1 = el suyo. "
                 + "Mucho rozamiento hace que la caja pivote en vez de "
                 + "resbalar; poco, que patine sin girar.")]
        [Range(0.2f, 3f)] public float prizeFriction = 1f;

        [Tooltip("Se SUMA al centro de masas que traiga la caja, en metros. "
                 + "Bajarlo (Y negativa) la vuelve casi invencible; subirlo la "
                 + "hace volcar casi sola.")]
        public Vector3 centerOfMassOffset = Vector3.zero;

        [Header("Barras")]
        [Tooltip("Separacion entre los ejes de las dos barras, en metros. "
                 + "Cuanto mas juntas, menos hueco para que pase la caja.")]
        [Range(0.05f, 0.40f)] public float barDistance = 0.17f;

        [Header("Partida")]
        [Tooltip("Segundos de turno antes de que la garra baje sola. 0 = sin limite.")]
        [Range(0f, 60f)] public float turnTime = 20f;
    }
}
