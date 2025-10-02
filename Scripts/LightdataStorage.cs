using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;



public enum LightType { Sphere, Spot }

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LightdataStorage : UdonSharpBehaviour
{

    [Header("Type")]
    [Tooltip("Select the logical light type for this source.")]
    public LightType lightType = LightType.Sphere;


    [Header("Light Settings")]

    public float range = 5f;



    [ColorUsage(true, true)]                // (showAlpha: true, HDR: true)
    public Color color = Color.white;

    [Tooltip("Intensity multiplier applied to the color (kept separate so you can tweak brightness without changing hue).")]
    public float intensity = 1f;

    [Header("Spotlight Shape")]
    [Tooltip("0 = omni (no cone)")]
    public float spotAngleDeg = 0f;

    [Header("Shadow Settings")]
    [Tooltip("0 = no shadows, 1-4 = shadow map index")]
    public float shadowMapIndex = 0f; // 0 = no shadows, 1-4 = shadow map index

    // Convert to a Vector4 for your shader upload
    public Vector4 GetFinalColor()
    {
        return new Vector4(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
    }

    public float GetCosHalfAngle()
    {
        if (spotAngleDeg <= 0f) return 0f;
        return Mathf.Cos(Mathf.Deg2Rad * (spotAngleDeg * 0.5f));
    }
    
    public int GetTypeId() => (int)lightType; // Omni=0, Spot=1, Directional=2
}
