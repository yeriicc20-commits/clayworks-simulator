using UnityEngine;

// Se puede volver a coger para cambiarlo de sitio.
//
// Sin esto, colocar una luz es para siempre: la unica salida seria borrarla en
// el editor, que no es algo que se pueda pedir a quien esta jugando. Y con el
// techo entero por delante, acertar a la primera con donde va cada luminaria no
// pasa nunca.
//
// No se vuelve a comprar: se levanta la que ya esta y se coloca en otro sitio,
// con la misma pantalla de colocacion que la primera vez.
public class Recolocable : MonoBehaviour
{
    [Tooltip("Desde cuan lejos se puede recoger.")]
    public float alcance = 5f;

    [Tooltip("Como se llama en el cartel: 'mover LA PANTALLA'.")]
    public string queEs = "la luz";

    [Tooltip("Recogerlo con la tecla de tirar en vez de con la de usar.")]
    public bool conTeclaDeTirar = false;

    Transform camara;

    // Con que se recoge.
    //
    // El interruptor necesita otra tecla: usar es lo que enciende la luz, y
    // compartirla lo dejaria imposible de encender sin acabar moviendolo de
    // sitio. La pantalla no hace nada al usarla, asi que ahi vale la de siempre.
    AjustesControles.Accion Tecla
    {
        get
        {
            return conTeclaDeTirar
                ? AjustesControles.Accion.Lanzar
                : AjustesControles.Accion.Usar;
        }
    }

    void Update()
    {
        // Ya se esta colocando algo: seria empezar a mover una segunda cosa
        // encima de la primera.
        if (PlacementManager.Instance == null) return;
        if (PlacementManager.Instance.IsPlacing) return;

        if (!Apuntado.A(transform, ref camara, alcance)) return;

        InteractionUI.Prompt(
            AjustesControles.NombreTecla(AjustesControles.Tecla(Tecla))
            + ": mover " + queEs);

        if (!AjustesControles.Pulsando(Tecla)) return;

        Recoger();
    }

    void Recoger()
    {
        // Donde estaba, por si se arrepiente.
        Vector3 posicion = transform.position;
        Quaternion giro = transform.rotation;

        PlacementManager.Instance.StartMoving(
            gameObject,
            (donde, comoQueda) => Volver(donde, comoQueda),
            () => Volver(posicion, giro));

        // Se esconde DESPUES de pedir el movimiento, no antes: el fantasma se
        // clona de este mismo objeto, y clonarlo ya apagado saldria apagado.
        gameObject.SetActive(false);
    }

    void Volver(Vector3 donde, Quaternion comoQueda)
    {
        gameObject.SetActive(true);
        transform.SetPositionAndRotation(donde, comoQueda);
    }
}
