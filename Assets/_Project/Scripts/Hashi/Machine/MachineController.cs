using System;
using System.Collections;
using UnityEngine;

namespace Hashi
{
    // La secuencia de la maquina, con el mando de DOS BOTONES de las
    // recreativas japonesas.
    //
    //   Reposo -> avanza a la derecha -> avanza al fondo -> baja -> cierra
    //          -> sube -> vuelve -> Reposo
    //
    // La garra no se conduce: se para. Arranca sola hacia la derecha y el boton
    // solo dice CUANDO parar; al pararla, arranca sola hacia el fondo y el mismo
    // boton vuelve a decir cuando. Dos pulsaciones y la jugada ya esta echada.
    //
    // Esto no es una simplificacion, es mas dificil que una palanca: hay que
    // calcular el punto de frenada de un carro que ya viene lanzado, y una vez
    // pulsado no hay correccion posible. Es lo que hace que la jugada se decida
    // en dos instantes concretos y no a base de ir tanteando.
    //
    // Si el jugador no pulsa, el carro llega al final de su carril y pasa de
    // fase solo, como el de verdad: no se queda esperando eternamente.
    public class MachineController : MonoBehaviour
    {
        public enum Estado
        {
            Reposo,     // arriba, en la esquina, esperando credito
            AvanzaX,    // corre sola a la derecha; el boton la para
            AvanzaZ,    // corre sola hacia el fondo; el boton la para
            Bajando,
            Cerrando,
            Subiendo,
            Volviendo,
        }

        // De donde salen las ordenes del jugador.
        //
        // La misma maquina vale para dos sitios muy distintos y no se puede
        // pedir lo mismo en los dos. En su escena de pruebas es un juego suelto
        // y lee el teclado directamente; dentro del local es un mueble mas de la
        // tienda, y ahi las teclas las elige el jugador en el menu de ajustes.
        // Ignorar eso significaria que la maquina de puente se juega con unas
        // teclas y la de garra con otras, y eso no hay quien lo defienda.
        public enum Mando
        {
            Propio,   // escena suelta: WASD y espacio, sin pasar por los ajustes
            Tienda,   // dentro del local: las teclas de AjustesControles
        }

        [Header("Referencias")]
        [SerializeField] ClawController garra;
        [SerializeField] ClawFingerController pinzas;

        [Tooltip("El motor y su mando. Si esta puesto, el par de cada partida "
                 + "lo decide el, no el valor fijo de las pinzas.")]
        [SerializeField] ClawStrength fuerza;

        [Header("Mando")]
        [Tooltip("Propio = escena de pruebas. Tienda = dentro del local, con "
                 + "las teclas que el jugador tenga puestas en los ajustes.")]
        [SerializeField] Mando mando = Mando.Propio;

        [Header("Tiempos")]
        [Tooltip("Segundos de turno. Al agotarse baja sola, como la de verdad. "
                 + "0 = sin limite.")]
        [Range(0f, 90f)] [SerializeField] float tiempoTurno = 20f;

        [Tooltip("Lo que espera parada, ya colocada, antes de empezar a bajar. "
                 + "Es el rato en el que el jugador ve donde ha parado.")]
        [Range(0f, 3f)] [SerializeField] float esperaAntesDeBajar = 1f;

        [Tooltip("Lo que se queda apretando abajo antes de empezar a subir. Es "
                 + "el rato en el que la caja acaba de volcar.")]
        [Range(0f, 3f)] [SerializeField] float esperaAbajo = 0.6f;

        [Tooltip("Tope de seguridad esperando a que cierren las pinzas, por si "
                 + "se quedan atascadas contra algo.")]
        [Range(0.5f, 10f)] [SerializeField] float esperaCierreMaxima = 3f;

        [Tooltip("Las pinzas siguen cerradas mientras sube. Es a proposito: la "
                 + "caja puede seguir chocando con ellas y terminar de girar "
                 + "durante la subida. Se abren al llegar arriba.")]
        [SerializeField] bool abrirSoloAlLlegarArriba = true;

