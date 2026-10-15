#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Collections;

[System.Serializable]
public class OctreeVisualizer
{
    public void DrawScene(OctreeSettings settings)
    {
        if (settings == null || !settings.octreePreview.enabled) return;
        Texture2D bakedTexture = settings.octreePreview.bakedTexture;
        if (bakedTexture == null) return;

        if (bakedTexture.format != TextureFormat.RGBA64)
        {
            return;
        }

        try
        {
            NativeArray<ushort> pixelData = bakedTexture.GetPixelData<ushort>(0);
            if (!pixelData.IsCreated || pixelData.Length == 0) return;

            DrawOctreeRecursive(0, settings.bounds, 0, settings.octreePreview.displayDepth, pixelData, settings.octreePreview.showOnlyOccupied);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Octree Visualizer Error: {e.Message}");
        }
    }

    private void DrawOctreeRecursive(int nodeIndex, Bounds bounds, int currentDepth, int maxDepth, NativeArray<ushort> pixelData, bool onlyOccupied)
    {
        int baseIndex = nodeIndex * 12;
        if (baseIndex + 11 >= pixelData.Length) return;

        bool isOccupied = pixelData[baseIndex + 0] > 0;
        bool isLeaf = pixelData[baseIndex + 1] > 0;

        if (!onlyOccupied || isOccupied)
        {
            if (currentDepth <= maxDepth)
            {
                Handles.color = isLeaf ? Color.green : Color.cyan;
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
        }

        if (!isLeaf && currentDepth < maxDepth)
        {
            Vector3 size = bounds.size;
            Vector3 childSize = size * 0.5f;
            Vector3 childExtents = childSize * 0.5f;
            Vector3 min = bounds.min;

            for (int i = 0; i < 8; i++)
            {
                int childIndex = pixelData[baseIndex + 4 + i];
                if (childIndex > 0)
                {
                    Vector3 childCenter = min + new Vector3(
                        (i & 1) == 0 ? childExtents.x : childExtents.x + childSize.x,
                        (i & 2) == 0 ? childExtents.y : childExtents.y + childSize.y,
                        (i & 4) == 0 ? childExtents.z : childExtents.z + childSize.z
                    );

                    Bounds childBounds = new Bounds(childCenter, childSize);
                    DrawOctreeRecursive(childIndex, childBounds, currentDepth + 1, maxDepth, pixelData, onlyOccupied);
                }
            }
        }
    }
}
#endif
