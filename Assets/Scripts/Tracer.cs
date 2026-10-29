using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Tracer : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 direction;
    private float length;
    private Transform shooter;
    private Rigidbody target;
    private LineRenderer line;
    private float dieAt;

    public static Tracer Spawn(Vector3 position, Vector3 velocity, Vector3 direction, float length, float width, Material material, Transform shooter, Rigidbody target, float lifetime)
    {
        GameObject bullet = new GameObject("Bullet");
        LineRenderer line = bullet.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = width;
        line.endWidth = width * 0.3f;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.15f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        };

        Tracer tracer = bullet.AddComponent<Tracer>();
        tracer.line = line;
        tracer.velocity = velocity;
        tracer.direction = direction;
        tracer.length = length;
        tracer.shooter = shooter;
        tracer.target = target;
        tracer.dieAt = Time.time + lifetime;
        bullet.transform.position = position;
        tracer.Draw();
        return tracer;
    }

    void Update()
    {
        Vector3 from = transform.position;
        Vector3 step = velocity * Time.deltaTime;

        RaycastHit[] hits = Physics.RaycastAll(from, step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        Collider struck = null;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(shooter) || hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            struck = hit.collider;
        }

        if (struck != null)
        {
            if (struck.attachedRigidbody == target)
                Debug.Log("Bullet hit the player");
            Destroy(gameObject);
            return;
        }

        transform.position = from + step;
        Draw();

        if (Time.time > dieAt)
            Destroy(gameObject);
    }

    private void Draw()
    {
        Vector3 head = transform.position;
        line.SetPosition(0, head);
        line.SetPosition(1, head - direction * length);
    }
}
