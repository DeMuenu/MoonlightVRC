// Assets/Editor/LightUpdaterPreview.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public static class LightUpdaterPreview
{
    const double kTickInterval = 0.1; // seconds
    const string kCameraEnabledKey = "MoonlightVRC.SceneCameraLight.Enabled";
    const string kCameraColorKey = "MoonlightVRC.SceneCameraLight.Color";
    const string kCameraIntensityKey = "MoonlightVRC.SceneCameraLight.Intensity";
    const string kCameraRangeKey = "MoonlightVRC.SceneCameraLight.Range";

    static double _nextTick;
    static readonly Dictionary<LightUpdater, Cache> _cache = new Dictionary<LightUpdater, Cache>();

    struct Cache
    {
        public Vector4[] positions;
        public Vector4[] colors;
        public Vector4[] directions;
        public float[]   types;
        public float[]   shadowMapIndices;
        public int       size;
    }

    public static bool cameraLightEnabled
    {
        get => EditorPrefs.GetBool(kCameraEnabledKey, false);
        set => EditorPrefs.SetBool(kCameraEnabledKey, value);
    }

    public static Color cameraLightColor
    {
        get
        {
            string value = EditorPrefs.GetString(kCameraColorKey, "1,1,1,1");
            string[] comps = value.Split(',');
            float r = comps.Length > 0 ? float.TryParse(comps[0], out var rr) ? rr : 1f : 1f;
            float g = comps.Length > 1 ? float.TryParse(comps[1], out var gg) ? gg : 1f : 1f;
            float b = comps.Length > 2 ? float.TryParse(comps[2], out var bb) ? bb : 1f : 1f;
            float a = comps.Length > 3 ? float.TryParse(comps[3], out var aa) ? aa : 1f : 1f;
            return new Color(r, g, b, a);
        }
        set => EditorPrefs.SetString(kCameraColorKey, string.Format("{0},{1},{2},{3}", value.r, value.g, value.b, value.a));
    }

    public static float cameraLightIntensity
    {
        get => EditorPrefs.GetFloat(kCameraIntensityKey, 2f);
        set => EditorPrefs.SetFloat(kCameraIntensityKey, Mathf.Max(0f, value));
    }

    public static float cameraLightRange
    {
        get => EditorPrefs.GetFloat(kCameraRangeKey, 5f);
        set => EditorPrefs.SetFloat(kCameraRangeKey, Mathf.Max(0.01f, value));
    }

    static LightUpdaterPreview()
    {
        EditorApplication.update += Update;
        EditorApplication.hierarchyChanged += ForceTick;
        Undo.undoRedoPerformed += ForceTick;
        Selection.selectionChanged += ForceTick;
    }

    public static void ForceTick() => _nextTick = 0;

    [MenuItem("MoonlightVRC/Scene Camera Light/Toggle %l", priority = 100)]
    public static void ToggleSceneCameraLight()
    {
        cameraLightEnabled = !cameraLightEnabled;
        ForceTick();
        SceneView.RepaintAll();
    }

    [MenuItem("MoonlightVRC/Scene Camera Light/Toggle %l", validate = true)]
    public static bool ToggleSceneCameraLightValidate()
    {
        Menu.SetChecked("MoonlightVRC/Scene Camera Light/Toggle %l", cameraLightEnabled);
        return true;
    }

    [MenuItem("MoonlightVRC/Scene Camera Light/Open Settings", priority = 101)]
    public static void OpenSceneCameraLightSettings()
    {
        SceneCameraLightSettingsWindow.ShowWindow();
    }

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

    static LightUpdater[] FindAllInScene()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<LightUpdater>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#elif UNITY_2020_1_OR_NEWER
        return Object.FindObjectsOfType<LightUpdater>(true);
#else
        return Resources.FindObjectsOfTypeAll<LightUpdater>();
#endif
    }

    static void EnsureArrays(LightUpdater src, int required)
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

    static void AppendCameraLight(ref Vector4[] positions, ref Vector4[] colors, ref Vector4[] directions, ref float[] types, ref float[] shadowMapIndices, ref int count, int max)
    {
        if (!cameraLightEnabled || count >= max) return;

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null) return;

        Transform cameraTransform = view.camera.transform;
        Vector3 cameraPosition = cameraTransform.position;
        Vector3 cameraDirection = cameraTransform.forward;
        Color lightColor = cameraLightColor;

        positions[count] = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, cameraLightRange);
        colors[count] = new Vector4(lightColor.r, lightColor.g, lightColor.b, cameraLightIntensity);
        directions[count] = new Vector4(cameraDirection.x, cameraDirection.y, cameraDirection.z, 0f);
        types[count] = 0f;
        shadowMapIndices[count] = 0f;
        count++;
    }

    static void PushFromBehaviour(LightUpdater src)
    {
        int max = Mathf.Max(1, LightUpdater.maxLights);
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

        AppendCameraLight(ref positions, ref colors, ref directions, ref types, ref shadowMapIndices, ref count, max);

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

public class SceneCameraLightSettingsWindow : EditorWindow
{
    public static void ShowWindow()
    {
        var window = GetWindow<SceneCameraLightSettingsWindow>(false, "MoonlightVRC Camera Light");
        window.minSize = new Vector2(260f, 220f);
        window.ShowUtility();
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        bool enabled = LightUpdaterPreview.cameraLightEnabled;
        Color color = LightUpdaterPreview.cameraLightColor;
        float intensity = LightUpdaterPreview.cameraLightIntensity;
        float range = LightUpdaterPreview.cameraLightRange;

        enabled = EditorGUILayout.Toggle("Enabled", enabled);
        color = EditorGUILayout.ColorField("Color", color);
        intensity = EditorGUILayout.Slider("Intensity", intensity, 0f, 20f);
        range = EditorGUILayout.Slider("Range", range, 0.1f, 50f);

        if (EditorGUI.EndChangeCheck())
        {
            LightUpdaterPreview.cameraLightEnabled = enabled;
            LightUpdaterPreview.cameraLightColor = color;
            LightUpdaterPreview.cameraLightIntensity = intensity;
            LightUpdaterPreview.cameraLightRange = range;
            LightUpdaterPreview.ForceTick();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(80f)))
            {
                Close();
            }
        }
    }
}

[CustomEditor(typeof(LightUpdater))]
public class LightUpdaterInspector : Editor
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

        GUILayout.Space(6);
        bool sceneCameraLightEnabled = LightUpdaterPreview.cameraLightEnabled;
        bool toggled = EditorGUILayout.Toggle("Scene Camera Light", sceneCameraLightEnabled);
        if (toggled != sceneCameraLightEnabled)
        {
            LightUpdaterPreview.cameraLightEnabled = toggled;
            LightUpdaterPreview.ForceTick();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Refresh Now"))
        {
            LightUpdaterPreview.ForceTick();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
#endif
