using UnityEngine;

[ExecuteAlways]
public class ProbeDebugVisualizer : MonoBehaviour
{
    [Header("Textures & Data")]
    [Tooltip("The render texture output from your shadow calculator shader.")]
    public RenderTexture shadowCalculationTexture;
    
    [Tooltip("The 2D texture storing probe world positions (must have Read/Write enabled in import settings).")]
    public Texture2D probePosTexture;

    [Header("Debug Settings")]
    public int probeCount = 200;
    public int targetLightIndex = 0;
    public float gizmoRadius = 0.25f;
    [Tooltip("Update gizmos continuously in the Scene view.")]
    public bool liveUpdate = true;

    private Texture2D cachedShadowTex;

    private void OnDrawGizmos()
    {
        if (probePosTexture == null || shadowCalculationTexture == null)
            return;

        UpdateCachedShadowTexture();

        if (cachedShadowTex == null)
            return;

        int shadowTexWidth = cachedShadowTex.width;

        for (int i = 0; i < probeCount; i++)
        {
            Vector3 probePos = GetProbePositionFromTexture(i);

            uint sampleIndex = (uint)targetLightIndex * (uint)probeCount + (uint)i;
            uint pixelLinearIndex = sampleIndex / 4u;
            uint channelIndex = sampleIndex % 4u; // 0=R, 1=G, 2=B, 3=A

            uint px = pixelLinearIndex % (uint)shadowTexWidth;
            uint py = pixelLinearIndex / (uint)shadowTexWidth;

            if (px >= (uint)shadowTexWidth || py >= (uint)cachedShadowTex.height)
                continue;

            Color pixelCol = cachedShadowTex.GetPixel((int)px, (int)py);
            float shadowVal = channelIndex == 0 ? pixelCol.r :
                              channelIndex == 1 ? pixelCol.g :
                              channelIndex == 2 ? pixelCol.b : pixelCol.a;

            // Corrected: 1.0 means interrupted/occluded -> Red. 0.0 means clear -> Green.
            bool isOccluded = shadowVal > 0.5f;
            Gizmos.color = isOccluded ? Color.red : Color.green;
            Gizmos.DrawSphere(probePos, gizmoRadius);
        }
    }

    private Vector3 GetProbePositionFromTexture(int index)
    {
        int posTexWidth = probePosTexture.width;
        if (posTexWidth <= 0) posTexWidth = 256;

        int x = index % posTexWidth;
        int y = index / posTexWidth;

        Color col = probePosTexture.GetPixel(x, y);
        return new Vector3(col.r, col.g, col.b);
    }

    private void UpdatedCachedShadowTexture() { } // Legacy placeholder

    private void UpdateCachedShadowTexture()
    {
        int width = shadowCalculationTexture.width;
        int height = shadowCalculationTexture.height;

        if (cachedShadowTex == null || cachedShadowTex.width != width || cachedShadowTex.height != height)
        {
            cachedShadowTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cachedShadowTex.filterMode = FilterMode.Point;
        }

        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = shadowCalculationTexture;
        
        cachedShadowTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        cachedShadowTex.Apply();

        RenderTexture.active = currentRT;
    }
}