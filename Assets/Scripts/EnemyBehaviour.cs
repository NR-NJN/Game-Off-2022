using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyMove { Ram, Wave, Burst }

[RequireComponent(typeof(Rigidbody))]
public class EnemyBehaviour : MonoBehaviour
{
    private enum Mode { Rest, Charge, Pullback, Retreat }

    public GameObject follow;

    [Range(0f, 1f)]
    public float screenPosition = 0.12f;
    public float catchUp = 2f;
    public float maxCatchUpSpeed = 15f;
    public float acceleration = 80f;
    public float braking = 20f;
    public float laneSnap = 8f;
    public float laneSpeed = 12f;

    [Header("Moves")]
    public Vector2 restTime = new Vector2(3f, 6f);
    public EnemyMove[] moves = { EnemyMove.Ram, EnemyMove.Wave, EnemyMove.Burst };
    [Min(1)]
    public int maxRepeats = 3;

    [Header("Ram")]
    public Light[] headlights;
    public int flashes = 3;
    public float flashOn = 0.08f;
    public float flashOff = 0.1f;
    public float chargeDelay = 0.45f;
    public float chargeSpeed = 40f;
    public float chargeResponse = 150f;
    public float chargeOvershoot = 6f;
    public float pullbackSpeed = 35f;
    public float maxChargeTime = 3f;

    [Header("Burst")]
    public Minigun minigun;
    public float burstDuration = 10f;
    public float roundInterval = 0.07f;
    public float lockTime = 0.6f;
    public float missBehind = 2.5f;
    [Range(0.5f, 1f)]
    public float topSpeedTolerance = 0.95f;
    public float hitSpread = 0.25f;
    public Vector3 aimOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Wave")]
    public MiniTukTuk miniTukTuk;
    [Range(0.3f, 1.5f)]
    public float miniSize = 0.85f;
    public float miniLift = 0f;
    public float retreatViewport = -0.4f;
    public float spawnViewport = -0.15f;
    public float holdViewport = 0.05f;
    public float waveDelay = 0.8f;
    public float waveInterval = 0.7f;

    private Rigidbody rb;
    private Rigidbody target;
    private Lanes lanes;
    private PlayerMovementController player;
    private Camera cam;
    private Collider[] ownColliders;
    private Collider[] targetColliders;

