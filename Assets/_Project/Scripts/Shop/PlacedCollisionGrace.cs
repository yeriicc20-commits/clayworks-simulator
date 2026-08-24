using UnityEngine;
using System.Collections.Generic;

// Cuando dejas algo pegado a los pies, su collider aparece dentro del jugador y
// el motor los separa de un empujon: eso es lo que te lanzaba por los aires.
//
// Comprobar antes que no se solapan no basta, porque el fantasma se mide con un
// margen y el objeto de verdad puede acabar rozando igual. Asi que aqui se corta
// por lo sano: la colision entre el objeto y el jugador se ignora hasta que
// dejan de tocarse, y solo entonces se vuelve a activar.
public class PlacedCollisionGrace : MonoBehaviour
{
    [Tooltip("Tope de seguridad por si el jugador se queda encima y no se mueve.")]
    public float maxSeconds = 10f;

    [Tooltip("Margen extra al comprobar si siguen tocandose.")]
    public float clearance = 0.05f;

    private readonly List<Collider> mine = new List<Collider>();
    private readonly List<Collider> player = new List<Collider>();

    private float deadline;
    private bool ignoring = false;

    public static void Apply(GameObject target, Transform playerRoot)
    {
        if (target == null || playerRoot == null) return;

        PlacedCollisionGrace grace = target.GetComponent<PlacedCollisionGrace>();

        if (grace == null) grace = target.AddComponent<PlacedCollisionGrace>();

        grace.Begin(playerRoot);
    }

    void Begin(Transform playerRoot)
    {
        Release();

        mine.Clear();
        player.Clear();

        // Solo los que estan vivos: Physics.IgnoreCollision protesta por consola
        // si le pasas un collider apagado o de un objeto desactivado.
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (Usable(col)) mine.Add(col);
        }

        foreach (Collider col in playerRoot.GetComponentsInChildren<Collider>(true))
        {
            if (Usable(col)) player.Add(col);
        }

        if (mine.Count == 0 || player.Count == 0) return;

        SetIgnore(true);

        deadline = Time.time + maxSeconds;
        ignoring = true;
        enabled = true;
    }

    static bool Usable(Collider col)
    {
        return col != null && !col.isTrigger && col.enabled && col.gameObject.activeInHierarchy;
    }

    void SetIgnore(bool ignore)
    {
        foreach (Collider a in mine)
        {
            if (!Usable(a)) continue;

            foreach (Collider b in player)
            {
                if (!Usable(b)) continue;

                Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }

    void Update()
    {
        if (!ignoring) return;

        if (Time.time > deadline || !StillTouching()) Release();
    }

    bool StillTouching()
    {
        Bounds? a = Combined(mine);
        Bounds? b = Combined(player);

        if (a == null || b == null) return false;

        Bounds grown = a.Value;
        grown.Expand(clearance * 2f);

        return grown.Intersects(b.Value);
    }

    Bounds? Combined(List<Collider> list)
    {
        Bounds result = new Bounds();
        bool any = false;

        foreach (Collider col in list)
        {
            if (col == null || !col.enabled) continue;

            if (!any)
            {
                result = col.bounds;
                any = true;
            }
            else
            {
                result.Encapsulate(col.bounds);
            }
        }

        return any ? result : (Bounds?)null;
    }

    void Release()
    {
        if (!ignoring) return;

        SetIgnore(false);

        ignoring = false;
        enabled = false;
    }

    void OnDestroy()
    {
        Release();
    }
}
