using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FFD_EditorWindow : EditorWindow
{
    public MeshDeformationDemo targetFFD;
    List<int> selectedPointIndices = new List<int>();
    private int hoverPoint = -1;
    private float pointHandleSize = 0.15f;

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        titleContent = new GUIContent("FFD变形编辑器");
    }
    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    [MenuItem("FFD工具/打开FFD变形编辑器")]
    static void ShowWindow()
    {
        GetWindow<FFD_EditorWindow>("FFD变形编辑器");
    }

    void OnGUI()
    {
        // ========== 顶部工具栏 变形模式选择 ==========
        EditorGUILayout.Space();
        GUILayout.Label("【变形模式工具栏】", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(targetFFD?.deformMode == MeshDeformationDemo.DeformMode.FFD, "FFD自由变形", "Button"))
        {
            if (targetFFD != null)
            {
                Undo.RecordObject(targetFFD, "切换模式FFD");
                targetFFD.deformMode = MeshDeformationDemo.DeformMode.FFD;
                EditorUtility.SetDirty(targetFFD);
            }
        }
        if (GUILayout.Toggle(targetFFD?.deformMode == MeshDeformationDemo.DeformMode.Bend, "Bend弯曲", "Button"))
        {
            if (targetFFD != null)
            {
                Undo.RecordObject(targetFFD, "切换模式Bend");
                targetFFD.deformMode = MeshDeformationDemo.DeformMode.Bend;
                EditorUtility.SetDirty(targetFFD);
            }
        }
        if (GUILayout.Toggle(targetFFD?.deformMode == MeshDeformationDemo.DeformMode.Twist, "Twist扭曲", "Button"))
        {
            if (targetFFD != null)
            {
                Undo.RecordObject(targetFFD, "切换模式Twist");
                targetFFD.deformMode = MeshDeformationDemo.DeformMode.Twist;
                EditorUtility.SetDirty(targetFFD);
            }
        }
        if (GUILayout.Toggle(targetFFD?.deformMode == MeshDeformationDemo.DeformMode.Taper, "Taper锥化", "Button"))
        {
            if (targetFFD != null)
            {
                Undo.RecordObject(targetFFD, "切换模式Taper");
                targetFFD.deformMode = MeshDeformationDemo.DeformMode.Taper;
                EditorUtility.SetDirty(targetFFD);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        targetFFD = (MeshDeformationDemo)EditorGUILayout.ObjectField("目标物体", targetFFD, typeof(MeshDeformationDemo), true);
        if (targetFFD == null)
        {
            EditorGUILayout.HelpBox("请在场景选中挂载MeshDeformationDemo脚本的物体", MessageType.Warning);
            return;
        }

        // FFD参数面板
        if (targetFFD.deformMode == MeshDeformationDemo.DeformMode.FFD)
        {
            EditorGUILayout.LabelField("FFD控制点设置", EditorStyles.boldLabel);
            targetFFD.gridSizeX = EditorGUILayout.IntSlider("X方向点数", targetFFD.gridSizeX, 2, 6);
            targetFFD.gridSizeY = EditorGUILayout.IntSlider("Y方向点数", targetFFD.gridSizeY, 2, 6);
            targetFFD.gridSizeZ = EditorGUILayout.IntSlider("Z方向点数", targetFFD.gridSizeZ, 2, 6);

            if (GUILayout.Button("重建FFD网格"))
            {
                Undo.RecordObject(targetFFD, "重建FFD网格");
                targetFFD.RebuildFFDGrid();
                selectedPointIndices.Clear();
                SceneView.RepaintAll();
            }
            EditorGUILayout.LabelField($"已选中控制点数量：{selectedPointIndices.Count}");
        }

        // Bend参数
        if (targetFFD.deformMode == MeshDeformationDemo.DeformMode.Bend)
        {
            EditorGUILayout.LabelField("弯曲参数", EditorStyles.boldLabel);
            targetFFD.bendStrength = EditorGUILayout.Slider("弯曲强度", targetFFD.bendStrength, 0, 3f);
        }

        // Twist参数
        if (targetFFD.deformMode == MeshDeformationDemo.DeformMode.Twist)
        {
            EditorGUILayout.LabelField("扭曲参数", EditorStyles.boldLabel);
            targetFFD.twistAngle = EditorGUILayout.Slider("扭曲角度", targetFFD.twistAngle, -180, 180);
        }

        // Taper参数
        if (targetFFD.deformMode == MeshDeformationDemo.DeformMode.Taper)
        {
            EditorGUILayout.LabelField("锥化参数", EditorStyles.boldLabel);
            targetFFD.taperStrength = EditorGUILayout.Slider("锥化强度", targetFFD.taperStrength, 0, 1f);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("应用变形到模型（Bake）"))
        {
            Undo.RecordObject(targetFFD.GetComponent<MeshFilter>().sharedMesh, "Bake变形");
            EditorUtility.SetDirty(targetFFD.GetComponent<MeshFilter>().sharedMesh);
        }
    }

    void OnSceneGUI(SceneView sv)
    {
        if (targetFFD == null) return;

        // 只有FFD模式才绘制控制点和网格线
        if (targetFFD.deformMode == MeshDeformationDemo.DeformMode.FFD && targetFFD.controlPoints != null)
        {
            Transform t = targetFFD.transform;
            Vector3[] pts = targetFFD.controlPoints;
            int nx = targetFFD.gridSizeX;
            int ny = targetFFD.gridSizeY;
            int nz = targetFFD.gridSizeZ;

            // 绘制FFD网格棱线
            Handles.color = Color.gray;
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        int idx = x + nx * (y + ny * z);
                        Vector3 worldPos = t.TransformPoint(pts[idx]);
                        if (x < nx - 1)
                            Handles.DrawLine(worldPos, t.TransformPoint(pts[idx + 1]));
                        if (y < ny - 1)
                            Handles.DrawLine(worldPos, t.TransformPoint(pts[idx + nx]));
                        if (z < nz - 1)
                            Handles.DrawLine(worldPos, t.TransformPoint(pts[idx + nx * ny]));
                    }
                }
            }

            // 控制点拾取 + Ctrl多选
            Event evt = Event.current;
            hoverPoint = -1;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 worldPos = t.TransformPoint(pts[i]);
                bool selected = selectedPointIndices.Contains(i);
                Handles.color = selected ? Color.red : Color.yellow;
                float size = HandleUtility.GetHandleSize(worldPos) * pointHandleSize;
                if (Handles.Button(worldPos, Quaternion.identity, size, size, Handles.SphereHandleCap))
                {
                    if (evt.control)
                    {
                        if (selectedPointIndices.Contains(i))
                            selectedPointIndices.Remove(i);
                        else
                            selectedPointIndices.Add(i);
                    }
                    else
                    {
                        selectedPointIndices.Clear();
                        selectedPointIndices.Add(i);
                    }
                    evt.Use();
                }
            }

            // 多选控制点局部坐标系移动
            if (selectedPointIndices.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 worldOrigin = t.TransformPoint(pts[selectedPointIndices[0]]);
                Vector3 newWorldPos = Handles.DoPositionHandle(worldOrigin, t.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetFFD, "移动FFD控制点");
                    Vector3 worldDelta = newWorldPos - worldOrigin;
                    Vector3 localDelta = t.InverseTransformDirection(worldDelta);
                    foreach (int pid in selectedPointIndices)
                    {
                        pts[pid] += localDelta;
                    }
                    targetFFD.controlPoints = pts;
                    targetFFD.RecalculateMesh();
                    EditorUtility.SetDirty(targetFFD);
                }
            }
        }
        sv.Repaint();
    }
}
