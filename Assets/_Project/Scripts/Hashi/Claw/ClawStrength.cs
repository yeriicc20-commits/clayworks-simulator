using UnityEngine;

namespace Hashi
{
    // La fuerza del motor de los brazos, con el mando del cuadro trasero.
    //
    // Es la misma idea que ClawFingerMotors en la maquina de garra, y a
    // proposito: el dueno de un salon no regula "la dificultad", regula cuanta
    // corriente le llega al motor. Poca y los brazos empujan la caja sin poder
    // moverla; mucha y la vuelcan al primer intento. Que las dos maquinas se
    // regulen igual es medio local.
    //
    // Y como en una recreativa de verdad, la fuerza no es fija: sube un poco con
    // cada partida sin premio y vuelve a cero cuando la maquina paga. Es lo que
    // hace que se recuerde como "llevaba diez sin dar nada y de pronto solto
    // una", en vez de como una moneda al aire.
    public class ClawStrength : MonoBehaviour
    {
        [Header("Par de los brazos, en newton-metro")]
        [Tooltip("Lo minimo que puede tocar en una partida. Con esto la caja se "
                 + "mueve un dedo y poco mas: es la partida que no paga.")]
        [SerializeField] float torqueMin = 0.08f;

        [Tooltip("Par al 80% del mando. De ahi para arriba sube hasta torqueMax.")]
        [SerializeField] float torqueNormal = 0.30f;

        [Tooltip("Par al 100% del mando. Lo que hace falta para girar la caja "
                 + "mas pesada en dos o tres intentos, no para volcarla de un "
                 + "golpe: pasado esto la caja sale despedida y se ve falso.")]
        [SerializeField] float torqueMax = 0.55f;

        [Tooltip("A partir de que punto del mando se deja de sortear la fuerza "
                 + "y manda el maximo.")]
        [Range(0.5f, 1f)] [SerializeField] float mandoFirme = 0.8f;

        [Tooltip("Cuantas partidas de cada diez salen con fuerza de sobra.")]
        [Range(0f, 1f)] [SerializeField] float generosidad = 0.12f;

        [Header("Mando del cuadro trasero")]
        [Tooltip("Lo que marca el mando, de 0 a 1. Es el punto de partida del "
                 + "que sube con cada partida sin premio.")]
        [Range(0f, 1f)] public float ajuste = 0.35f;

        [Header("Progresion entre premios")]
        [Tooltip("A las cuantas partidas sin premio llega al maximo del motor.")]
        [SerializeField] int jugadasParaGarantizar = 10;

        [HideInInspector] public int jugadasSinPremio;

        [Header("Referencias")]
        [SerializeField] ClawFingerController pinzas;
        [SerializeField] DropZone bandeja;

        void Awake()
        {
            if (pinzas == null) pinzas = GetComponentInParent<ClawFingerController>();
            if (bandeja == null) bandeja = GetComponentInChildren<DropZone>();
        }

        void OnEnable()
        {
            // Se entera sola de que ha pagado. Dejarselo al GameManager
            // significaria que la maquina se comporta distinto en su escena de
            // pruebas, donde ese GameManager no existe.
            if (bandeja != null) bandeja.AlRecogerPremio += Premiado;
        }

        void OnDisable()
        {
            if (bandeja != null) bandeja.AlRecogerPremio -= Premiado;
        }

        // Techo de par de la proxima partida: parte de lo que marca el mando y
        // sube con cada jugada sin premio.
        public float TechoActual => TechoDe(ajuste);

        public float TechoDe(float mando)
        {
            float a = Mathf.Clamp01(mando);

            // El mando no va en linea recta, tiene un codo. Hasta mandoFirme se
            // mueve por el tramo con el que se juega; el ultimo tramo dispara el
            // par, porque para mover la caja A VECES basta con poco y para
            // girarla SIEMPRE hace falta bastante mas.
            float baseTecho = a <= mandoFirme
                ? Mathf.Lerp(torqueMin, torqueNormal, a / mandoFirme)
                : Mathf.Lerp(torqueNormal, torqueMax, (a - mandoFirme) / (1f - mandoFirme));

            float progreso = jugadasParaGarantizar <= 0
                ? 1f
                : Mathf.Clamp01((float)jugadasSinPremio / jugadasParaGarantizar);

            // Al cuadrado, no en linea recta: subiendo recto la maquina llegaba
            // a pagar por la cuarta partida y el mando dejaba de importar.
            return Mathf.Lerp(baseTecho, torqueMax, progreso * progreso);
        }

        // De 0 a 1, que es lo que marca la aguja de la esfera.
        public float FuerzaEfectiva => Mathf.InverseLerp(torqueMin, torqueMax, TechoActual);

        // El par que toca en esta partida, sorteado bajo el techo. Al 100% no
        // hay loteria: si el dueno ha puesto el maximo, es que quiere el maximo.
        public float ParaEstaPartida()
        {
            float techo = TechoActual;

            if (ajuste >= 0.999f) return techo;
            if (Random.value < generosidad) return techo;

            // El sorteo va del 60% del techo al techo, no desde el minimo:
            // repartiendo desde abajo, ni con el mando alto se notaba la
            // diferencia.
            return Random.Range(techo * 0.6f, techo);
        }

        // La llama la maquina al empezar el turno.
        public float Cobrar()
        {
            jugadasSinPremio++;

            float par = ParaEstaPartida();
            if (pinzas != null) pinzas.PonerFuerza(par);

            return par;
        }

        void Premiado(PrizeController premio)
        {
            jugadasSinPremio = 0;
        }
    }
}
