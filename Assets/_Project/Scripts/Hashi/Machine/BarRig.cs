using UnityEngine;

namespace Hashi
{
    // Las dos barras paralelas sobre las que se apoya el premio.
    //
    // ORIENTACION, que es lo primero que hay que tener claro: las barras van de
    // IZQUIERDA A DERECHA (eje X), o sea de lado a lado mirando la maquina de
    // frente, y estan separadas en PROFUNDIDAD (eje Z). Una queda cerca del
    // cristal y otra al fondo, y el hueco por el que cae el premio se aleja del
    // jugador. Es como estan las de verdad, y de ahi sale todo lo demas: la caja
    // gira sobre el eje X, y por eso las pinzas tienen que empujar en Z.
    //
    // Es la pieza que define el juego entero. El hueco libre entre las dos
    // barras es lo unico que decide si una caja puede caer o no: si el lado mas
    // corto de la caja no cabe por ese hueco, no hay jugada posible por bien que
    // se juegue, y desde fuera parece que la maquina esta rota. Por eso aqui se
    // mide el hueco y se avisa cuando una caja no cabe, en vez de dejarlo a ojo.
    //
    // Cada barra es un objeto vacio con el CapsuleCollider (sin escalar, para
    // que el collider no salga deformado) y una malla de cilindro dentro solo
    // para verse. Poner el collider en la misma pieza que la malla obliga a
    // escalarla en dos ejes y PhysX acaba con una capsula que no es la que se ve.
    [ExecuteAlways]
    public class BarRig : MonoBehaviour
    {
        [Header("Piezas")]
        [Tooltip("La barra de delante, la del lado del cristal.")]
        [SerializeField] Transform leftBar;

        [Tooltip("La barra del fondo.")]
        [SerializeField] Transform rightBar;

        [Header("Medidas (metros)")]
        [Tooltip("Separacion en PROFUNDIDAD entre los ejes de las dos barras. "
                 + "El hueco libre es esto menos dos radios.")]
        [Range(0.05f, 0.40f)] [SerializeField] float barDistance = 0.17f;

        [Tooltip("Radio de la barra. 8 mm es una barra de maquina de verdad.")]
        [Range(0.002f, 0.03f)] [SerializeField] float barRadius = 0.008f;

        [Tooltip("Altura de los ejes de las barras sobre el suelo del cajon.")]
        [Range(0.05f, 1f)] [SerializeField] float barHeight = 0.30f;

        [Tooltip("Largo de las barras, de lado a lado de la maquina.")]
        [Range(0.1f, 2f)] [SerializeField] float barLength = 0.78f;

        [Header("Depuracion")]
        [SerializeField] bool mostrarGizmos = true;

        // ------------------------------------------------------------ lectura

        public float BarDistance => barDistance;
        public float BarRadius => barRadius;
        public float BarHeight => barHeight;
        public float BarLength => barLength;

        // El hueco por el que tiene que pasar la caja, de superficie a
        // superficie. Es el numero que de verdad importa.
        public float HuecoLibre => Mathf.Max(0f, barDistance - 2f * barRadius);

        // Altura a la que se apoya una caja: la parte de arriba de las barras.
        public float AlturaApoyo => barHeight + barRadius;

        // Centro del hueco en coordenadas de mundo, a la altura del apoyo.
        public Vector3 CentroApoyo =>
            transform.TransformPoint(new Vector3(0f, AlturaApoyo, 0f));

        // ------------------------------------------------------------ montaje

        void OnEnable() { Aplicar(); }

        void OnValidate()
        {
            // La capsula no admite una altura menor que su propio diametro, y
            // Unity la recorta por su cuenta sin decir nada.
            barLength = Mathf.Max(barLength, 2f * barRadius);
            Aplicar();
        }

        // Coloca las dos barras segun las medidas. Se puede llamar en cualquier
        // momento: la dificultad cambia barDistance en caliente.
        public void Aplicar()
        {
            Colocar(leftBar, -barDistance * 0.5f);
            Colocar(rightBar, +barDistance * 0.5f);
        }

