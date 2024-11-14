using UnityEngine;

public class FirstPersonController : MonoBehaviour
{
    [SerializeField] Camera m_cam;

    [Header("Collisions")]
    [SerializeField] float m_bodyHeight = 2f;
    [SerializeField] float m_bodyWidth = 1f;
    [SerializeField] Vector3 m_groundCheckOffset = Vector3.down * 0.51f;
    [SerializeField] float m_groundCheckRadius = 0.5f;
    [SerializeField] LayerMask m_collisionLayerMask = ~0;

    [Header("Movement properties")]
    [SerializeField] float m_groundAccel = 50f;
    [SerializeField] float m_airAccel = 5f;
    [SerializeField] float m_groundFriction = 0.2f;
    [SerializeField] float m_airFriction = 0.01f;
    [SerializeField] float m_jumpSpeed = 5f;
    [SerializeField] float m_mouseSensitivity = 2f;
    [SerializeField] Vector3 m_gravity = Vector3.down * 9.81f;

    [Header("Inputs")]
    [SerializeField] KeyCode m_forwardKey = KeyCode.W;
    [SerializeField] KeyCode m_backwardKey = KeyCode.S;
    [SerializeField] KeyCode m_leftKey = KeyCode.A;
    [SerializeField] KeyCode m_rightKey = KeyCode.D;
    [SerializeField] KeyCode m_jumpKey = KeyCode.Space;

    const uint MAX_COLLISION_STEPS = 10;
    const float SAFE_MARGIN = 0.001f;
    const float STOP_EPSILON = 0.0001f;
    const float JUMP_TIMER_BUFFER = 0.3f;


