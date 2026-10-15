#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(OctreeSettings))]
public class Shadowbaker : Editor
{
    [SerializeField] private bool showBaking = true;
    [SerializeField] private bool showNodes;
    [SerializeField] private bool showOctreeVisualization;
    [SerializeField] private bool showProbeDebug;
    [SerializeField] private UltimateNodePlacer nodePlacer;
    [SerializeField] private ProbeBaker probeBaker;
    [SerializeField] private ProbeDebugVisualizer probeDebug;
    [SerializeField] private OctreeVisualizer octreeVisualizer = new OctreeVisualizer();
    [SerializeField] private int selectedTab = 0;
    private string[] tabs = { "Editor", "Generator", "Operations", "Display" };

    private void OnEnable()
    {
        OctreeSettings settings = (OctreeSettings)target;
        Transform root = settings.probeRootTransform != null ? settings.probeRootTransform : settings.transform;
        if (nodePlacer == null) nodePlacer = root.GetComponent<UltimateNodePlacer>();
        if (probeBaker == null) probeBaker = settings.GetComponent<ProbeBaker>();
        if (probeDebug == null) probeDebug = settings.GetComponent<ProbeDebugVisualizer>();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        OctreeSettings settings = (OctreeSettings)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shadow Baker", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        showBaking = EditorGUILayout.Foldout(showBaking, "Baking & Lightmap Operations", true);
        if (showBaking) DrawBaking(settings);

        showNodes = EditorGUILayout.Foldout(showNodes, "Node Placement & Editing", true);
        if (showNodes) DrawNodes(settings);

        showOctreeVisualization = EditorGUILayout.Foldout(showOctreeVisualization, "Octree Visualization", true);
        if (showOctreeVisualization) DrawOctreeVisualization();

        showProbeDebug = EditorGUILayout.Foldout(showProbeDebug, "Probe Debug Visualization", true);
        if (showProbeDebug) DrawProbeDebug(settings);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
    }

    private void DrawBaking(OctreeSettings settings)
    {
        OctreeBaker baker = new OctreeBaker(settings);
        if (GUILayout.Button("Calculate and Bake Octree Texture")) baker.BakeOctree();
        if (GUILayout.Button("Bake Probe Positions (Local Space)")) baker.BakeProbePositions();

        EditorGUILayout.Space();
        probeBaker = (ProbeBaker)EditorGUILayout.ObjectField("Probe Baker", probeBaker, typeof(ProbeBaker), true);
        if (probeBaker == null)
        {
            if (GUILayout.Button("Add Probe Baker")) probeBaker = Undo.AddComponent<ProbeBaker>(settings.gameObject);
            return;
        }

        DrawComponentProperties(probeBaker);
        if (GUILayout.Button("Bake Probe Positions (World Space)")) probeBaker.BakeProbePositions();
        if (GUILayout.Button("Bake Closest Probes Texture Array")) probeBaker.BakeClosestProbesTexture();
        if (GUILayout.Button("Apply Lightmap Indices Automatically")) probeBaker.ApplyLightmapIndices();
    }

    private void DrawNodes(OctreeSettings settings)
    {
        nodePlacer = (UltimateNodePlacer)EditorGUILayout.ObjectField("Node Placer", nodePlacer, typeof(UltimateNodePlacer), true);
        if (nodePlacer == null)
        {
            if (GUILayout.Button("Add Node Placer to Probe Root"))
            {
                Transform root = settings.probeRootTransform != null ? settings.probeRootTransform : settings.transform;
                nodePlacer = Undo.AddComponent<UltimateNodePlacer>(root.gameObject);
            }
            return;
        }

        UltimateNodePlacer placer = nodePlacer;
        Undo.RecordObject(placer, "Edit Node Placer Settings");
        EditorGUI.BeginChangeCheck();
        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
        GUILayout.Space(10);
        switch (selectedTab)
        {
            case 0: DrawEditorTab(placer); break;
            case 1: DrawGeneratorTab(placer); break;
            case 2: DrawOperationsTab(placer); break;
            case 3: DrawDisplayTab(placer); break;
        }
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(placer);
    }