    private Mode mode;
    private int lane;
    private bool trackLane;
    private bool hitTarget;
    private float bodyLength;
    private string lastOrder;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        target = follow.GetComponent<Rigidbody>();
        player = follow.GetComponent<PlayerMovementController>();
        lanes = player.lanes;
        cam = Camera.main;
        ownColliders = GetComponentsInChildren<Collider>();
        targetColliders = follow.GetComponentsInChildren<Collider>();
        lane = lanes.Nearest(rb.position.z);
        bodyLength = Length(gameObject);
        SetHeadlights(false);
    }

    void Start()
    {
        StartCoroutine(Moves());
    }

    void FixedUpdate()
    {
        if (trackLane)
            lane = lanes.current;

        switch (mode)
        {
            case Mode.Charge:
                Steer(new Vector3(rb.position.x + chargeSpeed, 0f, LaneZ()), chargeSpeed, chargeResponse, chargeResponse);
                break;
            case Mode.Pullback:
                Steer(RestPoint(), pullbackSpeed, chargeResponse, chargeResponse);
                break;
            case Mode.Retreat:
                Steer(ScreenPoint(retreatViewport, lane), pullbackSpeed, chargeResponse, chargeResponse);
                break;
            default:
                Steer(RestPoint(), maxCatchUpSpeed, acceleration, braking);
                break;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.rigidbody != target)
            return;

        if (mode == Mode.Charge)
        {
            hitTarget = true;
            Debug.Log("Tuk-tuk rammed the player");
        }
        else
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = target.linearVelocity.x;
            rb.linearVelocity = velocity;
            Debug.Log("Tuk-tuk rear-ended the player");
        }
    }

    private IEnumerator Moves()
    {
        EnemyMove last = EnemyMove.Ram;
        int streak = 0;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(restTime.x, restTime.y));

            EnemyMove move = NextMove(last, streak);
            streak = move == last ? streak + 1 : 1;
            last = move;
            if (move == EnemyMove.Wave && miniTukTuk != null)
                yield return Wave();
            else if (move == EnemyMove.Burst && minigun != null)
                yield return Burst();
            else
                yield return Ram();
        }
    }

    private EnemyMove NextMove(EnemyMove last, int streak)
    {
        List<EnemyMove> pool = new List<EnemyMove>();
        foreach (EnemyMove move in moves)
            if (!pool.Contains(move))
                pool.Add(move);

        if (pool.Count == 0)
            return EnemyMove.Ram;

        if (streak >= maxRepeats && pool.Count > 1)
            pool.Remove(last);

        return pool[Random.Range(0, pool.Count)];
    }

    private IEnumerator Burst()
    {
        minigun.SpinUp();
        while (!minigun.SpunUp)
        {
            minigun.AimAt(MissPoint(lanes.current));
            yield return null;
        }

        int gunLane = lanes.current;
        float laneSince = Time.time;
        float end = Time.time + burstDuration;
        float nextRound = Time.time;
        while (Time.time < end)
        {
            if (lanes.current != gunLane)
            {
                gunLane = lanes.current;
                laneSince = Time.time;
            }

            bool slow = player.speed.current < player.speed.maximum * topSpeedTolerance;
            bool locked = slow || Time.time - laneSince >= lockTime;

            Vector3 aim = locked ? target.position + aimOffset : MissPoint(gunLane);
            minigun.AimAt(aim);

            while (Time.time >= nextRound)
            {
                if (locked)
                    minigun.Fire(aim, target, hitSpread);
                else
                    minigun.Fire(aim, target);
                nextRound += roundInterval;
            }
            yield return null;
        }

        minigun.SpinDown();
        minigun.StopAiming();
    }

    private Vector3 MissPoint(int atLane)
    {
        return new Vector3(target.position.x - missBehind, target.position.y, lanes.Position(atLane));
    }

    private IEnumerator Wave()
    {
        SetIgnoreTarget(true);
        mode = Mode.Retreat;
        float end = Time.time + maxChargeTime;
        while (cam.WorldToViewportPoint(rb.position).x > retreatViewport + 0.1f && Time.time < end)
            yield return new WaitForFixedUpdate();

        List<MiniTukTuk> minis = new List<MiniTukTuk>();
        for (int i = 0; i < lanes.count; i++)
        {
            Vector3 spawn = ScreenPoint(spawnViewport, i);
            spawn.y = rb.position.y;
            MiniTukTuk mini = Instantiate(miniTukTuk, spawn, miniTukTuk.transform.rotation);
            float miniLength = Length(mini.gameObject);
            if (bodyLength > 0f && miniLength > 0f)
                mini.transform.localScale *= bodyLength * miniSize / miniLength;
            if (HasBounds(gameObject, out Bounds body) && HasBounds(mini.gameObject, out Bounds miniBody))
                mini.transform.position += Vector3.up * (body.min.y - miniBody.min.y + miniLift);
            mini.Init(target, holdViewport);
            minis.Add(mini);
        }

        yield return new WaitForSeconds(waveDelay);

        foreach (int i in DashOrder(minis.Count))
        {
            if (minis[i] != null)
                minis[i].Dash();
            yield return new WaitForSeconds(waveInterval);
        }

        while (minis.Exists(mini => mini != null))
            yield return null;

        mode = Mode.Pullback;
        end = Time.time + maxChargeTime;
        while (Mathf.Abs(rb.position.x - RestPoint().x) > 1f && Time.time < end)
            yield return new WaitForFixedUpdate();

        mode = Mode.Rest;
        SetIgnoreTarget(false);
    }

    private IEnumerator Ram()
    {
        trackLane = true;
        for (int i = 1; i <= flashes; i++)
        {
            if (i == flashes)
                trackLane = false;

            SetHeadlights(true);
            yield return new WaitForSeconds(flashOn);
            SetHeadlights(false);
            yield return new WaitForSeconds(flashOff);
        }

        yield return new WaitForSeconds(chargeDelay);

        SetHeadlights(true);
        hitTarget = false;
        mode = Mode.Charge;
        float end = Time.time + maxChargeTime;
        while (!hitTarget && rb.position.x < target.position.x + chargeOvershoot && Time.time < end)
            yield return new WaitForFixedUpdate();

        SetHeadlights(false);
        SetIgnoreTarget(true);
        mode = Mode.Pullback;
        end = Time.time + maxChargeTime;
        while (Mathf.Abs(rb.position.x - RestPoint().x) > 1f && Time.time < end)
            yield return new WaitForFixedUpdate();

        mode = Mode.Rest;
        SetIgnoreTarget(false);
    }

    private float LaneZ()
    {
        return lanes.Position(lane);
    }

    private int[] DashOrder(int count)
    {
        int[] order = new int[count];
        string key;
        do
        {
            for (int i = 0; i < count; i++)
                order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (order[i], order[swap]) = (order[swap], order[i]);
            }
            key = string.Join(",", order);
        }
        while (count > 1 && key == lastOrder);

        lastOrder = key;
        return order;
    }

    private static float Length(GameObject body)
    {
        return HasBounds(body, out Bounds bounds) ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f;
    }

    private static bool HasBounds(GameObject body, out Bounds bounds)
    {
        Renderer[] renderers = body.GetComponentsInChildren<Renderer>();
        bounds = renderers.Length > 0 ? renderers[0].bounds : default;
        foreach (Renderer part in renderers)
            bounds.Encapsulate(part.bounds);
        return renderers.Length > 0;
    }

    private Vector3 RestPoint()
    {
        return ScreenPoint(screenPosition, lane);
    }

    private Vector3 ScreenPoint(float viewportX, int atLane)
    {
        float z = lanes.Position(atLane);
        float x = cam.ViewportToWorldPoint(new Vector3(viewportX, 0.5f, z - cam.transform.position.z)).x;
        return new Vector3(x, 0f, z);
    }

    private void Steer(Vector3 point, float maxSpeed, float speedUp, float slowDown)
    {
        Vector3 offset = point - rb.position;
        float desiredX = target.linearVelocity.x + Mathf.Clamp(offset.x * catchUp, -maxSpeed, maxSpeed);
        float desiredZ = Mathf.Clamp(offset.z * laneSnap, -laneSpeed, laneSpeed);

        Vector3 velocity = rb.linearVelocity;
        float response = desiredX > velocity.x ? speedUp : slowDown;
        float x = Mathf.MoveTowards(velocity.x, desiredX, response * Time.fixedDeltaTime);

        rb.AddForce(x - velocity.x, 0f, desiredZ - velocity.z, ForceMode.VelocityChange);
    }

    private void SetHeadlights(bool on)
    {
        foreach (Light light in headlights)
            light.gameObject.SetActive(on);
    }

    private void SetIgnoreTarget(bool ignore)
    {
        foreach (Collider own in ownColliders)
            foreach (Collider other in targetColliders)
                Physics.IgnoreCollision(own, other, ignore);
    }
}
