// Assets/Editor/PlayerPositionsToShaderPreview.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public static class PlayerPositionsToShaderPreview
{
    const double kTickInterval = 0.1; // seconds
    static double _nextTick;
    static readonly Dictionary<PlayerPositionsToShader, Cache> _cache = new Dictionary<PlayerPositionsToShader, Cache>();

    struct Cache
    {
        public Vector4[] positions;
        public Vector4[] colors;
        public Vector4[] directions;
        public float[]   types;
        public float[]   shadowMapIndices;
        public int       size;
    }

    static PlayerPositionsToShaderPreview()
    {
        EditorApplication.update += Update;
        EditorApplication.hierarchyChanged += ForceTick;
        Undo.undoRedoPerformed += ForceTick;
        Selection.selectionChanged += ForceTick;
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

        var behaviours = FindAllInScene();
        foreach (var b in behaviours)
        {
            if (b == null || !b.isActiveAndEnabled) continue;
            if (EditorUtility.IsPersistent(b)) continue; // skip assets
            PushFromBehaviour(b);
        }

        SceneView.RepaintAll();
    }

    static PlayerPositionsToShader[] FindAllInScene()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<PlayerPositionsToShader>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#elif UNITY_2020_1_OR_NEWER
        return Object.FindObjectsOfType<PlayerPositionsToShader>(true);
#else
        return Resources.FindObjectsOfTypeAll<PlayerPositionsToShader>();
#endif
    }

    static void EnsureArrays(PlayerPositionsToShader src, int required)
    {
        if (!_cache.TryGetValue(src, out var c) ||
            c.positions == null || c.colors == null || c.directions == null || c.types == null || c.shadowMapIndices == null ||
            c.size != required)
        {
            c = new Cache
            {
                positions         = new Vector4[required],
                colors            = new Vector4[required],
                directions        = new Vector4[required],
                types             = new float[required],
                shadowMapIndices  = new float[required],
                size              = required
            };
            _cache[src] = c;
        }
    }

    static void PushFromBehaviour(PlayerPositionsToShader src)
    {
        int max = Mathf.Max(1, src.maxLights);
        EnsureArrays(src, max);

        var c = _cache[src];
        var positions         = c.positions;
        var colors            = c.colors;
        var directions        = c.directions;
        var types             = c.types;
        var shadowMapIndices  = c.shadowMapIndices;

        // Clear arrays to safe defaults
        for (int i = 0; i < max; i++)
        {
            positions[i]        = Vector4.zero;
            colors[i]           = Vector4.zero;
            directions[i]       = Vector4.zero;
            types[i]            = 0f;
            shadowMapIndices[i] = 0f;
        }

        // Use the Editor-side function defined on the partial class
        int count = 0;
        try
        {
            src.Editor_BuildPreview(out positions, out colors, out directions, out types, out shadowMapIndices, out count);

            // replace cache arrays if sizes changed
            if (positions.Length != c.size)
                EnsureArrays(src, positions.Length);

            _cache[src] = new Cache
            {
                positions         = positions,
                colors            = colors,
                directions        = directions,
                types             = types,
                shadowMapIndices  = shadowMapIndices,
                size              = positions.Length
            };
        }
        catch
        {
            // Fallback: nothing to push if the method signature changes unexpectedly
            count = 0;
        }

        // Mirror runtime: push as GLOBAL shader properties
        // Resolve property IDs only if names are provided
        if (!string.IsNullOrEmpty(src.positionsProperty))
        {
            int id = Shader.PropertyToID(src.positionsProperty);
            Shader.SetGlobalVectorArray(id, positions);
        }

        if (!string.IsNullOrEmpty(src.colorProperty))
        {
            int id = Shader.PropertyToID(src.colorProperty);
            Shader.SetGlobalVectorArray(id, colors);
        }

        if (!string.IsNullOrEmpty(src.directionsProperty))
        {
            int id = Shader.PropertyToID(src.directionsProperty);
            Shader.SetGlobalVectorArray(id, directions);
        }

        if (!string.IsNullOrEmpty(src.typeProperty))
        {
            int id = Shader.PropertyToID(src.typeProperty);
            Shader.SetGlobalFloatArray(id, types);
        }

        if (!string.IsNullOrEmpty(src.shadowMapIndexProperty))
        {
            int id = Shader.PropertyToID(src.shadowMapIndexProperty);
            Shader.SetGlobalFloatArray(id, shadowMapIndices);
        }

        if (!string.IsNullOrEmpty(src.countProperty))
        {
            int id = Shader.PropertyToID(src.countProperty);
            Shader.SetGlobalFloat(id, count);
        }
    }
}

[CustomEditor(typeof(PlayerPositionsToShader))]
public class PlayerPositionsToShaderInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(6);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.LabelField("Edit-Mode Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Updates ~10×/s using players and Other Light Sources.");
        }

        if (GUILayout.Button("Refresh Now"))
        {
            PlayerPositionsToShaderPreview.ForceTick();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
#endif
