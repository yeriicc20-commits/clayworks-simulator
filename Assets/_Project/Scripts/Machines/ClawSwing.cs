using UnityEngine;

public class ClawSwing : MonoBehaviour
{
    public Transform railX;
    public Transform railZ;

    public float stiffness = 40f;
    public float damping = 6f;
    public float tiltAmount = 15f;
    public float maxTiltAngle = 25f;

    private float tiltX = 0f;
    private float tiltZ = 0f;
    private float velTiltX = 0f;
    private float velTiltZ = 0f;

    private Vector3 lastRailPos;

    void Start()
    {
        lastRailPos = new Vector3(railX.localPosition.x, 0f, railZ.localPosition.z);
    }

    void Update()
    {
        Vector3 currentRailPos = new Vector3(railX.localPosition.x, 0f, railZ.localPosition.z);
        Vector3 velocity = (currentRailPos - lastRailPos) / Time.deltaTime;
        lastRailPos = currentRailPos;

        float targetTiltZ = Mathf.Clamp(-velocity.x * tiltAmount, -maxTiltAngle, maxTiltAngle);
        float targetTiltX = Mathf.Clamp(velocity.z * tiltAmount, -maxTiltAngle, maxTiltAngle);

        float forceZ = -stiffness * (tiltZ - targetTiltZ) - damping * velTiltZ;
        velTiltZ += forceZ * Time.deltaTime;
        tiltZ += velTiltZ * Time.deltaTime;

        float forceX = -stiffness * (tiltX - targetTiltX) - damping * velTiltX;
        velTiltX += forceX * Time.deltaTime;
        tiltX += velTiltX * Time.deltaTime;

        transform.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
    }
}