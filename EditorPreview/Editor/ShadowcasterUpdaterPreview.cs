#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public static class ShadowcasterUpdaterPreview
{
    const double kTickInterval = 0.1; // seconds
    static double _nextTick;

    // One shared MPB to avoid allocations each frame
    static readonly MaterialPropertyBlock sMPB = new MaterialPropertyBlock();

    // Cache: component -> property ID, and renderer -> last applied matrix
    static readonly Dictionary<ShadowcasterUpdater, int> _propId = new Dictionary<ShadowcasterUpdater, int>();
    static readonly Dictionary<Renderer, Matrix4x4> _lastW2L = new Dictionary<Renderer, Matrix4x4>();
    static readonly List<Renderer> _toRemove = new List<Renderer>(32);

    static ShadowcasterUpdaterPreview()
    {
        EditorApplication.update += Update;
        EditorApplication.hierarchyChanged += ForceTick;
        Undo.undoRedoPerformed += ForceTick;
        Selection.selectionChanged += ForceTick;
        EditorApplication.playModeStateChanged += _ => ForceTick();
    }

    public static void ForceTick() => _nextTick = 0;

    static void Update()
    {
#if UNITY_2019_1_OR_NEWER
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
#else
        if (EditorApplication.isPlaying) return;
#endif
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextTick) return;
        _nextTick = now + kTickInterval;

        CleanupNullRenderers();

        var behaviours = FindAllInScene();
        foreach (var b in behaviours)
        {
            if (b == null || !b.isActiveAndEnabled) continue;
            if (EditorUtility.IsPersistent(b)) continue; // skip assets/prefabs
            ApplyToBehaviour(b);
        }

        SceneView.RepaintAll();
    }

    static ShadowcasterUpdater[] FindAllInScene()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<ShadowcasterUpdater>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#elif UNITY_2020_1_OR_NEWER
        return Object.FindObjectsOfType<ShadowcasterUpdater>(true);
#else
        return Resources.FindObjectsOfTypeAll<ShadowcasterUpdater>();
#endif
    }

    static void CleanupNullRenderers()
    {
        _toRemove.Clear();
        foreach (var kv in _lastW2L)
            if (kv.Key == null) _toRemove.Add(kv.Key);
        for (int i = 0; i < _toRemove.Count; i++)
            _lastW2L.Remove(_toRemove[i]);
    }

    static int GetPropertyId(ShadowcasterUpdater b)
    {
        string name = string.IsNullOrEmpty(b.propertyName) ? "_Udon_WorldToLocal" : b.propertyName;

        if (!_propId.TryGetValue(b, out int id))
        {
            id = Shader.PropertyToID(name);
            _propId[b] = id;
            return id;
        }

        // If user changed the property name in inspector, refresh the ID
        int newId = Shader.PropertyToID(name);
        if (newId != id)
        {
            _propId[b] = newId;
            id = newId;
        }
        return id;
    }

    static void ApplyToBehaviour(ShadowcasterUpdater b)
    {
        var renderers = b.rendererTargets;
        if (renderers == null || renderers.Length == 0) return;

        int id = GetPropertyId(b);
        Matrix4x4 w2l = b.transform.worldToLocalMatrix;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            if (_lastW2L.TryGetValue(r, out var last) && last == w2l)
                continue; // nothing changed for this renderer

            r.GetPropertyBlock(sMPB);
            sMPB.SetMatrix(id, w2l);
            r.SetPropertyBlock(sMPB);

            _lastW2L[r] = w2l;
        }
    }
}

[CustomEditor(typeof(ShadowcasterUpdater))]
public class ShadowcasterUpdaterInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(6);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.LabelField("Edit-Mode Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Keeps the matrix property updated in the Scene View.");
        }

        if (GUILayout.Button("Refresh Now"))
        {
            ShadowcasterUpdaterPreview.ForceTick();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
#endif
