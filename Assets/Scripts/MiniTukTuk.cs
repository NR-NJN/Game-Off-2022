using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MiniTukTuk : MonoBehaviour
{
    public float dashSpeed = 35f;
    public float arriveSpeed = 25f;
    public float despawnViewport = 1.2f;
    public float lifetime = 8f;

    private Rigidbody rb;
    private Rigidbody target;
    private Camera cam;
    private float holdViewport;
    private bool dashing;
    private bool hit;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        cam = Camera.main;

        if (GetComponentInChildren<Collider>() == null)
            AddBoxCollider();
        foreach (Collider part in GetComponentsInChildren<Collider>())
            part.isTrigger = true;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Init(Rigidbody player, float hold)
    {
        target = player;
        holdViewport = hold;
    }

    public void Dash()
    {
        dashing = true;
    }

    void FixedUpdate()
    {
        float depth = rb.position.z - cam.transform.position.z;
        float speed = target.linearVelocity.x;

        if (dashing)
        {
            speed += dashSpeed;
        }
        else
        {
            float holdX = cam.ViewportToWorldPoint(new Vector3(holdViewport, 0.5f, depth)).x;
            speed += Mathf.Clamp((holdX - rb.position.x) * 3f, -arriveSpeed, arriveSpeed);
        }

        rb.MovePosition(rb.position + Vector3.right * speed * Time.fixedDeltaTime);

        if (dashing && cam.WorldToViewportPoint(rb.position).x > despawnViewport)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hit || other.attachedRigidbody != target)
            return;

        hit = true;
        Debug.Log("Mini tuk-tuk hit the player");
    }

    private void AddBoxCollider()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer part in renderers)
            bounds.Encapsulate(part.bounds);

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);
        Vector3 size = transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }
}
