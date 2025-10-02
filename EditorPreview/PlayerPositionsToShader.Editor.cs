// Assets/Lighting/Scripts/PlayerPositionsToShader.Editor.cs
#if UNITY_EDITOR
using UnityEngine;

public partial class PlayerPositionsToShader
{
    public void Editor_BuildPreview(
        out Vector4[] positions,
        out Vector4[] colors,
        out Vector4[] directions,
        out float[]   types,
        out float[]   shadowMapIndices,
        out int       count)
    {
        int max = Mathf.Max(1, maxLights);

        positions         = new Vector4[max];
        colors            = new Vector4[max];
        directions        = new Vector4[max];
        types             = new float[max];
        shadowMapIndices  = new float[max];
        count             = 0;

        if (otherLightSources == null) return;

        for (int i = 0; i < otherLightSources.Length && count < max; i++)
        {
            Transform t = otherLightSources[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;

            LightdataStorage data = t.GetComponent<LightdataStorage>();

            Vector3 pos        = t.position;
            float range        = (data != null) ? data.range * t.localScale.x : t.localScale.x;

            // rgb = color, a = intensity (packed to match runtime/shader)
            Vector4 col        = (data != null) ? data.GetFinalColor() : new Vector4(1f, 1f, 1f, 1f);
            float intensity    = (data != null) ? data.intensity * t.localScale.x : 1f;

            // w = cosHalfAngle (0 for omni)
            float cosHalf      = (data != null) ? data.GetCosHalfAngle() : 0f;

            // 0=Omni, 1=Spot, 2=Directional (your custom enum)
            int typeId         = (data != null) ? data.GetTypeId() : 0;

            float shIndex      = (data != null) ? data.shadowMapIndex : 0f;

            Quaternion rot     = t.rotation;
            Vector3 fwd        = rot * Vector3.down;

            positions[count]   = new Vector4(pos.x, pos.y, pos.z, range);
            colors[count]      = new Vector4(col.x, col.y, col.z, intensity);
            directions[count]  = new Vector4(fwd.x, fwd.y, fwd.z, cosHalf);
            types[count]       = (float)typeId;
            shadowMapIndices[count] = shIndex;

            count++;
        }
    }
}
#endif
