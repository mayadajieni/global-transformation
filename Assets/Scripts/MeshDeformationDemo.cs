using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
[ExecuteInEditMode]
public class MeshDeformationDemo : MonoBehaviour
{
    public enum DeformMode
    {
        FFD,
        Bend,
        Twist,
        Taper
    }
    [Header("变形模式选择")]
    public DeformMode deformMode = DeformMode.FFD;
    [Header("FFD设置")]
    public int gridSizeX = 4;
    public int gridSizeY = 4;
    public int gridSizeZ = 4;
    public Vector3[] controlPoints;
    [Header("Bend 弯曲参数【C4D原版弯曲，限制框内弯曲，框外刚性延伸】")]
    public float bendStrength = 0.3f;
    [Tooltip("XAxis=左右弯曲(XY平面), ZAxis=前后弯曲(ZY平面)")]
    public enum BendDir { XAxis, ZAxis }
    public BendDir bendDirection = BendDir.XAxis;
    [Header("Bend区间限制：Start=底部不动起点，End=弯曲最高点")]
    public float bendStartY = 0.0f;
    public float bendEndY = 2.0f;
    [Header("Twist 扭转参数")]
    public float twistAngle = 60f;
    public float twistStartY = 0;
    public float twistEndY = 2;
    [Header("Taper 锥化参数")]
    public float taperStrength = 0.5f;
    private MeshFilter mf;
    private Mesh _originalSharedMesh;
    private Mesh workingMesh;
    private bool _isDirty;
    void OnEnable()
    {
        mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("物体缺少MeshFilter或者没有Mesh！");
            return;
        }
        _originalSharedMesh = mf.sharedMesh;
        workingMesh = new Mesh();
        workingMesh.name = "DeformMesh";
        Bounds b = _originalSharedMesh.bounds;
        twistStartY = b.min.y;
        twistEndY = b.max.y;
        bendStartY = b.min.y;
        bendEndY = b.max.y;
        _isDirty = true;
        ScheduleUpdate();
    }
    void Update()
    {
        if (_isDirty)
        {
            RecalculateMesh();
            _isDirty = false;
        }
    }
    void OnValidate()
    {
        if (mf == null) mf = GetComponent<MeshFilter>();
        if (bendStartY > bendEndY)
        {
            float temp = bendStartY;
            bendStartY = bendEndY;
            bendEndY = temp;
        }
        _isDirty = true;
        ScheduleUpdate();
    }
    void ScheduleUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null) RecalculateMesh();
            };
        }
