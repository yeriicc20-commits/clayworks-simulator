using UnityEngine;

public class FirstPersonController : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float gravity = -9.81f;

    public Transform cameraTransform;

    // La tecla principal la elige el jugador en el menu de ajustes.
    static KeyCode crouchKey { get { return AjustesControles.Tecla(AjustesControles.Accion.Agacharse); } }

    [Header("Agacharse")]

    [Tooltip("Segunda tecla para lo mismo. Existe por el editor, no por el "
             + "juego.")]
    public KeyCode crouchKeyAlt = KeyCode.C;

    // Valen las dos, y Control es la principal.
    //
    // Le quite Control por mi cuenta y no debi hacerlo: andar hacia atras es S,
    // asi que agacharse retrocediendo es literalmente Ctrl+S, y en modo juego
    // Unity contesta con un cartel de "You must exit play mode to save the
    // scene!". Pero eso es cosa del EDITOR: en una build compilada no pasa
    // nunca, y Control es donde la busca cualquiera.
    //
    // Asi que se queda Control, y C es la salida para quien este probando
    // dentro del editor y le moleste el cartel. No hay forma de callar ese
    // atajo desde el juego: lo atiende Unity antes que nadie.

    [Tooltip("Que fraccion de su altura mide agachado.")]
    [Range(0.3f, 0.9f)] public float crouchHeight = 0.45f;

    [Tooltip("Lo despacio que anda agachado, en fraccion de su velocidad.")]
    [Range(0.2f, 1f)] public float crouchSpeed = 0.45f;

    public float crouchLerp = 10f;

    // Lo consultan los objetos que se recogen agachandose.
    public static bool IsCrouching { get; private set; }

    // Y la tecla, para que el aviso de pantalla la diga sin tenerla escrita
    // dos veces: cambiarla aqui cambia tambien lo que pone el cartel.
    public static KeyCode TeclaAgacharse { get; private set; }

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
        Cursor.lockState = CursorLockMode.Locked;
        Preparar();
    }

    // La postura de pie, medida de la escena y no escrita aqui: asi agacharse
    // sigue funcionando si manana cambia la altura del jugador.
    //
    // Va aparte de Start y se comprueba en cada Update porque recompilar con
    // el juego en marcha recarga el dominio: los campos privados se van a cero
    // y Start() NO se vuelve a llamar. Con standHeight en cero, la altura
    // objetivo del CharacterController sale cero y el jugador se hunde en el
    // suelo. Es el mismo fallo que tenia OrejasBlandas, y ahi costo quince mil
    // excepciones darse cuenta.
    void Preparar()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null) return;

        if (standHeight > 0.01f) return;

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

        Preparar();

        HandleCrouch();
        if (!LookLocked) HandleMouseLook();
        HandleMovement();
    }

    void HandleCrouch()
    {
        if (controller == null || standHeight <= 0.01f) return;

        TeclaAgacharse = crouchKey;
        IsCrouching = AjustesControles.Pulsada(AjustesControles.Accion.Agacharse) || Input.GetKey(crouchKeyAlt);

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
        // La sensibilidad sale del menu de ajustes y se lee cada fotograma:
        // moviendo la barra se nota el cambio sin cerrar la pantalla.
        float sens = AjustesJuego.Sensibilidad;

        float mouseX = Input.GetAxis("Mouse X") * sens;
        float mouseY = Input.GetAxis("Mouse Y") * sens;

        if (AjustesJuego.InvertirY) mouseY = -mouseY;

        transform.Rotate(Vector3.up * mouseX);

        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        // Teclas sueltas, y no los ejes "Horizontal" y "Vertical" de Unity.
        //
        // Esos ejes son WASD fijos: se configuran en los ajustes del proyecto
        // y no hay manera de cambiarlos desde el juego. Con una pantalla de
        // controles delante eso seria mentir, asi que se leen las teclas que
        // tenga puestas cada uno.
        //
        // Leyendolas asi tampoco hace falta zona muerta: una tecla esta
        // pulsada o no lo esta, no hay valores a medias que filtrar.
        float moveX = 0f;
        float moveZ = 0f;

        if (AjustesControles.Pulsada(AjustesControles.Accion.Derecha)) moveX += 1f;
        if (AjustesControles.Pulsada(AjustesControles.Accion.Izquierda)) moveX -= 1f;
        if (AjustesControles.Pulsada(AjustesControles.Accion.Adelante)) moveZ += 1f;
        if (AjustesControles.Pulsada(AjustesControles.Accion.Atras)) moveZ -= 1f;

        bool isSprinting = AjustesControles.Pulsada(AjustesControles.Accion.Correr)
                           && !IsCrouching;
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