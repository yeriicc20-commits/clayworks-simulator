using UnityEngine;

namespace Hashi
{
    // Rele de contacto que va pegado a cada dedo.
    //
    // Existe por una regla de Unity que se olvida siempre: las colisiones se le
    // avisan al componente que esta en el MISMO objeto que el collider. El
    // controlador de las pinzas esta arriba, en la garra, asi que no se entera de
    // nada. Este trocito vive en el dedo, escucha el golpe y lo sube.
    [RequireComponent(typeof(Rigidbody))]
    public class ClawFingerContact : MonoBehaviour
    {
        [Tooltip("Quien recibe el aviso. Se rellena solo si se deja vacio.")]
        [SerializeField] ClawFingerController pinzas;

        [Tooltip("Por debajo de este impulso no se avisa. Un roce continuo "
                 + "dispararia el sonido de golpe cincuenta veces por segundo.")]
        [SerializeField] float impulsoMinimo = 0.01f;

        void Awake()
        {
            if (pinzas == null) pinzas = GetComponentInParent<ClawFingerController>();
        }

        void OnCollisionEnter(Collision c)
        {
            if (pinzas == null) return;
            if (c.gameObject.layer != HashiLayers.Premio) return;

            float impulso = c.impulse.magnitude;
            if (impulso < impulsoMinimo) return;

            pinzas.AvisarContacto(impulso);

            // La caja puede estar dormida despues de un rato quieta, y un cuerpo
            // dormido se traga el primer empujon. Se despierta al tocarla.
            Rigidbody rb = c.rigidbody;
            if (rb != null) rb.WakeUp();
        }
    }
}
