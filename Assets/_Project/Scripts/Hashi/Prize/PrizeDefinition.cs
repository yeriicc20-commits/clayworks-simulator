using UnityEngine;

namespace Hashi
{
    // Una caja de premio intercambiable.
    //
    // Cada asset de estos es una caja distinta: la pequena que cae sola, la
    // larga que cuesta girar porque tiene mucha inercia en el eje de las barras,
    // la pesada con el centro de masas por los suelos... La dificultad de cada
    // una sale solo de estos numeros, no de ningun ajuste escondido.
    [CreateAssetMenu(fileName = "Premio_Nuevo", menuName = "Hashi/Premio", order = 1)]
    public class PrizeDefinition : ScriptableObject
    {
        [Header("Identificacion")]
        public string nombre = "Caja";

        [TextArea(2, 4)]
        [Tooltip("Por que esta caja es facil o dificil.")]
        public string notas = "";

        [Header("Forma (metros)")]
        [Tooltip("Las barras van de lado a lado (X) y estan separadas en "
                 + "profundidad (Z), asi que:\n"
                 + "X = largo, corre a lo largo de las barras y no sujeta nada.\n"
                 + "Y = alto, es el lado que pasa por el hueco al volcar: tiene "
                 + "que ser MENOR que el hueco libre.\n"
                 + "Z = fondo, es el lado que cruza las dos barras: tiene que "
                 + "ser MAYOR que la separacion entre ellas.")]
        public Vector3 size = new Vector3(0.17f, 0.115f, 0.22f);

        [Header("Fisica")]
        [Range(0.05f, 5f)] public float mass = 0.40f;

        [Tooltip("Rozamiento del aire. Casi cero: una caja no flota.")]
        [Range(0f, 2f)] public float linearDamping = 0.02f;

        [Tooltip("Frena el giro. Poco, o la caja se para a medio volcar y no "
                 + "termina de caer, que es de las cosas que peor sientan.")]
        [Range(0f, 2f)] public float angularDamping = 0.06f;

        [Tooltip("Desplazamiento del centro de masas respecto al centro de la "
                 + "caja, en metros. Es LO que decide si vuelca facil.")]
        public Vector3 centerOfMassOffset = Vector3.zero;

        [Tooltip("Rozamiento contra las barras.")]
        [Range(0.02f, 1.2f)] public float friction = 0.42f;

        [Tooltip("Rebote. Casi cero: el carton no bota.")]
        [Range(0f, 1f)] public float bounciness = 0.02f;

        [Header("Aspecto")]
        public Color color = new Color(0.25f, 0.45f, 0.85f);

        [Tooltip("Opcional. Se pone en la cara de delante de la caja.")]
        public Texture2D ilustracion;
    }
}
