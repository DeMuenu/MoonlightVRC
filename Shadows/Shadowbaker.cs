#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(OctreeSettings))]
public class Shadowbaker : Editor
{
    private readonly OctreeVisualizer octreeVisualizer = new OctreeVisualizer();
    private readonly ProbeSceneRenderer probeSceneRenderer = new ProbeSceneRenderer();
    private readonly ProbeSceneConnections probeConnections = new ProbeSceneConnections();
    private const int MaxProbeLabels = 64;
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
        Undo.undoRedoPerformed -= OnProbeUndoRedo;
        Undo.undoRedoPerformed += OnProbeUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnProbeUndoRedo;
        probeSceneRenderer.Dispose();
    }

    private void OnProbeUndoRedo()
    {
        probeSceneRenderer.Invalidate();
        probeConnections.Invalidate();
        Repaint();
        SceneView.RepaintAll();
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
                probeSceneRenderer.InvalidateSelection();
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
        Event e = Event.current;
        Camera camera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : null;
        if (camera == null) return;
        int control = GUIUtility.GetControlID("MoonlightProbeEditor".GetHashCode(), FocusType.Passive);
        if (e.type == EventType.Layout && !e.alt) HandleUtility.AddDefaultControl(control);
        placer.selectedIndices.RemoveAll(index => index < 0 || index >= placer.ProbeCount);

        if (e.type == EventType.KeyDown && placer.selectedIndices.Count > 0 &&
            (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
        {
            DeleteSelectedNodes(placer);
            e.Use();
            Repaint();
        }
        if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) &&
            e.commandName == "Duplicate" && placer.selectedIndices.Count > 0)
        {
            if (e.type == EventType.ExecuteCommand) DuplicateSelectedNodes(placer);
            e.Use();
            Repaint();
        }

        Vector3[] positions = probeSceneRenderer.GetPositions(placer);
        // One move handle for the whole selection, never one IMGUI control per probe.
        if (placer.selectedIndices.Count > 0 && !placer.isDraggingBox)
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
                placer.TranslateProbes(placer.selectedIndices, delta);
                SaveProbeChanges(placer);
                positions = probeSceneRenderer.GetPositions(placer);
            }
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt &&
            GUIUtility.hotControl == 0 && HandleUtility.nearestControl == control)
        {
            if (e.control || e.command)
            {
                placer.isDraggingBox = true;
                placer.boxStartMousePos = placer.boxCurrentMousePos = e.mousePosition;
                GUIUtility.hotControl = control;
            }
            else
            {
                int picked = PickProbe(positions, e.mousePosition, camera, MarkerRadius(placer) + 3f);
                if (picked >= 0)
                {
                    if (!e.shift) placer.selectedIndices.Clear();
                    if (e.shift && placer.selectedIndices.Contains(picked)) placer.selectedIndices.Remove(picked);
                    else placer.selectedIndices.Add(picked);
                    probeSceneRenderer.InvalidateSelection();
                    EditorUtility.SetDirty(placer);
                }
                else if (e.shift && Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out RaycastHit hit))
                    SpawnNode(placer, hit.point + hit.normal * 0.05f);
            }
            e.Use();
            SceneView.RepaintAll();
            Repaint();
        }
        if (placer.isDraggingBox && GUIUtility.hotControl == control)
        {
            if (e.type == EventType.Ignore || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
            {
                placer.isDraggingBox = false;
                GUIUtility.hotControl = 0;
                if (e.type != EventType.Ignore) e.Use();
                SceneView.RepaintAll();
            }
            else if (e.type == EventType.MouseDrag)
            {
                placer.boxCurrentMousePos = e.mousePosition;
                e.Use();
                SceneView.RepaintAll();
            }
            else if (e.type == EventType.MouseUp)
            {
                placer.boxCurrentMousePos = e.mousePosition;
                placer.isDraggingBox = false;
                GUIUtility.hotControl = 0;
                PerformBoxSelection(placer, positions, camera);
                e.Use();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        if (e.type != EventType.Repaint) return;
        probeSceneRenderer.Draw(placer, camera, MarkerRadius(placer));
        positions = probeSceneRenderer.GetPositions(placer);
        if (placer.showConnections)
        {
            probeConnections.Build(positions, placer.selectedIndices, placer.connectionDistance);
            if (probeConnections.Lines.Count > 0)
            {
                Handles.color = new Color(0f, 0.8f, 1f, 0.25f);
                Handles.DrawLines(probeConnections.Lines.ToArray());
            }
        }
        if (placer.showLabels)
        {
            int labelCount = Mathf.Min(MaxProbeLabels, placer.selectedIndices.Count > 0 ? placer.selectedIndices.Count : positions.Length);
            for (int i = 0; i < labelCount; i++)
            {
                int index = placer.selectedIndices.Count > 0 ? placer.selectedIndices[i] : i;
                if (IsInFrontOfCamera(positions[index], camera)) Handles.Label(positions[index], $"[{index}]", EditorStyles.whiteBoldLabel);
            }
        }
        if (placer.isDraggingBox)
        {
            Handles.BeginGUI();
            Rect rect = RectMinMax(placer.boxStartMousePos, placer.boxCurrentMousePos);
            EditorGUI.DrawRect(rect, new Color(0, 0.5f, 1f, 0.15f));
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0, 0.5f, 1f, 0.8f));
            Handles.EndGUI();
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

    private static float MarkerRadius(OctreeSettings settings) => Mathf.Clamp(settings.handleSize * 12f, 2f, 12f);

    private static bool IsInFrontOfCamera(Vector3 position, Camera camera)
    {
        return Vector3.Dot(position - camera.transform.position, camera.transform.forward) >= camera.nearClipPlane;
    }

    private static int PickProbe(Vector3[] positions, Vector2 mouse, Camera camera, float radius)
    {
        int nearest = -1;
        float bestDistance = radius * radius;
        float bestDepth = float.PositiveInfinity;
        Vector3 cameraPosition = camera.transform.position;
        Vector3 cameraForward = camera.transform.forward;
        float nearClip = camera.nearClipPlane;
        for (int i = 0; i < positions.Length; i++)
        {
            float depth = Vector3.Dot(positions[i] - cameraPosition, cameraForward);
            if (depth < nearClip) continue;
            float distance = (HandleUtility.WorldToGUIPoint(positions[i]) - mouse).sqrMagnitude;
            if (distance < bestDistance || (distance == bestDistance && depth < bestDepth))
            {
                nearest = i;
                bestDistance = distance;
                bestDepth = depth;
            }
        }
        return nearest;
    }

    private void PerformBoxSelection(OctreeSettings placer, Vector3[] positions, Camera camera)
    {
        Rect screenRect = RectMinMax(placer.boxStartMousePos, placer.boxCurrentMousePos);
        var selected = Event.current.shift ? new HashSet<int>(placer.selectedIndices) : new HashSet<int>();
        Vector3 cameraPosition = camera.transform.position;
        Vector3 cameraForward = camera.transform.forward;
        float nearClip = camera.nearClipPlane;
        for (int i = 0; i < positions.Length; i++)
        {
            if (Vector3.Dot(positions[i] - cameraPosition, cameraForward) < nearClip) continue;
            if (screenRect.Contains(HandleUtility.WorldToGUIPoint(positions[i]))) selected.Add(i);
        }
        placer.selectedIndices = new List<int>(selected);
        probeSceneRenderer.InvalidateSelection();
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
        placer.RemoveProbes(placer.selectedIndices);
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

    private sealed class ProbeSceneRenderer
    {
        private Vector3[] positions;
        private Matrix4x4 probeToWorld;
        private int revision;
        private bool invalid = true;
        private bool meshDirty = true;
        private bool selectionDirty = true;
        private int selectedCount = -1;
        private Mesh mesh;
        private Material material;
        private Color32[] colors;
        private Vector3[] vertices;

        internal void Invalidate() => invalid = true;
        internal void InvalidateSelection() => selectionDirty = true;

        internal Vector3[] GetPositions(OctreeSettings settings)
        {
            Matrix4x4 matrix = settings.ProbeRoot.localToWorldMatrix;
            if (invalid || positions == null || positions.Length != settings.ProbeCount ||
                revision != settings.ProbeRevision || !probeToWorld.Equals(matrix))
            {
                positions = settings.GetProbeWorldPositions();
                probeToWorld = matrix;
                revision = settings.ProbeRevision;
                invalid = false;
                meshDirty = true;
                selectionDirty = true;
            }
            return positions;
        }

        internal void Draw(OctreeSettings settings, Camera camera, float radius)
        {
            Vector3[] points = GetPositions(settings);
            if (points.Length == 0) return;
            if (material == null)
            {
                Shader shader = Shader.Find("Hidden/MoonlightVRC/ProbeSceneMarkers");
                if (shader == null) return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (mesh == null)
            {
                mesh = new Mesh { name = "Probe Editor Markers", hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                meshDirty = true;
            }
            if (meshDirty)
            {
                // Four vertices per billboard; 32-bit indices also handle the full 65,536-probe capacity.
                bool topologyChanged = vertices == null || vertices.Length != points.Length * 4;
                if (topologyChanged) vertices = new Vector3[points.Length * 4];
                for (int i = 0; i < points.Length; i++)
                {
                    int v = i * 4;
                    vertices[v] = vertices[v + 1] = vertices[v + 2] = vertices[v + 3] = points[i];
                }
                if (topologyChanged)
                {
                    var uv = new Vector2[vertices.Length];
                    var indices = new int[points.Length * 6];
                    colors = new Color32[vertices.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        int v = i * 4;
                        uv[v] = new Vector2(-1f, -1f);
                        uv[v + 1] = new Vector2(-1f, 1f);
                        uv[v + 2] = new Vector2(1f, 1f);
                        uv[v + 3] = new Vector2(1f, -1f);
                        int t = i * 6;
                        indices[t] = v;
                        indices[t + 1] = v + 1;
                        indices[t + 2] = v + 2;
                        indices[t + 3] = v;
                        indices[t + 4] = v + 2;
                        indices[t + 5] = v + 3;
                    }
                    mesh.Clear();
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = vertices;
                    mesh.uv = uv;
                    mesh.triangles = indices;
                }
                else mesh.vertices = vertices;
                meshDirty = false;
                selectionDirty = true;
            }
            if (selectionDirty || selectedCount != settings.selectedIndices.Count)
            {
                var selected = new HashSet<int>(settings.selectedIndices);
                Color32 normal = Color.cyan;
                Color32 highlight = Color.yellow;
                for (int i = 0; i < points.Length; i++)
                {
                    int v = i * 4;
                    Color32 color = selected.Contains(i) ? highlight : normal;
                    colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
                }
                mesh.colors32 = colors;
                selectedCount = settings.selectedIndices.Count;
                selectionDirty = false;
            }
            material.SetFloat("_MarkerRadius", radius * EditorGUIUtility.pixelsPerPoint);
            material.SetVector("_ViewportSize", new Vector4(camera.pixelWidth, camera.pixelHeight, 0f, 0f));
            if (material.SetPass(0)) Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
        }

        internal void Dispose()
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
            if (material != null) Object.DestroyImmediate(material);
            mesh = null;
            material = null;
            vertices = null;
            colors = null;
            invalid = true;
        }
    }
}
#endif
