using UnityEngine;

// ====================================================================
// Controlador mínimo en primera persona, solo para poder probar el
// combate. WASD para moverte, mouse para mirar, Esc para liberar el
// cursor. Barra espaciadora para saltar y Shift Izquierdo para Dash.
// ====================================================================

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float gravity = -20f;

    [Header("Salto")]
    public float jumpHeight = 1.5f;

    [Header("Dash")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    [Header("Cámara")]
    public Transform cameraPivot;
    public float mouseSensitivity = 2f;

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraPitch;

    private Vector3 dashDirection;
    private float dashTimer;
    private float nextDashTime;
    private bool isDashing;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        HandleLook();
        HandleDashInput();
        HandleMove();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None
                : CursorLockMode.Locked;
        }

        if (UIManager.Instance != null)
        {
            float remainingDashTime = nextDashTime - Time.time;   
            if (remainingDashTime < 0) remainingDashTime = 0;
            UIManager.Instance.UpdateDashCooldown(remainingDashTime, dashCooldown);
        }
    }

    void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -80f, 80f);
        if (cameraPivot != null)
            cameraPivot.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
    }

    void HandleDashInput()
    {
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= nextDashTime)
        {
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            
            if (input.sqrMagnitude == 0f) 
                input = Vector3.forward;

            dashDirection = transform.TransformDirection(input.normalized);
            isDashing = true;
            dashTimer = dashDuration;
            nextDashTime = Time.time + dashCooldown;
        }
    }

    void HandleMove()
    {
        Vector3 finalMove;

        if (isDashing)
        {
            finalMove = dashDirection * dashSpeed;
            verticalVelocity = 0f; 
        }
        else
        {
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            finalMove = transform.TransformDirection(input.normalized) * moveSpeed;

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = -2f;

                if (Input.GetButtonDown("Jump"))
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        finalMove.y = verticalVelocity;
        controller.Move(finalMove * Time.deltaTime);
    }
}