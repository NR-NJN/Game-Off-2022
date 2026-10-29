using System.Collections;
using UnityEngine;

public class Minigun : MonoBehaviour
{
    public Transform muzzle;

    [Header("Aiming")]
    public Transform aimPivot;
    public float turnSpeed = 240f;
    public float maxAimAngle = 70f;

    [Header("Spin")]
    public Transform[] barrels;
    public float maxSpinSpeed = 1800f;
    public float spinUpTime = 0.8f;
    public float spinDownTime = 1.5f;

    [Header("Bullets")]
    public float bulletSpeed = 70f;
    public float bulletLength = 1.2f;
    public float bulletWidth = 0.06f;
    public float bulletLifetime = 2f;
    public float spread = 0.6f;
    public Color bulletColor = new Color(1f, 0.85f, 0.35f);
    public float bulletGlow = 5f;

    [Header("Muzzle Flash")]
    public float flashTime = 0.04f;
    public float flashSize = 1.2f;
    public Color flashColor = new Color(1f, 0.7f, 0.3f);
    public float flashGlow = 8f;
    public float flashLightIntensity = 10f;
    public float flashLightRange = 6f;

    private Rigidbody body;
    private Camera cam;
    private Material bulletMaterial;
    private Material flashMaterial;
    private GameObject flash;
    private Light flashLight;
    private Coroutine flashing;
    private float flashSpin;
    private Vector3 lastAim = Vector3.right;

    private bool aiming;
    private Vector3 aimPoint;
    private Quaternion restRotation;
    private Vector3 restPosition;
    private Vector3 restDirection;
    private Vector3 mountPoint;
    private Vector3 swivelAxis;

    private bool spinning;
    private float spin;
    private Vector3[] barrelCentres;
    private Vector3[] barrelAxes;

    public bool SpunUp => spin >= maxSpinSpeed * 0.98f;

