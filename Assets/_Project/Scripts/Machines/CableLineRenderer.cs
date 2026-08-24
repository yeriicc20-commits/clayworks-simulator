using UnityEngine;

public class CableLineRenderer : MonoBehaviour
{
    public Transform topPoint;
    public Transform bottomPoint;
    public int segmentCount = 10;
    public float sagAmount = 0.03f;

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segmentCount + 1;
    }

    void Update()
    {
        Vector3 top = topPoint.position;
        Vector3 bottom = bottomPoint.position;

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            Vector3 point = Vector3.Lerp(top, bottom, t);
            point.y -= Mathf.Sin(t * Mathf.PI) * sagAmount;
            lineRenderer.SetPosition(i, point);
        }
    }
}
