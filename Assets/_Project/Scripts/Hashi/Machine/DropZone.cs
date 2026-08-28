using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hashi
{
    // La bandeja de recogida: el unico sitio donde se gana.
    //
    // Ganar no es tocar el trigger. Una esquina de la caja asomando entre las
    // barras ya toca este volumen, y dar eso por ganado convierte el juego en
    // otra cosa: bastaria con menear la caja hasta que asome un pico. Aqui se
    // exige que la caja este ENTERA dentro, por debajo de las barras y quieta un
    // rato. Es lo mismo que mira una maquina de verdad, que no paga hasta que el
    // premio ha cruzado el sensor del cajon.
    //
    // Los candidatos se apuntan en OnTriggerEnter y se comprueban en FixedUpdate
    // en vez de usar OnTriggerStay: Unity deja de mandar Stay cuando el cuerpo se
    // duerme, y una caja que aterriza y se queda quieta se duerme enseguida. Ya
    // paso en otros sitios: el premio se quedaba en la bandeja sin ser de nadie.
    [RequireComponent(typeof(BoxCollider))]
    public class DropZone : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Para saber a que altura estan las barras y comprobar que la "
                 + "caja ha pasado de verdad por el hueco.")]
        [SerializeField] BarRig barras;

        [Header("Condiciones")]
        [Tooltip("Cuanto tiene que llevar dentro y quieta antes de darla por "
                 + "recogida.")]
        [Range(0.05f, 3f)] [SerializeField] float tiempoConfirmacion = 0.45f;

        [Tooltip("Holgura al comprobar que la caja cabe entera dentro del "
                 + "volumen. Sin holgura, una caja apoyada justo en el borde "
                 + "no cuenta nunca.")]
        [Range(0f, 0.1f)] [SerializeField] float holgura = 0.01f;

        [Tooltip("Exige ademas que la caja este parada. Quitalo si quieres que "
                 + "cuente en cuanto entra entera.")]
        [SerializeField] bool exigirQuietud = true;

        [Header("Depuracion")]
        [SerializeField] bool mostrarGizmos = true;

        BoxCollider volumen;
        readonly List<PrizeController> candidatos = new List<PrizeController>();
        readonly Dictionary<PrizeController, float> dentroDesde =
            new Dictionary<PrizeController, float>();

        // Salta una sola vez por premio.
        public event Action<PrizeController> AlRecogerPremio;

        void Awake()
        {
            volumen = GetComponent<BoxCollider>();
            volumen.isTrigger = true;
        }

        void Reset()
        {
            BoxCollider c = GetComponent<BoxCollider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            PrizeController p = other.GetComponentInParent<PrizeController>();
            if (p == null || candidatos.Contains(p)) return;

            candidatos.Add(p);
        }

        void OnTriggerExit(Collider other)
        {
            PrizeController p = other.GetComponentInParent<PrizeController>();
            if (p == null) return;

            candidatos.Remove(p);
            dentroDesde.Remove(p);
        }

        void FixedUpdate()
        {
            for (int i = candidatos.Count - 1; i >= 0; i--)
            {
                PrizeController p = candidatos[i];

                if (p == null)
                {
                    candidatos.RemoveAt(i);
                    continue;
                }

                if (p.EstadoActual == PrizeController.Estado.Recogido) continue;

                if (!EstaDentro(p))
                {
                    dentroDesde.Remove(p);
                    continue;
                }

                if (!dentroDesde.ContainsKey(p)) dentroDesde[p] = Time.time;

                if (Time.time - dentroDesde[p] < tiempoConfirmacion) continue;
                if (exigirQuietud && !p.EstaQuieta) continue;

                p.CambiarEstado(PrizeController.Estado.Recogido);
                candidatos.RemoveAt(i);
                dentroDesde.Remove(p);

                AlRecogerPremio?.Invoke(p);
            }
        }

        // Las dos condiciones, separadas para poder leerlas de una pasada.
        public bool EstaDentro(PrizeController p)
        {
            if (p == null || volumen == null) return false;

            Bounds caja = p.Envolvente;

            // 1. La caja ENTERA por debajo de las barras. Esta es la condicion
            //    que de verdad quiere decir "ha pasado por el hueco": mientras
            //    asome un pico por arriba, sigue apoyada y no ha caido nada.
            //
            //    Se mira contra las barras y no contra el techo del volumen a
            //    proposito. Contra el volumen habria que hacerlo altisimo para
            //    que una caja de canto quepa entera, y entonces empieza a
            //    contar cosas que aun no han caido.
            if (barras != null)
            {
                float bajoLasBarras = barras.CentroApoyo.y - barras.BarRadius * 2f;
                if (caja.max.y > bajoLasBarras + holgura) return false;
            }

            // 2. Y encima de la bandeja, no atascada en cualquier otro sitio.
            Bounds zona = volumen.bounds;
            zona.Expand(holgura * 2f);

            return zona.Contains(caja.center);
        }

        // Para reiniciar sin arrastrar candidatos de la partida anterior.
        public void Olvidar()
        {
            candidatos.Clear();
            dentroDesde.Clear();
        }

        void OnDrawGizmos()
        {
            if (!mostrarGizmos) return;

            BoxCollider c = volumen != null ? volumen : GetComponent<BoxCollider>();
            if (c == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
            Gizmos.DrawCube(c.center, c.size);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(c.center, c.size);
        }
    }
}