        [Tooltip("Lo que espera arriba antes de volver al punto de salida.")]
        [Range(0f, 3f)] [SerializeField] float esperaArriba = 0.3f;

        [Header("Estado")]
        [Tooltip("Con esto puesto, el jugador no puede ni mover ni soltar. Lo "
                 + "maneja el GameManager cuando no hay creditos o ya se ha "
                 + "ganado.")]
        [SerializeField] bool controlesBloqueados = true;

        // ------------------------------------------------------------- eventos

        public event Action<Estado> AlCambiarEstado;

        // El turno ha terminado y la garra ya esta otra vez en su sitio.
        public event Action AlTerminarTurno;

        public event Action AlSoltar;      // se para el segundo eje y va a bajar
        public event Action AlPararEje;    // se para el primero y arranca el otro
        public event Action AlCerrarPinzas;

        // ------------------------------------------------------------- lectura

        public Estado EstadoActual { get; private set; } = Estado.Reposo;
        public bool Ocupado => EstadoActual != Estado.Reposo
                               && EstadoActual != Estado.AvanzaX
                               && EstadoActual != Estado.AvanzaZ;
        public bool EnTurno => EstadoActual != Estado.Reposo;
        public float TiempoRestante { get; private set; }
        public bool ControlesBloqueados
        {
            get => controlesBloqueados;
            set => controlesBloqueados = value;
        }

        Coroutine jugada;

        // Sin garra o sin pinzas esto no es una maquina, es una excepcion por
        // fotograma. Vale mas apagarse y decirlo una vez que llenar la consola
        // de nulos hasta que no se vea ni el error de verdad.
        void Awake()
        {
            if (garra != null && pinzas != null) return;

            Debug.LogError("[Hashi] A " + name + " le falta la garra o las "
                           + "pinzas. Apago la maquina; se arregla rellenando "
                           + "esos dos campos, o volviendo a montar la escena "
                           + "con ClayWorks/Hashi-Watashi/Montar escena.", this);

            enabled = false;
        }

        void Update()
        {
            bool corriendo = EstadoActual == Estado.AvanzaX
                             || EstadoActual == Estado.AvanzaZ;

            if (!corriendo) return;

            if (controlesBloqueados)
            {
                garra.Conducir(Vector2.zero);
                return;
            }

            // La direccion no la elige el jugador: en la primera fase va a la
            // derecha y en la segunda hacia el fondo, siempre.
            garra.Conducir(EstadoActual == Estado.AvanzaX ? Vector2.right : Vector2.up);

            if (tiempoTurno > 0f)
            {
                TiempoRestante = Mathf.Max(0f, TiempoRestante - Time.deltaTime);
            }

            // Tres maneras de terminar la fase: pulsar el boton, llegar al final
            // del carril, o quedarse sin tiempo. Las tres hacen lo mismo, que es
            // parar donde este y seguir.
            bool topeCarril = EstadoActual == Estado.AvanzaX
                ? garra.EnLimiteX
                : garra.EnLimiteZ;

            bool sinTiempo = tiempoTurno > 0f && TiempoRestante <= 0f;

            if (!QuiereBajar() && !topeCarril && !sinTiempo) return;

            garra.Conducir(Vector2.zero);

            if (EstadoActual == Estado.AvanzaX)
            {
                Cambiar(Estado.AvanzaZ);
                AlPararEje?.Invoke();
                return;
            }

            Soltar();
        }

        bool QuiereBajar()
        {
            if (mando == Mando.Propio) return InputReader.Bajar();

            // Con el cursor libre (menus, ordenador, panel de precios) el
            // teclado no es de la maquina. Sin esto, escribir un precio con el
            // panel abierto soltaba la garra por detras.
            if (CursorMode.FreeCursor) return false;

            return AjustesControles.Pulsando(AjustesControles.Accion.BajarGarra);
        }

        public void PonerMando(Mando m) { mando = m; }