    Vector3 m_velocity, m_inputDir, m_previousPos, m_currentPos;
    float m_xRot, m_yRot, m_jumpTimer;
    bool m_isGrounded;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        m_previousPos = transform.position;
        m_currentPos = transform.position;
    }

    void Update()
    {
        HandleKbInputs();
        MouseLook();

        float interpolationFactor = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        transform.position = Vector3.Lerp(m_previousPos, m_currentPos, interpolationFactor);
    }

    void FixedUpdate()
    {
        m_previousPos = m_currentPos;

        m_isGrounded = Physics.CheckSphere(transform.position + m_groundCheckOffset, m_groundCheckRadius, m_collisionLayerMask);

        //Apply user kb inputs
        float speed = m_isGrounded ? m_groundAccel : m_airAccel;
        m_velocity += transform.TransformDirection(m_inputDir) * speed * Time.fixedDeltaTime;

        //Apply gravity
        m_velocity += m_gravity * Time.fixedDeltaTime;

        //Apply horizontal friction
        ApplyFriction();

        //Jump
        HandleJump();

        //Compute next position and handle collisions
        Vector3 clippedVelocity = ClipVelocity();

        //Prevent micro movements
        clippedVelocity = PreventJittering(clippedVelocity);

        //Move
        Vector3 positionBeforeMoving = m_currentPos;
        m_currentPos += clippedVelocity;
        m_velocity = (m_currentPos - positionBeforeMoving) / Time.fixedDeltaTime;
    }

    void ApplyFriction()
    {
        if (m_isGrounded)
        {
            m_velocity.x = Mathf.Lerp(m_velocity.x, 0f, m_groundFriction);
            m_velocity.z = Mathf.Lerp(m_velocity.z, 0f, m_groundFriction);
        }
        else
        {
            m_velocity.x = Mathf.Lerp(m_velocity.x, 0f, m_airFriction);
            m_velocity.z = Mathf.Lerp(m_velocity.z, 0f, m_airFriction);
        }
    }

    void HandleJump()
    {
        if (Time.time - m_jumpTimer < JUMP_TIMER_BUFFER)
        {
            if (m_isGrounded)
            {
                m_jumpTimer = JUMP_TIMER_BUFFER;
                m_velocity.y = m_jumpSpeed;
            }
        }
    }

    Vector3 ClipVelocity()
    {
        Vector3 v = m_velocity * Time.fixedDeltaTime;

        Vector3 p1 = m_currentPos + Vector3.down * (m_bodyHeight * 0.5f - m_bodyWidth * 0.5f);
        Vector3 p2 = m_currentPos + Vector3.up * (m_bodyHeight * 0.5f - m_bodyWidth * 0.5f);
        Vector3 initial_p1 = p1;
        Vector3 initial_p2 = p2;

        //Check for collisions and slide along collision planes
        uint iter = 0;
        while (Physics.CapsuleCast(p1, p2, m_bodyWidth * 0.5f, v.normalized, out RaycastHit hit, v.magnitude, m_collisionLayerMask) && iter < MAX_COLLISION_STEPS)
        {
            iter++;

            Vector3 actualTravel = v.normalized * hit.distance;

            p1 += actualTravel + hit.normal * SAFE_MARGIN;
            p2 += actualTravel + hit.normal * SAFE_MARGIN;

            Vector3 remainder = v - actualTravel;
            v = Vector3.ProjectOnPlane(remainder, hit.normal);
        }

        Vector3 finalPos = (p1 + p2) * 0.5f + v;
        Vector3 finalVelocity = finalPos - m_currentPos;

        //Check for collision for the final velocity
        //This prevent going through a wall when the angle is acute
        if (Physics.CapsuleCast(initial_p1, initial_p2, m_bodyWidth * 0.5f, finalVelocity.normalized, finalVelocity.magnitude, m_collisionLayerMask))
        {
            finalVelocity = Vector3.zero;
        }

        return finalVelocity;
    }

    Vector3 PreventJittering(Vector3 v)
    {
        if (v.x > -STOP_EPSILON && v.x < STOP_EPSILON) v.x = 0f;
        if (v.y > -STOP_EPSILON && v.y < STOP_EPSILON) v.y = 0f;
        if (v.z > -STOP_EPSILON && v.z < STOP_EPSILON) v.z = 0f;

        return v;
    }

    void HandleKbInputs()
    {
        //Fell free to update this with the new input system or whatever

        //Horizontal movement
        Vector3 inputDir = Vector3.zero;
        if (Input.GetKey(m_forwardKey)) inputDir += Vector3.forward;
        if (Input.GetKey(m_backwardKey)) inputDir += Vector3.back;
        if (Input.GetKey(m_leftKey)) inputDir += Vector3.left;
        if (Input.GetKey(m_rightKey)) inputDir += Vector3.right;
        m_inputDir = inputDir.normalized;

        //Jump
        if (Input.GetKeyDown(m_jumpKey)) m_jumpTimer = Time.time;
    }

    void MouseLook()
    {
        m_yRot += Input.GetAxisRaw("Mouse X") * m_mouseSensitivity;
        m_xRot -= Input.GetAxisRaw("Mouse Y") * m_mouseSensitivity;
        m_xRot = Mathf.Clamp(m_xRot, -90f, 90f);

        transform.localRotation = Quaternion.Euler(0, m_yRot, 0);
        m_cam.transform.localRotation = Quaternion.Euler(m_xRot, 0, 0);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        DrawGizmoCapsule(m_bodyHeight, m_bodyWidth, transform.position);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position + m_groundCheckOffset, m_groundCheckRadius);
    }

    void DrawGizmoCapsule(float height, float width, Vector3 center)
    {
        float radius = width * 0.5f;

        Vector3 p1 = center + Vector3.down * (height * 0.5f - radius);
        Vector3 p2 = center + Vector3.up * (height * 0.5f - radius);

        Gizmos.DrawWireSphere(p1, radius);
        Gizmos.DrawWireSphere(p2, radius);
        Gizmos.DrawLine(p1 + Vector3.right * radius, p2 + Vector3.right * radius);
        Gizmos.DrawLine(p1 + Vector3.left * radius, p2 + Vector3.left * radius);
        Gizmos.DrawLine(p1 + Vector3.forward * radius, p2 + Vector3.forward * radius);
        Gizmos.DrawLine(p1 + Vector3.back * radius, p2 + Vector3.back * radius);
    }

    /// <summary>
    /// You may want to call this just after moving the player
    /// </summary>
    public void ResetPhysicsInterpolation()
    {
        m_previousPos = transform.position;
        m_currentPos = transform.position;
    }

    /// <summary>
    /// You may want to call this just after rotating the player
    /// </summary>
    public void ResetMouseLook()
    {
        m_xRot = m_cam.transform.localRotation.eulerAngles.x;
        m_yRot = transform.localRotation.eulerAngles.y;
    }

    public void SetPosition(Vector3 p)
    {
        transform.position = p;
        ResetPhysicsInterpolation();
    }

    public void SetRotation(Quaternion r)
    {
        transform.rotation = r;
        ResetMouseLook();
    }

    public void SetRotation(Vector3 r)
    {
        transform.rotation = Quaternion.Euler(r);
        ResetMouseLook();
    }
}