        public void AplicarSeparacion(float nuevaDistancia)
        {
            barDistance = Mathf.Max(2f * barRadius, nuevaDistancia);
            Aplicar();
        }

        void Colocar(Transform barra, float z)
        {
            if (barra == null) return;

            barra.localPosition = new Vector3(0f, barHeight, z);
            barra.localRotation = Quaternion.identity;
            barra.localScale = Vector3.one;

            CapsuleCollider col = barra.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.direction = 0;              // eje X: la barra va de lado a lado
                col.radius = barRadius;
                col.height = barLength;
                col.center = Vector3.zero;
            }

            // La malla es un cilindro de Unity: mide 2 de alto en Y a escala 1,
            // asi que la escala en Y es la MITAD del largo, y hay que tumbarlo
            // sobre el eje X girandolo en Z.
            Transform malla = barra.childCount > 0 ? barra.GetChild(0) : null;
            if (malla != null)
            {
                malla.localPosition = Vector3.zero;
                malla.localRotation = Quaternion.Euler(0f, 0f, 90f);
                malla.localScale = new Vector3(barRadius * 2f, barLength * 0.5f,
                                               barRadius * 2f);
            }
        }

        // Le dice a quien pregunte si una caja de este tamano puede llegar a
        // caer. Se usa en el spawner y en el constructor de la escena.
        // Las dos condiciones no son intercambiables, y confundirlas cuesta un
        // rato de "por que no cae nunca":
        //
        //   - Para APOYARSE, lo que cuenta es la Z, el fondo de la caja, que es
        //     el lado que cruza las dos barras. La X va a lo largo de ellas y no
        //     sujeta nada.
        //   - Para CAER, lo que cuenta es la Y. La caja pasa por el hueco
        //     girando sobre el eje de las barras hasta ponerse de canto, y
        //     entonces lo que tiene que pasar por el hueco es su altura.
        public bool CabePorElHueco(Vector3 tamanoCaja, out string motivo)
        {
            if (tamanoCaja.y >= HuecoLibre)
            {
                motivo = "de canto sigue midiendo " + tamanoCaja.y.ToString("0.000")
                         + " m y el hueco es de " + HuecoLibre.ToString("0.000")
                         + " m: por bien que se juegue nunca caeria";
                return false;
            }

            if (tamanoCaja.z <= barDistance)
            {
                motivo = "mide " + tamanoCaja.z.ToString("0.000")
                         + " m de fondo y las barras estan a "
                         + barDistance.ToString("0.000")
                         + " m: no llega a apoyar y se cae sola al aparecer";
                return false;
            }

            motivo = null;
            return true;
        }

        // ------------------------------------------------------------- gizmos

        void OnDrawGizmos()
        {
            if (!mostrarGizmos) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            // Las dos barras.
            Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.9f);
            DibujarBarra(-barDistance * 0.5f);
            DibujarBarra(+barDistance * 0.5f);

            // El hueco por el que tiene que pasar el premio, que es lo que hay
            // que mirar cuando algo no cae.
            Gizmos.color = new Color(1f, 0.45f, 0.75f, 0.9f);
            float media = HuecoLibre * 0.5f;
            float x = barLength * 0.5f;
            Gizmos.DrawLine(new Vector3(-x, AlturaApoyo, -media), new Vector3(x, AlturaApoyo, -media));
            Gizmos.DrawLine(new Vector3(-x, AlturaApoyo, media), new Vector3(x, AlturaApoyo, media));
            Gizmos.DrawLine(new Vector3(0f, AlturaApoyo, -media), new Vector3(0f, AlturaApoyo, media));
        }

        void DibujarBarra(float z)
        {
            float x = barLength * 0.5f - barRadius;

            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 d = new Vector3(0f, Mathf.Sin(a) * barRadius, Mathf.Cos(a) * barRadius);
                Gizmos.DrawLine(new Vector3(-x, barHeight, z) + d,
                                new Vector3(x, barHeight, z) + d);
            }

            Gizmos.DrawWireSphere(new Vector3(-x, barHeight, z), barRadius);
            Gizmos.DrawWireSphere(new Vector3(x, barHeight, z), barRadius);
        }
    }
}