        // Arranca un turno. Lo llama el GameManager cuando ha cobrado el credito.
        public void EmpezarTurno()
        {
            if (EnTurno) return;

            TiempoRestante = tiempoTurno;
            garra.SoltarVuelta();
            garra.HorizontalBloqueado = false;

            // El par se sortea AQUI, una vez por partida, y ya no cambia. Si se
            // sorteara al cerrar, el jugador no podria aprender nada: la misma
            // jugada daria resultados distintos por motivos invisibles.
            if (fuerza != null) fuerza.Cobrar();

            pinzas.Abrir();

            Cambiar(Estado.AvanzaX);
        }

        // Baja la garra. Es el punto de no retorno del turno.
        public void Soltar()
        {
            if (EstadoActual != Estado.AvanzaZ) return;

            AlSoltar?.Invoke();
            jugada = StartCoroutine(Jugada());
        }

        public void AplicarDificultad(DifficultySettings d)
        {
            if (d == null) return;

            tiempoTurno = d.turnTime;
            garra.AplicarDificultad(d);
            pinzas.AplicarDificultad(d);
        }

        // Corta la jugada y devuelve la maquina a reposo. Para el boton de
        // reiniciar; en juego normal no hace falta.
        public void Abortar()
        {
            if (jugada != null)
            {
                StopCoroutine(jugada);
                jugada = null;
            }

            StopAllCoroutines();
            pinzas.Abrir();
            garra.VolverACasa();
            Cambiar(Estado.Reposo);
        }

        IEnumerator Jugada()
        {
            // --- se para y respira -----------------------------------------
            // El segundo de espera con la garra ya quieta no es relleno: es el
            // momento en el que el jugador ve DONDE ha parado y se da cuenta de
            // si le ha salido bien. Bajando al instante, el acierto y el fallo
            // se mezclan en el mismo movimiento y no se aprende nada.
            garra.HorizontalBloqueado = true;
            garra.Conducir(Vector2.zero);

            yield return new WaitForSeconds(esperaAntesDeBajar);

            // --- baja -------------------------------------------------------
            Cambiar(Estado.Bajando);

            // Baja con los brazos ABIERTOS y sigue abierta todo el descenso. No
            // es un detalle de adorno: los brazos abiertos son los que pasan por
            // fuera de la caja. Bajando ya cerrada, la garra le caeria encima y
            // la aplastaria contra las barras en vez de colocarse a sus lados.
            //
            // Se manda abrir aqui otra vez aunque ya se abrio al empezar el
            // turno. Es gratis y cubre el caso de que algo las haya cerrado por
            // el camino.
            pinzas.Abrir();
            garra.Bajar();

            while (!garra.AlturaEnDestino) yield return null;

            // --- cierra -----------------------------------------------------
            Cambiar(Estado.Cerrando);
            pinzas.Cerrar();
            AlCerrarPinzas?.Invoke();

            float limite = Time.time + esperaCierreMaxima;
            while (!pinzas.CierreTerminado && Time.time < limite) yield return null;

            // Aqui es donde vuelca la caja: las pinzas ya estan apretando y se
            // quedan un rato manteniendo la fuerza.
            yield return new WaitForSeconds(esperaAbajo);

            // --- sube -------------------------------------------------------
            Cambiar(Estado.Subiendo);
            if (!abrirSoloAlLlegarArriba) pinzas.Abrir();
            garra.Subir();

            while (!garra.AlturaEnDestino) yield return null;

            pinzas.Abrir();
            yield return new WaitForSeconds(esperaArriba);

            // --- vuelve -----------------------------------------------------
            Cambiar(Estado.Volviendo);
            garra.VolverACasa();

            while (!garra.EnCasa) yield return null;

            garra.SoltarVuelta();
            garra.HorizontalBloqueado = false;

            Cambiar(Estado.Reposo);
            jugada = null;

            AlTerminarTurno?.Invoke();
        }

        void Cambiar(Estado nuevo)
        {
            if (EstadoActual == nuevo) return;

            EstadoActual = nuevo;
            AlCambiarEstado?.Invoke(nuevo);
        }
    }
}