    void Awake()
    {
        body = GetComponentInParent<Rigidbody>();
        cam = Camera.main;
        if (muzzle == null)
            muzzle = transform;

        Shader glow = Shader.Find("Custom/Glow");
        bulletMaterial = new Material(glow);
        bulletMaterial.SetColor("_Color", bulletColor);
        bulletMaterial.SetFloat("_Intensity", bulletGlow);

        flashMaterial = new Material(glow) { mainTexture = StarTexture(64) };
        flashMaterial.SetColor("_Color", flashColor);
        flashMaterial.SetFloat("_Intensity", flashGlow);

        flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flash.name = "Muzzle Flash";
        Destroy(flash.GetComponent<Collider>());
        MeshRenderer quad = flash.GetComponent<MeshRenderer>();
        quad.sharedMaterial = flashMaterial;
        quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quad.receiveShadows = false;

        flashLight = flash.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = flashColor;
        flashLight.intensity = flashLightIntensity;
        flashLight.range = flashLightRange;
        flashLight.shadows = LightShadows.None;

        flash.SetActive(false);

        if (aimPivot != null)
        {
            restRotation = aimPivot.localRotation;
            restPosition = aimPivot.localPosition;
            mountPoint = MountPoint();
            swivelAxis = PivotSpace(body != null ? body.transform.up : Vector3.up).normalized;
            restDirection = Flatten(muzzle.position - aimPivot.TransformPoint(mountPoint));
        }

        barrelCentres = new Vector3[barrels.Length];
        barrelAxes = new Vector3[barrels.Length];
        for (int i = 0; i < barrels.Length; i++)
        {
            MeshFilter mesh = barrels[i].GetComponent<MeshFilter>();
            Bounds bounds = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.forward);
            Vector3 size = bounds.size;
            barrelCentres[i] = bounds.center;
            barrelAxes[i] = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        }
    }

    void Update()
    {
        Aim();
        Spin();
    }

    public void AimAt(Vector3 point)
    {
        aiming = true;
        aimPoint = point;
    }

    public void StopAiming()
    {
        aiming = false;
    }

    public void SpinUp()
    {
        spinning = true;
    }

    public void SpinDown()
    {
        spinning = false;
    }

    private void Aim()
    {
        if (aimPivot == null)
            return;

        Quaternion goal = restRotation;
        if (aiming)
        {
            Vector3 wanted = Flatten(aimPoint - aimPivot.TransformPoint(mountPoint));
            if (wanted != Vector3.zero && restDirection != Vector3.zero)
            {
                Vector3 limited = Vector3.RotateTowards(restDirection, wanted, maxAimAngle * Mathf.Deg2Rad, 0f);
                goal = Quaternion.AngleAxis(Vector3.SignedAngle(restDirection, limited, swivelAxis), swivelAxis) * restRotation;
            }
        }

        aimPivot.localRotation = Quaternion.RotateTowards(aimPivot.localRotation, goal, turnSpeed * Time.deltaTime);
        Vector3 mount = Vector3.Scale(aimPivot.localScale, mountPoint);
        aimPivot.localPosition = restPosition + restRotation * mount - aimPivot.localRotation * mount;
    }

    private Vector3 Flatten(Vector3 world)
    {
        return Vector3.ProjectOnPlane(PivotSpace(world), swivelAxis).normalized;
    }

    private Vector3 MountPoint()
    {
        Bounds bounds = default;
        bool found = false;
        foreach (MeshFilter filter in aimPivot.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            Bounds local = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = aimPivot.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (found)
                    bounds.Encapsulate(point);
                else
                    bounds = new Bounds(point, Vector3.zero);
                found = true;
            }
        }
        return found ? bounds.center : Vector3.zero;
    }

    private void Spin()
    {
        float rate = maxSpinSpeed / (spinning ? spinUpTime : spinDownTime);
        spin = Mathf.MoveTowards(spin, spinning ? maxSpinSpeed : 0f, rate * Time.deltaTime);
        if (spin <= 0f)
            return;

        for (int i = 0; i < barrels.Length; i++)
        {
            Transform barrel = barrels[i];
            barrel.RotateAround(barrel.TransformPoint(barrelCentres[i]), barrel.TransformDirection(barrelAxes[i]), spin * Time.deltaTime);
        }
    }

    private Vector3 PivotSpace(Vector3 world)
    {
        return aimPivot.parent != null ? aimPivot.parent.InverseTransformDirection(world) : world;
    }

    void LateUpdate()
    {
        if (flash.activeSelf)
            PlaceFlash();
    }

    void OnDestroy()
    {
        if (flash != null)
            Destroy(flash);
    }

    private void PlaceFlash()
    {
        flash.transform.SetPositionAndRotation(
            muzzle.position + lastAim * flashSize * 0.3f,
            cam.transform.rotation * Quaternion.Euler(0f, 0f, flashSpin)
        );
    }

    public void Fire(Vector3 point, Rigidbody target)
    {
        Fire(point, target, spread);
    }

    public void Fire(Vector3 point, Rigidbody target, float scatter)
    {
        Vector3 from = muzzle.position;
        Vector2 jitter = Random.insideUnitCircle * scatter;
        Vector3 aim = (point + new Vector3(jitter.x, 0f, jitter.y) - from).normalized;
        Vector3 carried = body != null ? Vector3.right * body.linearVelocity.x : Vector3.zero;
        lastAim = aim;

        Tracer.Spawn(from, aim * bulletSpeed + carried, aim, bulletLength, bulletWidth, bulletMaterial, transform.root, target, bulletLifetime);

        if (flashing != null)
            StopCoroutine(flashing);
        flashing = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        flash.transform.localScale = Vector3.one * flashSize * Random.Range(0.75f, 1.25f);
        flashSpin = Random.Range(0f, 360f);
        PlaceFlash();
        flash.SetActive(true);
        yield return new WaitForSeconds(flashTime);
        flash.SetActive(false);
    }

    private static Texture2D StarTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                float core = Mathf.Clamp01(1f - radius * 2.2f);
                float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 12f) * Mathf.Clamp01(1f - radius);
                float alpha = Mathf.Clamp01(core * core + rays * 0.9f);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }
}
