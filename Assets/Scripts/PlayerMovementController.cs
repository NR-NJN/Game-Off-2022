using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class GroundCheckConstraints
{
    public float radius;
    public float offset;
    public bool grounded;

    public LayerMask groundLayer;
}

[Serializable]
public class Speed
{
    public float current;
    public float maximum;
    public float minimum;
    public float turning;
    public float acceleration;
    public float braking = 70f;
}

[Serializable]
public class Lanes
{
    public int count = 3;
    public float width = 2.8f;
    public float centre = 6.17f;
    public float snap = 12f;
    public int current = 1;

    public float Position(int lane)
    {
        return centre + (lane - (count - 1) * 0.5f) * width;
    }

    public int Nearest(float z)
    {
        return Mathf.Clamp(Mathf.RoundToInt((z - centre) / width + (count - 1) * 0.5f), 0, count - 1);
    }
}

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementController : MonoBehaviour
{
    public Speed speed;
    public Lanes lanes;

    public GroundCheckConstraints groundCheck;
    public float maxLeaning = 30;
    public float leanSmoothing = 12f;
    public float gravityScale = 1;

    private Rigidbody rb;
    private InputAction moveAction;
    private int steerHeld;
    private float lean;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
        lanes.current = lanes.Nearest(rb.position.z);
    }

    void FixedUpdate()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();

        float rate = move.x >= 0f ? speed.acceleration : speed.braking;
        speed.current = Mathf.Clamp(speed.current + move.x * rate * Time.fixedDeltaTime, speed.minimum, speed.maximum);

        int steer = move.y > 0.5f ? 1 : move.y < -0.5f ? -1 : 0;
        if (steer != 0 && steer != steerHeld)
            lanes.current = Mathf.Clamp(lanes.current + steer, 0, lanes.count - 1);
        steerHeld = steer;

        float offset = lanes.Position(lanes.current) - rb.position.z;
        float sideways = Mathf.Clamp(offset * lanes.snap, -speed.turning, speed.turning);

        Vector3 velocity = rb.linearVelocity;
        rb.AddForce(speed.current - velocity.x, 0, sideways - velocity.z, ForceMode.VelocityChange);

        lean = Mathf.Lerp(lean, sideways / speed.turning * maxLeaning, leanSmoothing * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Euler(lean, 0, 0));

        rb.AddForce(Physics.gravity * (gravityScale - 1), ForceMode.Acceleration);

        groundCheck.grounded = Physics.CheckSphere(
            transform.position - transform.up * groundCheck.offset, groundCheck.radius, groundCheck.groundLayer
        );
    }

    void OnDrawGizmos()
    {
        Gizmos.color = groundCheck.grounded ? Color.green : Color.red;
        Gizmos.DrawSphere(transform.position - transform.up * groundCheck.offset, groundCheck.radius);

        if (lanes == null)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < lanes.count; i++)
        {
            float z = lanes.Position(i);
            Gizmos.DrawLine(new Vector3(transform.position.x - 20f, 0.6f, z), new Vector3(transform.position.x + 40f, 0.6f, z));
        }
    }
}
