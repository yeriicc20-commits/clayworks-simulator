using UnityEngine;

// Ajusta el BoxCollider a lo que ocupan las mallas del objeto. Asi no hay que
// acertar las medidas a mano en el prefab, que ademas cambian si escalas.
[RequireComponent(typeof(BoxCollider))]
public class FitBoxToRenderers : MonoBehaviour
{
    public bool fitOnStart = true;

    void Start()
    {
        if (fitOnStart) Fit();
    }

    public void Fit()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool any = false;

        foreach (Renderer rend in renderers)
        {
            if (rend == null || rend is LineRenderer) continue;

            if (!any)
            {
                bounds = rend.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(rend.bounds);
            }
        }

        if (!any) return;

        Vector3 scale = transform.lossyScale;

        box.center = transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }
}
