using UnityEngine;

public class FirstPersonController : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float gravity = -9.81f;

    public Transform cameraTransform;
    public float mouseSensitivity = 2f;

    [Header("Agacharse")]
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Tooltip("Que fraccion de su altura mide agachado.")]
    [Range(0.3f, 0.9f)] public float crouchHeight = 0.45f;

    [Tooltip("Lo despacio que anda agachado, en fraccion de su velocidad.")]
    [Range(0.2f, 1f)] public float crouchSpeed = 0.45f;

    public float crouchLerp = 10f;

    // Lo consultan los objetos que se recogen agachandose.
    public static bool IsCrouching { get; private set; }

    // Bloquea SOLO el giro de camara, no el movimiento ni el raton.
    //
    // Lo enciende el modo de mirar el peluche, donde el raton pasa a girar
    // el juguete. Para esto no sirve CursorMode.Free: eso suelta el cursor,
    // y el jugador se encontraria la flecha del raton por encima, perdiendo
    // el centro de la pantalla que es justo donde esta el peluche.
    public static bool LookLocked { get; set; }

    private CharacterController controller;
    private Vector3 velocity;
    private float verticalLookRotation = 0f;

    private float standHeight;
    private Vector3 standCenter;
    private float camStandY;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;

        // La postura de pie se toma de la escena y no de un numero escrito
        // aqui: asi agacharse sigue funcionando si manana cambia la altura del
        // jugador.
        standHeight = controller.height;
        standCenter = controller.center;

        if (cameraTransform != null) camStandY = cameraTransform.localPosition.y;
    }

    void Update()
    {
        // Mientras hay una pantalla abierta el raton es suyo, no de la camara.
        // Se mira la intencion y no Cursor.lockState: el editor suelta el raton
        // por su cuenta al pulsar Escape y eso dejaba la camara descolgada.
        if (CursorMode.FreeCursor)
        {
            IsCrouching = false;
            return;
        }

        HandleCrouch();
        if (!LookLocked) HandleMouseLook();
        HandleMovement();
    }

    void HandleCrouch()
    {
        IsCrouching = Input.GetKey(crouchKey);

        float objetivo = IsCrouching ? standHeight * crouchHeight : standHeight;

        // Interpolado y no de golpe. Cambiando la altura del CharacterController
        // de un fotograma al siguiente, el jugador se hunde en el suelo o pega
        // un salto, segun hacia donde vaya el cambio.
        float k = 1f - Mathf.Exp(-Time.deltaTime * crouchLerp);

        controller.height = Mathf.Lerp(controller.height, objetivo, k);

        // El centro baja la mitad de lo que baja la altura: si no, los pies se
        // quedan flotando o se meten bajo el suelo.
        Vector3 centro = standCenter;
        centro.y = standCenter.y - (standHeight - controller.height) * 0.5f;
        controller.center = centro;

        if (cameraTransform == null) return;

        Vector3 ojo = cameraTransform.localPosition;
        ojo.y = camStandY - (standHeight - controller.height);
        cameraTransform.localPosition = ojo;
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        float deadzone = 0.5f;
        if (Mathf.Abs(moveX) < deadzone) moveX = 0f;
        if (Mathf.Abs(moveZ) < deadzone) moveZ = 0f;

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && !IsCrouching;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        if (IsCrouching) currentSpeed *= crouchSpeed;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}