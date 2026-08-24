using UnityEngine;

public class StretchyCable : MonoBehaviour
{
    public Transform topPoint;
    public Transform bottomPoint;
    public int scaleAxisIndex = 1;
    public bool invert = false;

    private float originalLength;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private Vector3 initialLocalPosition;

    void Start()
    {
        originalLength = Vector3.Distance(topPoint.position, bottomPoint.position);
        if (originalLength < 0.001f) originalLength = 1f;

        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        initialLocalPosition = transform.localPosition;
    }

    void Update()
    {
        float currentLength = Vector3.Distance(topPoint.position, bottomPoint.position);
        float ratio = currentLength / originalLength;

        if (invert)
        {
            ratio = originalLength / currentLength;
        }

        transform.localRotation = initialLocalRotation;

        Vector3 scale = initialLocalScale;
        Vector3 position = initialLocalPosition;
        float growth = (ratio - 1f);

        if (scaleAxisIndex == 0)
        {
            scale.x = initialLocalScale.x * ratio;
            position.x = initialLocalPosition.x - (growth * initialLocalScale.x * 0.5f);
        }
        else if (scaleAxisIndex == 1)
        {
            scale.y = initialLocalScale.y * ratio;
            position.y = initialLocalPosition.y - (growth * initialLocalScale.y * 0.5f);
        }
        else
        {
            scale.z = initialLocalScale.z * ratio;
            position.z = initialLocalPosition.z - (growth * initialLocalScale.z * 0.5f);
        }

        transform.localScale = scale;
        transform.localPosition = position;
    }
}