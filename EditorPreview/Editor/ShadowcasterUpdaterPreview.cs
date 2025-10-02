#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ShadowcasterUpdaterEditorLoop
{
    private const double Interval = 0.1;
    private const float  Eps = 1e-6f;

    private static double _lastUpdateTime;
    private static ShadowcasterUpdater[] _cached;
    private static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

    static ShadowcasterUpdaterEditorLoop()
    {
        _lastUpdateTime = EditorApplication.timeSinceStartup;

        EditorApplication.update += Update;
        EditorApplication.hierarchyChanged += RefreshCache;
        AssemblyReloadEvents.afterAssemblyReload += RefreshCache;

        RefreshCache();
    }

    private static void RefreshCache()
    {
        _cached = FindSceneUpdaters();
    }

    private static ShadowcasterUpdater[] FindSceneUpdaters()
    {
#if UNITY_2020_1_OR_NEWER
        return Object.FindObjectsOfType<ShadowcasterUpdater>(true);
#else
        // Unity 2019-compatible path: include inactive, filter out assets/prefabs not in a scene
        var all = Resources.FindObjectsOfTypeAll(typeof(ShadowcasterUpdater));
        var list = new List<ShadowcasterUpdater>(all.Length);
        foreach (var o in all)
        {
            var c = o as ShadowcasterUpdater;
            if (c == null) continue;
            if (EditorUtility.IsPersistent(c)) continue; // skip assets
            if (!c.gameObject.scene.IsValid()) continue;
            list.Add(c);
        }
        return list.ToArray();
#endif
    }

    private static void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling) return;

        var now = EditorApplication.timeSinceStartup;
        if (now - _lastUpdateTime < Interval) return;
        _lastUpdateTime = now;

        var arr = _cached;
        if (arr == null || arr.Length == 0) return;

        for (int i = 0; i < arr.Length; i++)
        {
            var u = arr[i];
            if (u == null) continue;
            TryUpdate(u);
        }

        // Make changes visible in Scene/Game view without wiggling the mouse
        SceneView.RepaintAll();
    }

    private static void TryUpdate(ShadowcasterUpdater u)
    {
        // If inspector values changed, make sure textures/colors/min brightness are pushed
        try
        {
            u.ApplyTextureData();
        }
        catch
        {
            // UdonSharp can be touchy during certain editor states. Ignore and continue.
        }

        var targets = u.rendererTargets;
        if (targets == null || targets.Length == 0) return;

        // Match the runtime script's plane definition (0.5 half-size before scaling)
        const float quadHalfWidth = 0.5f;
        const float quadHalfHeight = 0.5f;

        Transform t = u.transform;

        // World-space basis from transform
        Vector3 Udir = t.rotation * Vector3.right; // local +X
        Vector3 Vdir = t.rotation * Vector3.up;    // local +Y

        // Half extents after non-uniform scaling
        float halfW = Mathf.Max(quadHalfWidth  * t.lossyScale.x, Eps);
        float halfH = Mathf.Max(quadHalfHeight * t.lossyScale.y, Eps);

        // Reciprocal axes so dot(r, Uinv/Vinv) -> [-0.5, 0.5]
        Vector3 Uinv = Udir / (2.0f * halfW);
        Vector3 Vinv = Vdir / (2.0f * halfH);

        // Unit normal
        Vector3 N = Vector3.Normalize(Vector3.Cross(Udir, Vdir));

        int idx = Mathf.Max(0, u.shadowcasterIndex);
        string suf = "_" + idx.ToString();

        int idShadowTex     = Shader.PropertyToID("_Udon_shadowCasterTex"   + suf);
        int idShadowColor   = Shader.PropertyToID("_Udon_shadowCasterColor" + suf);
        int idOutsideColor  = Shader.PropertyToID("_Udon_OutSideColor"      + suf);
        int idMinBrightness = Shader.PropertyToID("_Udon_MinBrightnessShadow" + suf);

        int idPlaneOrigin   = Shader.PropertyToID("_Udon_Plane_Origin_"  + idx.ToString());
        int idPlaneUinv     = Shader.PropertyToID("_Udon_Plane_Uinv_"    + idx.ToString());
        int idPlaneVinv     = Shader.PropertyToID("_Udon_Plane_Vinv_"    + idx.ToString());
        int idPlaneNormal   = Shader.PropertyToID("_Udon_Plane_Normal_"  + idx.ToString());

        Vector4 origin = new Vector4(t.position.x, t.position.y, t.position.z, 0);
        Vector4 uinv4  = new Vector4(Uinv.x, Uinv.y, Uinv.z, 0);
        Vector4 vinv4  = new Vector4(Vinv.x, Vinv.y, Vinv.z, 0);
        Vector4 n4     = new Vector4(N.x, N.y, N.z, 0);

        for (int r = 0; r < targets.Length; r++)
        {
            var ren = targets[r];
            if (ren == null) continue;

            ren.GetPropertyBlock(_mpb);

            // Also mirror texture/color in case ApplyTextureData couldn't run
            if (u.ShadowcasterTexture != null) _mpb.SetTexture(idShadowTex, u.ShadowcasterTexture);
            _mpb.SetColor(idShadowColor, u.TextureColor);
            _mpb.SetColor(idOutsideColor, u.OutsideColor);
            _mpb.SetFloat(idMinBrightness, u.MinBrightness);

            // Plane data
            _mpb.SetVector(idPlaneOrigin, origin);
            _mpb.SetVector(idPlaneUinv,   uinv4);
            _mpb.SetVector(idPlaneVinv,   vinv4);
            _mpb.SetVector(idPlaneNormal, n4);

            ren.SetPropertyBlock(_mpb);
        }
    }
}
#endif
