using UnityEngine;

// Donde se puede pegar esta cosa.
//
// Hasta ahora todo lo que se colocaba se apoyaba en el suelo o en un mostrador,
// asi que la regla estaba metida en el colocador: la superficie tenia que mirar
// hacia arriba y punto. Una bombilla no cumple eso nunca -- va en el techo, que
// mira hacia abajo -- y sin esto se quedaria colgando en el aire en rojo.
//
// La regla viaja con el objeto y no con el colocador porque es una propiedad de
// la cosa, no de quien la coloca: una bombilla va en el techo la ponga quien la
// ponga.
public class ReglaDeColocacion : MonoBehaviour
{
    public enum Donde
    {
        Suelo,   // Superficies que miran hacia arriba: el suelo, un mostrador.
        Techo,   // Las que miran hacia abajo.
        Libre,   // Cualquiera, y el objeto se orienta a ella.
    }

    public Donde donde = Donde.Suelo;

    [Tooltip("Cuanto se separa de la superficie, para que no se incruste.")]
    public float separacion = 0f;

    // Lo que se le dice al jugador cuando esta apuntando a donde no debe.
    public string Aviso
    {
        get
        {
            switch (donde)
            {
                case Donde.Techo: return "Esto va en el techo";
                case Donde.Libre: return "Apunta a una superficie";
                default: return "Esto va en el suelo";
            }
        }
    }

    // Si la superficie que se esta mirando vale para esto.
    public bool Vale(Vector3 normal, float minArriba)
    {
        switch (donde)
        {
            case Donde.Techo: return normal.y <= -minArriba;
            case Donde.Libre: return true;
            default: return normal.y >= minArriba;
        }
    }

    // Como queda puesto contra esa superficie.
    //
    // En el suelo se sigue usando el giro de siempre, que es el que el jugador
    // controla con la rueda. En techo y pared manda la superficie: una bombilla
    // torcida respecto al techo no tiene ningun sentido.
    public Quaternion Orientacion(Vector3 normal, float giroY)
    {
        if (donde == Donde.Suelo) return Quaternion.Euler(0f, giroY, 0f);

        if (donde == Donde.Techo)
        {
            // Colgando: el objeto esta modelado hacia abajo desde su origen, asi
            // que basta con dejarlo derecho y girarlo sobre si mismo.
            return Quaternion.Euler(0f, giroY, 0f);
        }

        // Contra la superficie, mirando hacia fuera de ella. El "delante" del
        // modelo es su +Z, que es lo que da LookRotation.
        Vector3 arriba = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;

        return Quaternion.LookRotation(normal, arriba) * Quaternion.Euler(0f, 0f, giroY);
    }
}