#endif
    }
    public void RebuildFFDGrid()
    {
        if (mf == null || _originalSharedMesh == null) return;
        Bounds bounds = _originalSharedMesh.bounds;
        Vector3 step = new Vector3(
            bounds.size.x / (gridSizeX - 1),
            bounds.size.y / (gridSizeY - 1),
            bounds.size.z / (gridSizeZ - 1)
        );
        controlPoints = new Vector3[gridSizeX * gridSizeY * gridSizeZ];
        int idx = 0;
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    Vector3 pos = bounds.min + new Vector3(x * step.x, y * step.y, z * step.z);
                    controlPoints[idx++] = pos;
                }
            }
        }
        RecalculateMesh();
    }
    public void RecalculateMesh()
    {
        if (mf == null || _originalSharedMesh == null || workingMesh == null) return;
        Vector3[] verts = _originalSharedMesh.vertices.Clone() as Vector3[];
        switch (deformMode)
        {
            case DeformMode.FFD:
                ApplyFFD(verts);
                break;
            case DeformMode.Bend:
                ApplyBend(verts);
                break;
            case DeformMode.Twist:
                ApplyTwist(verts);
                break;
            case DeformMode.Taper:
                ApplyTaper(verts);
                break;
        }
        workingMesh.vertices = verts;
        workingMesh.normals = _originalSharedMesh.normals;
        workingMesh.uv = _originalSharedMesh.uv;
        workingMesh.triangles = _originalSharedMesh.triangles;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
        mf.mesh = workingMesh;
    }
    #region FFD 三线性插值
    void ApplyFFD(Vector3[] verts)
    {
        if (controlPoints == null) return;
        Bounds b = _originalSharedMesh.bounds;
        int nx = gridSizeX;
        int ny = gridSizeY;
        int nz = gridSizeZ;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 localPos = verts[i];
            float u = (localPos.x - b.min.x) / b.size.x * (nx - 1);
            float v = (localPos.y - b.min.y) / b.size.y * (ny - 1);
            float w = (localPos.z - b.min.z) / b.size.z * (nz - 1);
            int ix = Mathf.FloorToInt(u);
            int iy = Mathf.FloorToInt(v);
            int iz = Mathf.FloorToInt(w);
            ix = Mathf.Clamp(ix, 0, nx - 2);
            iy = Mathf.Clamp(iy, 0, ny - 2);
            iz = Mathf.Clamp(iz, 0, nz - 2);
            float fu = u - ix;
            float fv = v - iy;
            float fw = w - iz;
            int i000 = GetIndex(ix, iy, iz);
            int i100 = GetIndex(ix + 1, iy, iz);
            int i010 = GetIndex(ix, iy + 1, iz);
            int i110 = GetIndex(ix + 1, iy + 1, iz);
            int i001 = GetIndex(ix, iy, iz + 1);
            int i101 = GetIndex(ix + 1, iy, iz + 1);
            int i011 = GetIndex(ix, iy + 1, iz + 1);
            int i111 = GetIndex(ix + 1, iy + 1, iz + 1);
            Vector3 p000 = controlPoints[i000];
            Vector3 p100 = controlPoints[i100];
            Vector3 p010 = controlPoints[i010];
            Vector3 p110 = controlPoints[i110];
            Vector3 p001 = controlPoints[i001];
            Vector3 p101 = controlPoints[i101];
            Vector3 p011 = controlPoints[i011];
            Vector3 p111 = controlPoints[i111];
            Vector3 result =
                (1 - fu) * (1 - fv) * (1 - fw) * p000 +
                fu * (1 - fv) * (1 - fw) * p100 +
                (1 - fu) * fv * (1 - fw) * p010 +
                fu * fv * (1 - fw) * p110 +
                (1 - fu) * (1 - fv) * fw * p001 +
                fu * (1 - fv) * fw * p101 +
                (1 - fu) * fv * fw * p011 +
                fu * fv * fw * p111;
            verts[i] = result;
        }
    }
    int GetIndex(int x, int y, int z)
    {
        return x + gridSizeX * (y + gridSizeY * z);
    }
    #endregion

    #region Bend【C4D原版弯曲，限制框内弯曲，框外刚性延伸】
    void ApplyBend(Vector3[] verts)
    {
        float lengthY = bendEndY - bendStartY;
        if (lengthY < Mathf.Epsilon) return;

        float totalAngleRad = bendStrength;
        if (Mathf.Abs(totalAngleRad) < Mathf.Epsilon) return;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 p = verts[i];
            float localY = p.y - bendStartY;

            float bendT;
            float excess;

            if (localY <= 0f)
            {
                bendT = 0f;
                excess = localY;
            }
            else if (localY >= lengthY)
            {
                bendT = 1f;
                excess = localY - lengthY;
            }
            else
            {
                bendT = localY / lengthY;
                excess = 0f;
            }

            float theta = bendT * totalAngleRad;
            Vector3 bentPos = Vector3.zero;

            if (bendDirection == BendDir.XAxis)
            {
                float offset = p.x;
                float s = Mathf.Sin(theta);
                float c = Mathf.Cos(theta);

                float newX = offset + bendT * s * (lengthY - offset * (1f - c));
                float newY = bendStartY + localY * c + (1f - bendT) * offset * s;
                bentPos.x = newX;
                bentPos.y = newY;
                bentPos.z = p.z;

                if (Mathf.Abs(excess) > Mathf.Epsilon)
                {
                    float endS = Mathf.Sin(totalAngleRad);
                    float endC = Mathf.Cos(totalAngleRad);
                    bentPos.x += excess * endS;
                    bentPos.y += excess * endC;
                }
            }
            else
            {
                float offset = p.z;
                float s = Mathf.Sin(theta);
                float c = Mathf.Cos(theta);

                float newZ = offset + bendT * s * (lengthY - offset * (1f - c));
                float newY = bendStartY + localY * c + (1f - bendT) * offset * s;
                bentPos.z = newZ;
                bentPos.y = newY;
                bentPos.x = p.x;

                if (Mathf.Abs(excess) > Mathf.Epsilon)
                {
                    float endS = Mathf.Sin(totalAngleRad);
                    float endC = Mathf.Cos(totalAngleRad);
                    bentPos.z += excess * endS;
                    bentPos.y += excess * endC;
                }
            }
            verts[i] = bentPos;
        }
    }
    #endregion

    #region Twist【沿Y轴扭转，带区间限制】
    void ApplyTwist(Vector3[] verts)
    {
        Bounds b = _originalSharedMesh.bounds;
        float minY = b.min.y;
        float maxY = b.max.y;
        float rangeY = maxY - minY;
        if (rangeY < Mathf.Epsilon) return;
        float totalTwistRad = twistAngle * Mathf.Deg2Rad;
        for (int i = 0; i < verts.Length; i++)
        {
            float y = verts[i].y;
            float clampedY = Mathf.Clamp(y, twistStartY, twistEndY);
            float t = (clampedY - twistStartY) / (twistEndY - twistStartY);
            float rad = t * totalTwistRad;
            float cosA = Mathf.Cos(rad);
            float sinA = Mathf.Sin(rad);
            float x = verts[i].x;
            float z = verts[i].z;
            verts[i].x = x * cosA - z * sinA;
            verts[i].z = x * sinA + z * cosA;
        }
    }
    #endregion
    #region Taper 锥化
    void ApplyTaper(Vector3[] verts)
    {
        Bounds b = _originalSharedMesh.bounds;
        float minY = b.min.y;
        float maxY = b.max.y;
        float rangeY = maxY - minY;
        if (rangeY < Mathf.Epsilon) return;
        for (int i = 0; i < verts.Length; i++)
        {
            float t = (verts[i].y - minY) / rangeY;
            float scale = 1 - t * taperStrength;
            scale = Mathf.Max(scale, 0.01f);
            verts[i].x *= scale;
            verts[i].z *= scale;
        }
    }
    #endregion
}
