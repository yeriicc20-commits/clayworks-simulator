using UnityEngine;

// "¿Estoy apuntando a esto?", en un solo sitio.
//
// Lo usan el interruptor y todo lo que se puede recoger para cambiarlo de
// sitio. Vive aparte porque la primera version de esto la escribi dos veces mal
// de la misma manera: medir la direccion desde los PIES del jugador y
// compararla con hacia donde mira la CAMARA, que esta metro y medio mas arriba.
// Con una sola copia, arreglarlo una vez lo arregla en todas partes.
public static class Apuntado
{
    // Un rayo desde el centro de la pantalla, que es lo que el jugador cree que
    // esta haciendo. De regalo, lo que este por delante tapa lo de detras: con
    // distancia y angulo se podian pulsar cosas a traves de una pared.
    public static bool A(Transform objetivo, ref Transform camara, float alcance)
    {
        if (objetivo == null) return false;
        if (CursorMode.FreeCursor) return false;
        if (!Camara(ref camara)) return false;

        RaycastHit toque;

        if (!Physics.Raycast(camara.position, camara.forward, out toque, alcance,
                             ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Vale tambien si toca una pieza hija, como la tecla del interruptor o
        // el difusor de una pantalla.
        return toque.collider.transform == objetivo
               || toque.collider.transform.IsChildOf(objetivo);
    }

    static bool Camara(ref Transform camara)
    {
        if (camara != null) return true;

        FirstPersonController fpc = Object.FindAnyObjectByType<FirstPersonController>();

        // Por este orden: la que el jugador tenga puesta, y si no la principal.
        // No se pide el componente Camera, solo el sitio desde el que se mira:
        // pedirlo fue justo lo que dejo esto sin funcionar una vez.
        if (fpc != null && fpc.cameraTransform != null) camara = fpc.cameraTransform;
        else if (Camera.main != null) camara = Camera.main.transform;

        return camara != null;
    }
}