    private void DrawOctreeVisualization()
    {
        octreeVisualizer.bakedTexture = (Texture2D)EditorGUILayout.ObjectField("Baked Octree Texture", octreeVisualizer.bakedTexture, typeof(Texture2D), false);
        octreeVisualizer.displayDepth = EditorGUILayout.IntSlider("Display Max Depth", octreeVisualizer.displayDepth, 0, 7);
        octreeVisualizer.showOnlyOccupied = EditorGUILayout.Toggle("Show Only Occupied", octreeVisualizer.showOnlyOccupied);
        octreeVisualizer.drawInScene = EditorGUILayout.Toggle("Draw in Scene View", octreeVisualizer.drawInScene);
        if (GUILayout.Button("Repaint Scene")) SceneView.RepaintAll();
        EditorGUILayout.HelpBox(
            "Cyan = Intermediate Node\n" +
            "Green = Leaf Node\n" +
            "Settings are pulled directly from this OctreeSettings GameObject.", MessageType.Info);
    }

    private void DrawProbeDebug(OctreeSettings settings)
    {
        probeDebug = (ProbeDebugVisualizer)EditorGUILayout.ObjectField("Probe Debug Visualizer", probeDebug, typeof(ProbeDebugVisualizer), true);
        if (probeDebug == null)
        {
            if (GUILayout.Button("Add Probe Debug Visualizer")) probeDebug = Undo.AddComponent<ProbeDebugVisualizer>(settings.gameObject);
            return;
        }
        DrawComponentProperties(probeDebug);
    }

    private static void DrawComponentProperties(MonoBehaviour component)
    {
        SerializedObject componentObject = new SerializedObject(component);
        componentObject.Update();
        DrawPropertiesExcluding(componentObject, "m_Script");
        componentObject.ApplyModifiedProperties();
    }

    private void DrawEditorTab(UltimateNodePlacer placer)
    {
        SerializedObject placerObject = new SerializedObject(placer);
        placerObject.Update();
        EditorGUILayout.PropertyField(placerObject.FindProperty("nodePrefab"));
        placerObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Interactive Scene Controls:\n" +
            "- Click node to select.\n" +
            "- Shift + Click: Add/Remove from selection.\n" +
            "- Ctrl + Drag (or Cmd + Drag): Marquee Box Selection.\n" +
            "- Shift + Click on colliders: Spawn new node.\n" +
            "- Ctrl + D: Duplicate selected nodes.\n" +
            "- Delete / Backspace: Remove selected nodes.", 
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Node at Center", GUILayout.Height(30)))
        {
            SpawnNode(placer, placer.transform.position);
        }
        if (GUILayout.Button("Duplicate Selected", GUILayout.Height(30)))
        {
            DuplicateSelectedNodes(placer);
        }
        EditorGUILayout.EndHorizontal();

        if (placer.selectedIndices.Count > 0)
        {
            if (GUILayout.Button($"Clear Selection ({placer.selectedIndices.Count} selected)", GUILayout.Height(25)))
            {
                placer.selectedIndices.Clear();
                Repaint();
            }
        }
    }

    private void DrawGeneratorTab(UltimateNodePlacer placer)
    {
        EditorGUILayout.LabelField("Procedural Grid Generator", EditorStyles.boldLabel);
        placer.generatorSize = EditorGUILayout.Vector3Field("Grid Size (Bounds)", placer.generatorSize);
        placer.generatorCount = EditorGUILayout.Vector3IntField("Node Counts (X, Y, Z)", placer.generatorCount);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Grid (Replace Existing)", GUILayout.Height(35)))
        {
            if (EditorUtility.DisplayDialog("Generate Grid", "This will replace all existing child nodes. Are you sure?", "Yes", "Cancel"))
            {
                GenerateGrid(placer);
            }
        }
        if (GUILayout.Button("Append Grid to Existing", GUILayout.Height(30)))
        {
            AppendGrid(placer);
        }
    }

    private void DrawOperationsTab(UltimateNodePlacer placer)
    {
        EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);

        if (GUILayout.Button("Center Nodes at Parent", GUILayout.Height(25)))
        {
            CenterNodes(placer);
        }

