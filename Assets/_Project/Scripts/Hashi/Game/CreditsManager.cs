using System;
using UnityEngine;

namespace Hashi
{
    // Los creditos. Una partida, un credito.
    //
    // Es corto a proposito: lo unico que tiene que hacer es no dejar que se
    // juegue gratis y no dejar que se gaste lo que no hay. Repartir esa cuenta
    // entre varios scripts es como acaban saliendo las partidas de regalo.
    public class CreditsManager : MonoBehaviour
    {
        [Tooltip("Con cuantos empieza.")]
        [SerializeField] int creditosIniciales = 5;

        [Tooltip("Lo que cuesta una partida.")]
        [Min(1)] [SerializeField] int precioPorPartida = 1;

        [Tooltip("Tope, para que el boton de pruebas no se dispare.")]
        [SerializeField] int maximo = 99;

        int creditos;

        public event Action<int> AlCambiar;

        public int Creditos => creditos;
        public int Precio => precioPorPartida;
        public bool HayParaJugar => creditos >= precioPorPartida;

        void Awake()
        {
            creditos = Mathf.Clamp(creditosIniciales, 0, maximo);
        }

        void Start()
        {
            // En Start y no en Awake: la interfaz se suscribe en su propio
            // Awake, y avisando antes se pierde el primer aviso y el marcador
            // sale a cero hasta que cambie algo.
            AlCambiar?.Invoke(creditos);
        }

        // Cobra una partida. Devuelve false si no llega, y entonces no cobra
        // nada: cobrar a medias es como se pierden creditos sin explicacion.
        public bool Cobrar()
        {
            if (!HayParaJugar) return false;

            creditos -= precioPorPartida;
            AlCambiar?.Invoke(creditos);
            return true;
        }

        public void Anadir(int cuantos = 1)
        {
            creditos = Mathf.Clamp(creditos + cuantos, 0, maximo);
            AlCambiar?.Invoke(creditos);
        }

        public void Reiniciar()
        {
            creditos = Mathf.Clamp(creditosIniciales, 0, maximo);
            AlCambiar?.Invoke(creditos);
        }
    }
}
