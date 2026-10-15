#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(OctreeSettings))]
public class Shadowbaker : Editor
{
    private readonly OctreeVisualizer octreeVisualizer = new OctreeVisualizer();
    [SerializeField] private int selectedTab = 0;
    [SerializeField] private bool showBakeAdvanced;
    [SerializeField] private bool showNodeAdvanced;
    [SerializeField] private bool showPreviewAdvanced;
    [SerializeField] private bool showShortcuts;
    private readonly string[] tabs = { "Bake", "Nodes", "Preview" };

    private void OnEnable()
    {
        OctreeSettings settings = (OctreeSettings)target;
        MigrateSettings(settings);
    }

    public override void OnInspectorGUI()
    {
        OctreeSettings settings = (OctreeSettings)target;
        serializedObject.Update();
        EditorGUILayout.LabelField("Shadow Baker", EditorStyles.boldLabel);
        DrawProperty("probeRootTransform", "Probe Root");
        EditorGUILayout.LabelField($"{settings.ProbeRoot.childCount} probes", EditorStyles.miniLabel);
        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        switch (selectedTab)
        {
            case 0: DrawBaking(settings); break;
            case 1: DrawNodes(settings); break;
            case 2: DrawPreview(settings); break;
        }
        serializedObject.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
    }

    private void DrawBaking(OctreeSettings settings)
    {
        DrawProperty("bounds", "Bake Bounds");
        DrawProperty("maxDepth", "Octree Detail");
        DrawProperty("colliderLayerMask", "Geometry Layers");
        EditorGUILayout.Space();
        if (GUILayout.Button("Bake Shadows", GUILayout.Height(32)))
        {
            serializedObject.ApplyModifiedProperties();
            new OctreeBaker(settings).BakeOctree();
            ProbeBaker.BakeProbePositions(settings);
            ProbeBaker.BakeClosestProbesTexture(settings);
            ProbeBaker.ApplyLightmapIndicesAutomatically();
        }
        EditorGUILayout.LabelField("Uses existing Unity lightmaps and world-space probe positions.", EditorStyles.wordWrappedMiniLabel);
        showBakeAdvanced = EditorGUILayout.Foldout(showBakeAdvanced, "Advanced", true);
        if (!showBakeAdvanced) return;
        DrawProperty("textureWidth", "Octree Texture Width");
        DrawProperty("textureHeight", "Octree Texture Height");
        DrawProperty("octreeSavePath", "Octree Output");
        DrawProperty("worldProbePositionSavePath", "Probe Positions Output");
        DrawProperty("probeIndexTextureArraySavePath", "Probe Indices Output");
        EditorGUILayout.Space();
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button("Bake Octree Only")) new OctreeBaker(settings).BakeOctree();
        if (GUILayout.Button("Bake World Probe Positions Only")) ProbeBaker.BakeProbePositions(settings);
        if (GUILayout.Button("Bake Closest Probe Indices Only")) ProbeBaker.BakeClosestProbesTexture(settings);
        if (GUILayout.Button("Apply Lightmap Indices")) ProbeBaker.ApplyLightmapIndicesAutomatically();
        DrawProperty("probePositionSavePath", "Local Positions Output");
        if (GUILayout.Button("Bake Local Probe Positions"))
        {
            serializedObject.ApplyModifiedProperties();
            new OctreeBaker(settings).BakeProbePositions();
        }
    }

    private void DrawNodes(OctreeSettings settings)
    {
        DrawProperty("editNodes", "Edit Probes in Scene");
        DrawProperty("generatorSize", "Grid Size");
        DrawProperty("generatorCount", "Grid Counts");
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button("Generate Probe Grid", GUILayout.Height(30)))
        {
            if (settings.ProbeRoot.childCount == 0 || EditorUtility.DisplayDialog("Generate Grid", "This will replace all existing child nodes. Are you sure?", "Yes", "Cancel"))
                GenerateGrid(settings);
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Probe")) SpawnNode(settings, settings.ProbeRoot.position);
        using (new EditorGUI.DisabledScope(settings.selectedIndices.Count == 0))
        {
            if (GUILayout.Button("Duplicate")) DuplicateSelectedNodes(settings);
        }
        EditorGUILayout.EndHorizontal();
        showNodeAdvanced = EditorGUILayout.Foldout(showNodeAdvanced, "Advanced", true);
        if (!showNodeAdvanced) return;
        DrawProperty("nodePrefab", "Probe Prefab");
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button("Append Grid")) AppendGrid(settings);
        DrawOperationsTab(settings);
        DrawDisplayTab(settings);
        showShortcuts = EditorGUILayout.Foldout(showShortcuts, "Scene Shortcuts", true);
        if (showShortcuts) DrawEditorTab(settings);
    }

    private void DrawPreview(OctreeSettings settings)
    {
        DrawProperty("octreePreview.enabled", "Show Octree");
        if (serializedObject.FindProperty("octreePreview.enabled").boolValue)
        {
            UseBakedTexture("octreePreview.bakedTexture", settings.octreeSavePath);
        }
        DrawProperty("probePreview.enabled", "Show Probe Shadows");
        if (serializedObject.FindProperty("probePreview.enabled").boolValue)
        {
            DrawProperty("probePreview.shadowCalculationTexture", "Shadow Texture");
            UseBakedTexture("probePreview.probePosTexture", settings.worldProbePositionSavePath);
            DrawProperty("probePreview.targetLightIndex", "Light Index");
        }
        showPreviewAdvanced = EditorGUILayout.Foldout(showPreviewAdvanced, "Advanced", true);
        if (!showPreviewAdvanced) return;
        DrawProperty("octreePreview.bakedTexture", "Octree Texture");
        DrawProperty("octreePreview.displayDepth", "Display Depth");
        DrawProperty("octreePreview.showOnlyOccupied", "Occupied Nodes Only");
        DrawProperty("probePreview.probePosTexture", "Probe Positions Texture");
        DrawProperty("probePreview.probeCount", "Probe Count");
        DrawProperty("probePreview.gizmoRadius", "Probe Radius");
    }

    private void UseBakedTexture(string propertyName, string path)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property.objectReferenceValue == null)
            property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private void DrawProperty(string name, string label)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
    }

    private static void MigrateSettings(OctreeSettings settings)
    {
        if (settings.settingsConsolidated) return;
        Undo.RecordObject(settings, "Consolidate Shadow Settings");
        ProbeBaker baker = settings.GetComponent<ProbeBaker>();
        if (baker == null) baker = settings.ProbeRoot.GetComponent<ProbeBaker>();
        if (baker != null && baker.settings != null)
        {
            if (settings.probeRootTransform == null) settings.probeRootTransform = baker.settings.probeRootTransform;
            settings.worldProbePositionSavePath = baker.settings.probePositionSavePath;
            settings.probeIndexTextureArraySavePath = baker.settings.probeIndexTextureArraySavePath;
            Undo.RecordObject(baker, "Consolidate Shadow Settings");
            baker.settingsOwner = settings;
            EditorUtility.SetDirty(baker);
            PrefabUtility.RecordPrefabInstancePropertyModifications(baker);
        }
        UltimateNodePlacer placer = settings.ProbeRoot.GetComponent<UltimateNodePlacer>();
        if (placer != null)
        {
            settings.nodePrefab = placer.nodePrefab;
            settings.generatorSize = placer.generatorSize;
            settings.generatorCount = placer.generatorCount;
            settings.handleSize = placer.handleSize;
            settings.showConnections = placer.showConnections;
            settings.connectionDistance = placer.connectionDistance;
            settings.showLabels = placer.showLabels;
            settings.textureSavePath = placer.textureSavePath;
            settings.selectedIndices = new List<int>(placer.selectedIndices);
        }
        ProbeDebugVisualizer debug = settings.GetComponent<ProbeDebugVisualizer>();
        if (debug == null) debug = settings.ProbeRoot.GetComponent<ProbeDebugVisualizer>();
        if (debug != null)
        {
            settings.probePreview = debug.GetLegacySettings();
            Undo.RecordObject(debug, "Consolidate Shadow Settings");
            debug.settingsOwner = settings;
            EditorUtility.SetDirty(debug);
            PrefabUtility.RecordPrefabInstancePropertyModifications(debug);
        }
        settings.settingsConsolidated = true;
        EditorUtility.SetDirty(settings);
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
    }

    private void DrawEditorTab(OctreeSettings placer)
    {
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

        if (placer.selectedIndices.Count > 0)
        {
            if (GUILayout.Button($"Clear Selection ({placer.selectedIndices.Count} selected)", GUILayout.Height(25)))
            {
                placer.selectedIndices.Clear();
                Repaint();
            }
        }
    }

    private void DrawOperationsTab(OctreeSettings placer)
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
        DrawProperty("textureSavePath", "Node Positions Output");
        if (GUILayout.Button("Bake Positions to 256x256 Texture", GUILayout.Height(35)))
        {
            serializedObject.ApplyModifiedProperties();
            BakePositionsToTexture(placer);
        }
    }

    private void DrawDisplayTab(OctreeSettings placer)
    {
        EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);
        DrawProperty("handleSize", "Handle Size");
        DrawProperty("showConnections", "Show Neighbor Lines");
        if (serializedObject.FindProperty("showConnections").boolValue)
        {
            DrawProperty("connectionDistance", "Connection Distance");
        }
        DrawProperty("showLabels", "Show Probe Indices");
    }

    private void OnSceneGUI()
    {
        OctreeSettings settings = (OctreeSettings)target;
        octreeVisualizer.DrawScene(settings);
        if (settings.editNodes) DrawNodeSceneGUI(settings);
    }

    private void DrawNodeSceneGUI(OctreeSettings placer)
    {
        Transform parentTransform = placer.ProbeRoot;
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

    private void PerformBoxSelection(OctreeSettings placer)
    {
        Transform parentTransform = placer.ProbeRoot;
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

    private void SpawnNode(OctreeSettings placer, Vector3 worldPosition)
    {
        Undo.RecordObject(placer.ProbeRoot, "Add Node");
        
        GameObject newChild;
        if (placer.nodePrefab != null)
        {
            newChild = (GameObject)PrefabUtility.InstantiatePrefab(placer.nodePrefab, placer.ProbeRoot);
            newChild.transform.position = worldPosition;
        }
        else
        {
            newChild = new GameObject("Node_" + placer.ProbeRoot.childCount);
            newChild.transform.SetParent(placer.ProbeRoot, true);
            newChild.transform.position = worldPosition;
        }

        placer.selectedIndices.Clear();
        placer.selectedIndices.Add(placer.ProbeRoot.childCount - 1);
        EditorUtility.SetDirty(placer.gameObject);
    }

    private void DuplicateSelectedNodes(OctreeSettings placer)
    {
        Transform parentTransform = placer.ProbeRoot;
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

    private void DeleteSelectedNodes(OctreeSettings placer)
    {
        Transform parentTransform = placer.ProbeRoot;
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

    private void GenerateGrid(OctreeSettings placer)
    {
        Transform t = placer.ProbeRoot;
        Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "Generate Grid");

        while (t.childCount > 0)
        {
            Undo.DestroyObjectImmediate(t.GetChild(0).gameObject);
        }

        AppendGrid(placer);
    }

    private void AppendGrid(OctreeSettings placer)
    {
        Transform t = placer.ProbeRoot;
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

    private void CenterNodes(OctreeSettings placer)
    {
        Transform t = placer.ProbeRoot;
        Undo.RegisterCompleteObjectUndo(t, "Center Nodes");
        foreach (Transform child in t)
        {
            child.position = t.position;
        }
    }

    private void JitterSelected(OctreeSettings placer, float amount)
    {
        Transform t = placer.ProbeRoot;
        foreach (int i in placer.selectedIndices)
        {
            Transform child = t.GetChild(i);
            Undo.RecordObject(child, "Jitter Node");
            child.position += Random.insideUnitSphere * amount;
        }
    }

    private void MirrorNodes(OctreeSettings placer, Vector3 axis)
    {
        Transform t = placer.ProbeRoot;
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

    private void BakePositionsToTexture(OctreeSettings placer)
    {
        Transform t = placer.ProbeRoot;
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
