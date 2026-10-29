using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class HeadlightBeam : MonoBehaviour
{
    [Range(0.1f, 1f)]
    public float length = 0.6f;
    [Range(0.1f, 1f)]
    public float spread = 0.8f;
    public float startRadius = 0.15f;
    public float intensity = 0.6f;
    public float edgeSoftness = 2f;
    public bool useLightColor = true;
    public Color color = new Color(0.6f, 0.8f, 1f);

    private const int Segments = 32;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");

    private Light spot;
    private Mesh mesh;
    private Material material;
    private Vector3 built;

    void OnEnable()
    {
        spot = GetComponent<Light>();
        mesh = new Mesh { name = "Headlight Beam", hideFlags = HideFlags.HideAndDontSave };
        material = new Material(Shader.Find("Custom/HeadlightBeam")) { hideFlags = HideFlags.HideAndDontSave };
        built = Vector3.zero;
    }

    void OnDisable()
    {
        Release(mesh);
        Release(material);
    }

    void Update()
    {
        float beamLength = spot.range * length;
        float endRadius = Mathf.Max(startRadius, Mathf.Tan(spot.spotAngle * 0.5f * Mathf.Deg2Rad) * beamLength * spread);
        Vector3 shape = new Vector3(beamLength, startRadius, endRadius);
        if (shape != built)
            Build(shape);

        material.SetColor(ColorId, useLightColor ? spot.color : color);
        material.SetFloat(IntensityId, intensity);
        material.SetFloat(EdgeSoftnessId, edgeSoftness);

        RenderParams renderParams = new RenderParams(material)
        {
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false
        };
        Graphics.RenderMesh(renderParams, mesh, 0, transform.localToWorldMatrix);
    }

    private void Build(Vector3 shape)
    {
        float beamLength = shape.x;
        float slope = (shape.z - shape.y) / beamLength;

        Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[Segments * 6];

        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector3 around = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 normal = (around - Vector3.forward * slope).normalized;

            vertices[i * 2] = around * shape.y;
            vertices[i * 2 + 1] = around * shape.z + Vector3.forward * beamLength;
            normals[i * 2] = normal;
            normals[i * 2 + 1] = normal;
            colors[i * 2] = Color.white;
            colors[i * 2 + 1] = new Color(1f, 1f, 1f, 0f);
        }

        for (int i = 0; i < Segments; i++)
        {
            int v = i * 2;
            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 2;
            triangles[t + 4] = v + 1;
            triangles[t + 5] = v + 3;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        built = shape;
    }

    private static void Release(Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