        if (GUILayout.Button("Jitter / Randomize Selected Positions", GUILayout.Height(25)))
        {
            JitterSelected(placer, 0.2f);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mirror Selection", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mirror X")) MirrorNodes(placer, Vector3.right);
        if (GUILayout.Button("Mirror Y")) MirrorNodes(placer, Vector3.up);
        if (GUILayout.Button("Mirror Z")) MirrorNodes(placer, Vector3.forward);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Texture Baking (Position Texture)", EditorStyles.boldLabel);
        placer.textureSavePath = EditorGUILayout.TextField("Save Path", placer.textureSavePath);
        if (GUILayout.Button("Bake Positions to 256x256 Texture", GUILayout.Height(35)))
        {
            BakePositionsToTexture(placer);
        }
    }

    private void DrawDisplayTab(UltimateNodePlacer placer)
    {
        EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);
        placer.handleSize = EditorGUILayout.Slider("Handle Size", placer.handleSize, 0.05f, 2.0f);
        placer.showConnections = EditorGUILayout.Toggle("Show Neighbor Lines", placer.showConnections);
        if (placer.showConnections)
        {
            placer.connectionDistance = EditorGUILayout.Slider("Connection Distance", placer.connectionDistance, 0.5f, 10.0f);
        }
        placer.showLabels = EditorGUILayout.Toggle("Show Node Indices", placer.showLabels);
    }

    private void OnSceneGUI()
    {
        if (showOctreeVisualization) octreeVisualizer.DrawScene((OctreeSettings)target);
        if (showNodes && nodePlacer != null) DrawNodeSceneGUI(nodePlacer);
    }

    private void DrawNodeSceneGUI(UltimateNodePlacer placer)
    {
        Transform parentTransform = placer.transform;
        int childCount = parentTransform.childCount;

        placer.selectedIndices.RemoveAll(index => index >= childCount);

        Event e = Event.current;

        // Handle Marquee Box Selection (Ctrl / Cmd + Drag)
        if (e.control || e.command)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                placer.isDraggingBox = true;
                placer.boxStartMousePos = e.mousePosition;
                placer.boxCurrentMousePos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && placer.isDraggingBox)
            {
                placer.boxCurrentMousePos = e.mousePosition;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && placer.isDraggingBox)
            {
                placer.isDraggingBox = false;
                PerformBoxSelection(placer);
                e.Use();
                Repaint();
            }
        }
        else if (placer.isDraggingBox && e.type == EventType.MouseUp)
        {
            placer.isDraggingBox = false;
            Repaint();
        }

        // Draw selection box UI rect
        if (placer.isDraggingBox)
        {
            Handles.BeginGUI();
            Rect rect = RectMinMax(placer.boxStartMousePos, placer.boxCurrentMousePos);
            EditorGUI.DrawRect(rect, new Color(0, 0.5f, 1f, 0.15f));
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0, 0.5f, 1f, 0.8f));
            Handles.EndGUI();
            Repaint();
        }

        // Keyboard shortcuts
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                DeleteSelectedNodes(placer);
                e.Use();
                Repaint();
            }
        }

        if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && e.commandName == "Duplicate")
        {
            if (e.type == EventType.ExecuteCommand)
            {
                DuplicateSelectedNodes(placer);
            }
            e.Use();
            Repaint();
        }

        // Draw Connection Lines between neighbor nodes
        if (placer.showConnections && childCount > 1)
        {
            Handles.color = new Color(0f, 0.8f, 1f, 0.25f);
            for (int i = 0; i < childCount; i++)
            {
                Transform a = parentTransform.GetChild(i);
                if (a == null) continue;
                for (int j = i + 1; j < childCount; j++)
                {
                    Transform b = parentTransform.GetChild(j);
                    if (b == null) continue;

                    float dist = Vector3.Distance(a.position, b.position);
                    if (dist <= placer.connectionDistance)
                    {
                        Handles.DrawLine(a.position, b.position);
                    }
                }
            }
        }

        // Draw and handle all node spheres
        for (int i = 0; i < childCount; i++)
        {
            Transform child = parentTransform.GetChild(i);
            if (child == null) continue;

            bool isSelected = placer.selectedIndices.Contains(i);
            Handles.color = isSelected ? Color.yellow : Color.cyan;

            float size = HandleUtility.GetHandleSize(child.position) * placer.handleSize;
            
            if (Handles.Button(child.position, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
            {
                if (e.shift)
                {
                    if (placer.selectedIndices.Contains(i))
                        placer.selectedIndices.Remove(i);
                    else
                        placer.selectedIndices.Add(i);
                }
                else
                {
                    placer.selectedIndices.Clear();
                    placer.selectedIndices.Add(i);
                }
                EditorUtility.SetDirty(placer);
                Repaint();
            }

            // Optional Index Label
            if (placer.showLabels)
            {
                Handles.Label(child.position + Vector3.up * (size * 1.2f), $"[{i}]", EditorStyles.whiteBoldLabel);
            }
        }

        // Group Translation Handle
        if (placer.selectedIndices.Count > 0)
        {
            Vector3 center = Vector3.zero;
            foreach (int i in placer.selectedIndices) center += parentTransform.GetChild(i).position;
            center /= placer.selectedIndices.Count;

            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 delta = newCenter - center;
                foreach (int i in placer.selectedIndices)
                {
                    Transform child = parentTransform.GetChild(i);
                    Undo.RecordObject(child, "Move Node Group");
                    child.position += delta;
                    EditorUtility.SetDirty(child);
                }
            }
        }

        // Shift + Click to spawn new node onto colliders
        if (e.type == EventType.MouseDown && e.shift && e.button == 0 && !e.control && !e.command)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                SpawnNode(placer, hit.point + hit.normal * 0.05f);
                e.Use();
                Repaint();
            }
        }
    }

    private Rect RectMinMax(Vector2 pointA, Vector2 pointB)
    {
        return new Rect(
            Mathf.Min(pointA.x, pointB.x),
            Mathf.Min(pointA.y, pointB.y),
            Mathf.Abs(pointA.x - pointB.x),
            Mathf.Abs(pointA.y - pointB.y)
        );
    }

    private void PerformBoxSelection(UltimateNodePlacer placer)
    {
        Transform parentTransform = placer.transform;
        Rect screenRect = RectMinMax(placer.boxStartMousePos, placer.boxCurrentMousePos);

        if (!Event.current.shift)
        {
            placer.selectedIndices.Clear();
        }

        for (int i = 0; i < parentTransform.childCount; i++)
        {
            Transform child = parentTransform.GetChild(i);
            Vector2 screenPoint = HandleUtility.WorldToGUIPoint(child.position);
            if (screenRect.Contains(screenPoint))
            {
                if (!placer.selectedIndices.Contains(i))
                {
                    placer.selectedIndices.Add(i);
                }
            }
        }
        EditorUtility.SetDirty(placer);
    }

    private void SpawnNode(UltimateNodePlacer placer, Vector3 worldPosition)
    {
        Undo.RecordObject(placer.transform, "Add Node");
        
        GameObject newChild;
        if (placer.nodePrefab != null)
        {
            newChild = (GameObject)PrefabUtility.InstantiatePrefab(placer.nodePrefab, placer.transform);
            newChild.transform.position = worldPosition;
        }
        else
        {
            newChild = new GameObject("Node_" + placer.transform.childCount);
            newChild.transform.SetParent(placer.transform, true);
            newChild.transform.position = worldPosition;
        }

        placer.selectedIndices.Clear();
        placer.selectedIndices.Add(placer.transform.childCount - 1);
        EditorUtility.SetDirty(placer.gameObject);
    }

    private void DuplicateSelectedNodes(UltimateNodePlacer placer)
    {
        Transform parentTransform = placer.transform;
        if (placer.selectedIndices.Count == 0) return;

        Undo.RegisterCompleteObjectUndo(parentTransform, "Duplicate Selected Nodes");
        List<int> newIndices = new List<int>();
        Vector3 offset = new Vector3(0.5f, 0f, 0.5f);

        foreach (int oldIndex in placer.selectedIndices)
        {
            if (oldIndex >= parentTransform.childCount) continue;
            Transform source = parentTransform.GetChild(oldIndex);

            GameObject duplicate = placer.nodePrefab != null && PrefabUtility.IsPartOfPrefabAsset(placer.nodePrefab)
                ? (GameObject)PrefabUtility.InstantiatePrefab(placer.nodePrefab, parentTransform)
                : Instantiate(source.gameObject, parentTransform);

            duplicate.name = source.name;
            duplicate.transform.position = source.position + offset;

            newIndices.Add(parentTransform.childCount - 1);
            EditorUtility.SetDirty(duplicate);
        }

        placer.selectedIndices = newIndices;
        EditorUtility.SetDirty(placer.gameObject);
    }

    private void DeleteSelectedNodes(UltimateNodePlacer placer)
    {
        Transform parentTransform = placer.transform;
        if (placer.selectedIndices.Count == 0) return;

        placer.selectedIndices.Sort((a, b) => b.CompareTo(a));
        Undo.RegisterCompleteObjectUndo(parentTransform, "Delete Nodes");

        foreach (int index in placer.selectedIndices)
        {
            if (index < parentTransform.childCount)
            {
                Undo.DestroyObjectImmediate(parentTransform.GetChild(index).gameObject);
            }
        }
        placer.selectedIndices.Clear();
        EditorUtility.SetDirty(placer.gameObject);
    }

    private void GenerateGrid(UltimateNodePlacer placer)
    {
        Transform t = placer.transform;
        Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "Generate Grid");

        while (t.childCount > 0)
        {
            Undo.DestroyObjectImmediate(t.GetChild(0).gameObject);
        }

        AppendGrid(placer);
    }

    private void AppendGrid(UltimateNodePlacer placer)
    {
        Transform t = placer.transform;
        Undo.RecordObject(t.gameObject, "Append Grid");

        Vector3 startPos = t.position - placer.generatorSize * 0.5f;
        Vector3 step = new Vector3(
            placer.generatorCount.x > 1 ? placer.generatorSize.x / (placer.generatorCount.x - 1) : 0f,
            placer.generatorCount.y > 1 ? placer.generatorSize.y / (placer.generatorCount.y - 1) : 0f,
            placer.generatorCount.z > 1 ? placer.generatorSize.z / (placer.generatorCount.z - 1) : 0f
        );

        for (int x = 0; x < placer.generatorCount.x; x++)
        {
            for (int y = 0; y < placer.generatorCount.y; y++)
            {
                for (int z = 0; z < placer.generatorCount.z; z++)
                {
                    Vector3 pos = startPos + new Vector3(x * step.x, y * step.y, z * step.z);
                    GameObject go = placer.nodePrefab != null 
                        ? (GameObject)PrefabUtility.InstantiatePrefab(placer.nodePrefab, t)
                        : new GameObject($"Node_{t.childCount}");
                    
                    if (placer.nodePrefab == null) go.transform.SetParent(t, true);
                    go.transform.position = pos;
                    Undo.RegisterCreatedObjectUndo(go, "Create Node");
                }
            }
        }
        placer.selectedIndices.Clear();
        EditorUtility.SetDirty(t.gameObject);
    }

    private void CenterNodes(UltimateNodePlacer placer)
    {
        Transform t = placer.transform;
        Undo.RegisterCompleteObjectUndo(t, "Center Nodes");
        foreach (Transform child in t)
        {
            child.position = t.position;
        }
    }

    private void JitterSelected(UltimateNodePlacer placer, float amount)
    {
        Transform t = placer.transform;
        foreach (int i in placer.selectedIndices)
        {
            Transform child = t.GetChild(i);
            Undo.RecordObject(child, "Jitter Node");
            child.position += Random.insideUnitSphere * amount;
        }
    }

    private void MirrorNodes(UltimateNodePlacer placer, Vector3 axis)
    {
        Transform t = placer.transform;
        Vector3 parentPos = t.position;
        foreach (int i in placer.selectedIndices)
        {
            Transform child = t.GetChild(i);
            Undo.RecordObject(child, "Mirror Node");
            Vector3 local = child.position - parentPos;
            local.Scale(Vector3.one - (axis * 2f)); // invert selected axis
            child.position = parentPos + local;
        }
    }

    private void BakePositionsToTexture(UltimateNodePlacer placer)
    {
        Transform t = placer.transform;
        int width = 256;
        int height = 256;
        int maxCapacity = width * height;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        int childCount = t.childCount;
        Color[] colors = new Color[maxCapacity];

        for (int i = 0; i < maxCapacity; i++)
        {
            if (i < childCount)
            {
                Vector3 pos = t.GetChild(i).localPosition;
                colors[i] = new Color(pos.x, pos.y, pos.z, 1.0f);
            }
            else
            {
                colors[i] = Color.clear;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        AssetDatabase.CreateAsset(tex, placer.textureSavePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green>Successfully baked {childCount} node positions to texture:</color> {placer.textureSavePath}");
    }
}
#endif
