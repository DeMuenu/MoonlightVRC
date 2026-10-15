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
        DrawProperty("probeRootTransform", "Probe Space");
        EditorGUILayout.LabelField($"{settings.ProbeCount} probes (position data)", EditorStyles.miniLabel);
        if (settings.ProbeCount == 0 && settings.ProbeRoot.childCount > 0)
        {
            if (GUILayout.Button("Convert Child Probes to Position Data"))
            {
                serializedObject.ApplyModifiedProperties();
                ConvertChildProbes(settings);
                serializedObject.Update();
            }
        }
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
        using (new EditorGUI.DisabledScope(settings.ProbeCount == 0))
        {
            if (GUILayout.Button("Bake Shadows", GUILayout.Height(32)))
            {
                serializedObject.ApplyModifiedProperties();
                new OctreeBaker(settings).BakeOctree();
                ProbeBaker.BakeProbePositions(settings);
                ProbeBaker.BakeClosestProbesTexture(settings);
                ProbeBaker.ApplyLightmapIndicesAutomatically();
            }
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
            if (settings.ProbeCount == 0 || EditorUtility.DisplayDialog("Generate Grid", "This will replace all stored probe positions. Are you sure?", "Yes", "Cancel"))
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
        DrawProperty("probePreview.enabled", "Show Probes");
        if (serializedObject.FindProperty("probePreview.enabled").boolValue)
        {
            DrawProperty("probePreview.shadowCalculationTexture", "Shadow Texture (Optional)");
            if (serializedObject.FindProperty("probePreview.shadowCalculationTexture").objectReferenceValue != null)
                DrawProperty("probePreview.targetLightIndex", "Light Index");
        }
        showPreviewAdvanced = EditorGUILayout.Foldout(showPreviewAdvanced, "Advanced", true);
        if (!showPreviewAdvanced) return;
        DrawProperty("octreePreview.bakedTexture", "Octree Texture");
        DrawProperty("octreePreview.displayDepth", "Display Depth");
        DrawProperty("octreePreview.showOnlyOccupied", "Occupied Nodes Only");
        DrawProperty("probePreview.gizmoRadius", "Probe Radius");
        if (serializedObject.FindProperty("probePreview.shadowCalculationTexture").objectReferenceValue != null)
            DrawProperty("probePreview.liveUpdate", "Live Shadow Preview");
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
        int childCount = placer.ProbeCount;
        Vector3[] positions = placer.GetProbeWorldPositions();

        placer.selectedIndices.RemoveAll(index => index < 0 || index >= childCount);

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

        if (childCount != placer.ProbeCount)
        {
            childCount = placer.ProbeCount;
            positions = placer.GetProbeWorldPositions();
        }
        var selected = new HashSet<int>(placer.selectedIndices);

        // Draw Connection Lines between neighbor nodes
        if (placer.showConnections && childCount > 1 && e.type == EventType.Repaint)
        {
            DrawProbeConnections(positions, placer.connectionDistance);
        }

        // Draw and handle all node spheres
        for (int i = 0; i < childCount; i++)
        {
            Vector3 position = positions[i];

            bool isSelected = selected.Contains(i);
            Handles.color = isSelected ? Color.yellow : Color.cyan;

            float size = HandleUtility.GetHandleSize(position) * placer.handleSize;
            
            if (Handles.Button(position, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
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
                Handles.Label(position + Vector3.up * (size * 1.2f), $"[{i}]", EditorStyles.whiteBoldLabel);
            }
        }

        // Group Translation Handle
        if (placer.selectedIndices.Count > 0)
        {
            Vector3 center = Vector3.zero;
            foreach (int i in placer.selectedIndices) center += positions[i];
            center /= placer.selectedIndices.Count;

            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 delta = newCenter - center;
                Undo.RecordObject(placer, "Move Probe Group");
                foreach (int i in placer.selectedIndices)
                {
                    placer.SetProbeWorldPosition(i, positions[i] + delta);
                }
                SaveProbeChanges(placer);
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

    private static void DrawProbeConnections(Vector3[] positions, float distance)
    {
        if (distance <= 0f) return;
        Handles.color = new Color(0f, 0.8f, 1f, 0.25f);
        float squaredDistance = distance * distance;
        var cells = new Dictionary<Vector3Int, List<Vector3>>();
        foreach (Vector3 position in positions)
        {
            Vector3Int cell = Vector3Int.FloorToInt(position / distance);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (!cells.TryGetValue(cell + new Vector3Int(x, y, z), out List<Vector3> neighbors)) continue;
                foreach (Vector3 neighbor in neighbors)
                    if ((position - neighbor).sqrMagnitude <= squaredDistance) Handles.DrawLine(position, neighbor);
            }
            if (!cells.TryGetValue(cell, out List<Vector3> bucket))
            {
                bucket = new List<Vector3>();
                cells.Add(cell, bucket);
            }
            bucket.Add(position);
        }
    }

    private void PerformBoxSelection(OctreeSettings placer)
    {
        Rect screenRect = RectMinMax(placer.boxStartMousePos, placer.boxCurrentMousePos);

        if (!Event.current.shift)
        {
            placer.selectedIndices.Clear();
        }

        for (int i = 0; i < placer.ProbeCount; i++)
        {
            Vector2 screenPoint = HandleUtility.WorldToGUIPoint(placer.GetProbeWorldPosition(i));
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
        if (placer.ProbeCount >= OctreeSettings.MaxProbeCount) return;
        Undo.RecordObject(placer, "Add Probe");
        placer.AddProbeWorldPosition(worldPosition);
        placer.selectedIndices.Clear();
        placer.selectedIndices.Add(placer.ProbeCount - 1);
        SaveProbeChanges(placer);
    }

    private void DuplicateSelectedNodes(OctreeSettings placer)
    {
        placer.selectedIndices.RemoveAll(i => i < 0 || i >= placer.ProbeCount);
        if (placer.selectedIndices.Count == 0) return;
        if (placer.ProbeCount + placer.selectedIndices.Count > OctreeSettings.MaxProbeCount) return;

        Undo.RecordObject(placer, "Duplicate Probes");
        List<int> newIndices = new List<int>();
        Vector3 offset = new Vector3(0.5f, 0f, 0.5f);

        foreach (int oldIndex in placer.selectedIndices)
        {
            placer.AddProbeWorldPosition(placer.GetProbeWorldPosition(oldIndex) + offset);
            newIndices.Add(placer.ProbeCount - 1);
        }

        placer.selectedIndices = newIndices;
        SaveProbeChanges(placer);
    }

    private void DeleteSelectedNodes(OctreeSettings placer)
    {
        if (placer.selectedIndices.Count == 0) return;

        Undo.RecordObject(placer, "Delete Probes");
        placer.selectedIndices.Sort((a, b) => b.CompareTo(a));

        foreach (int index in placer.selectedIndices)
        {
            if (index >= 0 && index < placer.ProbeCount)
            {
                placer.RemoveProbeAt(index);
            }
        }
        placer.selectedIndices.Clear();
        SaveProbeChanges(placer);
    }

    private void GenerateGrid(OctreeSettings placer)
    {
        if (!CanGenerateGrid(placer, false)) return;
        Undo.RecordObject(placer, "Generate Probe Grid");
        placer.ClearProbes();
        AppendGridPositions(placer);
    }

    private void AppendGrid(OctreeSettings placer)
    {
        if (!CanGenerateGrid(placer, true)) return;
        Undo.RecordObject(placer, "Append Probe Grid");
        AppendGridPositions(placer);
    }

    private void AppendGridPositions(OctreeSettings placer)
    {
        Transform t = placer.ProbeRoot;

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
                    placer.AddProbeWorldPosition(pos);
                }
            }
        }
        placer.selectedIndices.Clear();
        SaveProbeChanges(placer);
    }

    private void CenterNodes(OctreeSettings placer)
    {
        Undo.RecordObject(placer, "Center Probes");
        for (int i = 0; i < placer.ProbeCount; i++)
        {
            placer.SetProbeWorldPosition(i, placer.ProbeRoot.position);
        }
        SaveProbeChanges(placer);
    }

    private void JitterSelected(OctreeSettings placer, float amount)
    {
        Undo.RecordObject(placer, "Jitter Probes");
        foreach (int i in placer.selectedIndices)
        {
            if (i < 0 || i >= placer.ProbeCount) continue;
            placer.SetProbeWorldPosition(i, placer.GetProbeWorldPosition(i) + Random.insideUnitSphere * amount);
        }
        SaveProbeChanges(placer);
    }

    private void MirrorNodes(OctreeSettings placer, Vector3 axis)
    {
        Undo.RecordObject(placer, "Mirror Probes");
        Vector3 parentPos = placer.ProbeRoot.position;
        foreach (int i in placer.selectedIndices)
        {
            if (i < 0 || i >= placer.ProbeCount) continue;
            Vector3 local = placer.GetProbeWorldPosition(i) - parentPos;
            local.Scale(Vector3.one - (axis * 2f)); // invert selected axis
            placer.SetProbeWorldPosition(i, parentPos + local);
        }
        SaveProbeChanges(placer);
    }

    private void BakePositionsToTexture(OctreeSettings placer)
    {
        ProbeBaker.BakePositionTexture(placer.ProbePositions, placer.textureSavePath);
    }

    private static bool CanGenerateGrid(OctreeSettings settings, bool append)
    {
        Vector3Int count = settings.generatorCount;
        if (count.x < 1 || count.y < 1 || count.z < 1) return false;
        double total = (double)count.x * count.y * count.z + (append ? settings.ProbeCount : 0);
        if (total <= OctreeSettings.MaxProbeCount) return true;
        Debug.LogError($"The grid exceeds the {OctreeSettings.MaxProbeCount} probe texture capacity.");
        return false;
    }

    private static void SaveProbeChanges(OctreeSettings settings)
    {
        EditorUtility.SetDirty(settings);
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        SceneView.RepaintAll();
    }

    private static void ConvertChildProbes(OctreeSettings settings)
    {
        Transform root = settings.ProbeRoot;
        if (settings.ProbeCount + root.childCount > OctreeSettings.MaxProbeCount) return;
        if (settings.transform != root && settings.transform.IsChildOf(root))
        {
            Debug.LogError("Choose a probe space whose children do not contain the OctreeSettings component before converting.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Convert Child Probes", "Store the direct children's positions as probe data and remove their GameObjects (including any attached components). This can be undone.", "Convert", "Cancel")) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Convert Child Probes");
        Undo.RegisterCompleteObjectUndo(settings, "Convert Child Probes");
        var children = new GameObject[root.childCount];
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = root.GetChild(i);
            settings.AddProbeWorldPosition(child.position);
            children[i] = child.gameObject;
        }
        foreach (GameObject child in children) Undo.DestroyObjectImmediate(child);
        settings.selectedIndices.Clear();
        SaveProbeChanges(settings);
        Undo.CollapseUndoOperations(group);
    }
}
#endif
