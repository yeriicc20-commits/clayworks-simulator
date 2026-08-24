using UnityEngine;
using TMPro;

// Un texto suelto en el mundo, encima de alguien, que sube un poco y se apaga.
// Usa TextMeshPro 3D en vez de UI, asi no necesita canvas ni escalados raros.
public class FloatingText : MonoBehaviour
{
    public float duration = 2f;
    public float riseSpeed = 0.4f;
    public float fadeStart = 0.6f;

    private TextMeshPro label;
    private Transform follow;
    private Vector3 followOffset;
    private float bornAt;
    private Camera cam;

    public static void Show(string message, Transform follow, Vector3 offset, Color color, float duration = 2f, float fontSize = 2.4f)
    {
        if (follow == null) return;

        GameObject go = new GameObject("FloatingText");
        go.transform.position = follow.position + offset;

        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(4f, 1f);

        FloatingText floating = go.AddComponent<FloatingText>();
        floating.label = text;
        floating.follow = follow;
        floating.followOffset = offset;
        floating.duration = duration;
    }

    void Awake()
    {
        bornAt = Time.time;
        cam = Camera.main;
    }

    void LateUpdate()
    {
        float age = Time.time - bornAt;

        if (age >= duration || follow == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = follow.position + followOffset + Vector3.up * (riseSpeed * age);

        // De cara a la camara siempre, o no se leeria desde segun donde.
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }

        if (label == null) return;

        float fadeAfter = duration * fadeStart;

        if (age > fadeAfter)
        {
            float t = 1f - Mathf.InverseLerp(fadeAfter, duration, age);

            Color color = label.color;
            color.a = t;
            label.color = color;
        }
    }
}
