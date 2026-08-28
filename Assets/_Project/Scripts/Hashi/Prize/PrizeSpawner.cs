using System;
using UnityEngine;

namespace Hashi
{
    // Pone la caja sobre las barras.
    //
    // Coloca, y nada mas. Ni la mueve durante la partida ni la devuelve a su
    // sitio cuando el jugador falla: lo que el jugador ha conseguido girar en un
    // intento sigue girado en el siguiente, que es de lo que va este juego.
    // Volver a colocarla es siempre una peticion expresa (el boton de reiniciar
    // o el menu de pruebas), nunca algo que pase solo.
    public class PrizeSpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Prefab de la caja. Tiene que llevar PrizeController.")]
        [SerializeField] PrizeController prefab;

        [Tooltip("Donde se coloca. Solo se usa su X y su Z: la altura la manda "
                 + "la de las barras, para que la caja apoye y no aparezca "
                 + "flotando ni metida dentro.")]
        [SerializeField] Transform puntoDeAparicion;

        [SerializeField] BarRig barras;

        [Header("Cajas disponibles")]
        [SerializeField] PrizeDefinition[] modelos;

        [Tooltip("Cual se pone al empezar. -1 = una al azar.")]
        [SerializeField] int modeloInicial = 1;

        [SerializeField] bool generarAlArrancar = true;

        [Header("Colocacion")]
        [Tooltip("Cuanto se deja la caja por encima de las barras al aparecer. "
                 + "Un pelo, para que caiga y se asiente ella sola en vez de "
                 + "aparecer clavada dentro de la barra.")]
        [Range(0f, 0.02f)] [SerializeField] float holguraApoyo = 0.002f;

        [Tooltip("Giro al azar sobre la vertical, en grados. Un poco de "
                 + "desorden hace que no todas las partidas empiecen igual. "
                 + "Cuidado con pasarse: muy girada, la caja apoya en menos "
                 + "sitio y se cae sola.")]
        [Range(0f, 25f)] [SerializeField] float giroAleatorio = 0f;

        PrizeController actual;
        int indiceActual = -1;

        public event Action<PrizeController> AlGenerar;

        public PrizeController Actual => actual;
        public PrizeDefinition[] Modelos => modelos;
        public int IndiceActual => indiceActual;

        void Start()
        {
            if (generarAlArrancar && actual == null) Generar(modeloInicial);
        }

        // Crea (o reaprovecha) la caja y la deja apoyada sobre las barras.
        public PrizeController Generar(int indice)
        {
            if (prefab == null)
            {
                Debug.LogError("[Hashi] El generador de premios no tiene prefab.");
                return null;
            }

            if (modelos != null && modelos.Length > 0)
            {
                indice = indice < 0
                    ? UnityEngine.Random.Range(0, modelos.Length)
                    : Mathf.Clamp(indice, 0, modelos.Length - 1);
            }
            else
            {
                indice = -1;
            }

            Quitar();

            actual = Instantiate(prefab, transform);
            actual.name = "Prize";
            actual.gameObject.layer = HashiLayers.Premio;
            PonerCapa(actual.transform, HashiLayers.Premio);

            if (indice >= 0)
            {
                actual.Aplicar(modelos[indice]);
                actual.name = "Prize_" + modelos[indice].nombre;
            }

            indiceActual = indice;

            Colocar(actual);
            Avisar(actual);

            AlGenerar?.Invoke(actual);
            return actual;
        }

        public PrizeController GenerarAleatorio() => Generar(-1);

        // Mete un premio traido de fuera: el que sale de una caja comprada en la
        // tienda. Devuelve false si no cabe, y dice por que.
        //
        // Solo cabe UNO. No es una limitacion tecnica, es como es la maquina:
        // sobre dos barras solo se puede apoyar una caja, y dos apoyadas a la
        // vez se estorbarian hasta tirarse solas. Por eso se avisa en vez de
        // dejar meterlas y que pase algo raro.
        public bool MeterPremio(GameObject prefabPremio, out string motivo)
        {
            if (prefabPremio == null)
            {
                motivo = "Esa caja no trae ningun premio";
                return false;
            }

            if (actual != null)
            {
                motivo = "Solo cabe un premio en las barras";
                return false;
            }

            PrizeController traido = prefabPremio.GetComponent<PrizeController>();

            if (traido == null)
            {
                motivo = "Esto no va en la maquina de puente";
                return false;
            }

            actual = Instantiate(traido, transform);
            actual.name = prefabPremio.name;
            PonerCapa(actual.transform, HashiLayers.Premio);

            // Sin aplicar ninguna definicion: la caja que viene de la tienda
            // trae su propio tamano y su propio peso, y son los que valen.
            indiceActual = -1;

            Colocar(actual);
            Avisar(actual);

            AlGenerar?.Invoke(actual);

            motivo = null;
            return true;
        }

        // Si hay sitio para otro premio. Lo usa la caja de la tienda para avisar
        // antes de gastar uno.
        public bool HaySitio => actual == null;

        // Devuelve la caja de ahora a su sitio, sin cambiarla por otra.
        public void Reiniciar()
        {
            if (actual == null)
            {
                Generar(modeloInicial);
                return;
            }

            Colocar(actual);
        }

        public void Quitar()
        {
            if (actual == null) return;

            if (Application.isPlaying) Destroy(actual.gameObject);
            else DestroyImmediate(actual.gameObject);

            actual = null;
        }

        void Colocar(PrizeController p)
        {
            Vector3 centro = puntoDeAparicion != null
                ? puntoDeAparicion.position
                : transform.position;

            if (barras != null)
            {
                // La altura la manda la barra, no el punto de aparicion: asi
                // cambiar el grosor de las barras no deja la caja flotando.
                centro.y = barras.CentroApoyo.y + p.Tamano.y * 0.5f + holguraApoyo;
            }

            float giro = giroAleatorio > 0f
                ? UnityEngine.Random.Range(-giroAleatorio, giroAleatorio)
                : 0f;

            p.Reposicionar(centro, Quaternion.Euler(0f, giro, 0f));
        }

        // Comprueba que la partida sea posible antes de que el jugador gaste un
        // credito en algo que no puede salir.
        void Avisar(PrizeController p)
        {
            if (barras == null) return;

            if (!barras.CabePorElHueco(p.Tamano, out string motivo))
            {
                Debug.LogWarning("[Hashi] La caja '" + p.name + "' no vale con "
                                 + "estas barras: " + motivo + ".");
            }
        }

        static void PonerCapa(Transform t, int capa)
        {
            t.gameObject.layer = capa;
            for (int i = 0; i < t.childCount; i++) PonerCapa(t.GetChild(i), capa);
        }
    }
}
