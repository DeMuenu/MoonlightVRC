// Assets/Lighting/Scripts/LightUpdater.Editor.cs
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEngine;

public partial class LightUpdater
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

        LightdataStorage[] sceneLights = Object.FindObjectsOfType<LightdataStorage>();

        for (int i = 0; i < sceneLights.Length && count < max; i++)
        {
            LightdataStorage data = sceneLights[i];
            if (data == null || !data.gameObject.activeInHierarchy) continue;

            Transform t = data.transform;

            // w = cosHalfAngle (0 for omni)
            float cosHalf      = data.GetCosHalfAngle();

            Vector3 pos        = t.position;
            float range = 0;
            if (data.lightType == LightType.Sphere)
            {
                range = data.range * t.localScale.x;
            }
            else
            {
                range = Mathf.Cos(Mathf.Deg2Rad * ((data.spotAngleDeg * 0.5f) + Mathf.Max(data.range, 0)));
            }

            // rgb = color, a = intensity (packed to match runtime/shader)
            Vector4 col        = data.GetFinalColor();
            float intensity    = data.intensity * t.localScale.x;

            // 0=Omni, 1=Spot, 2=Directional (your custom enum)
            int typeId         = data.GetTypeId();

            float shIndex      = data.shadowMapIndex;

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
