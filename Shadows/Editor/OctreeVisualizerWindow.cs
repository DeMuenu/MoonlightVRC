using UnityEngine;
using UnityEditor;
using Unity.Collections;

public class OctreeVisualizerWindow : EditorWindow
{
    private OctreeSettings settings;
    private Texture2D bakedTexture;
    private int displayDepth = 4;
    private bool showOnlyOccupied = true;
    private bool drawInScene = true;

    [MenuItem("Tools/Octree Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<OctreeVisualizerWindow>("Octree Visualizer");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        GUILayout.Label("Octree Texture Visualizer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        settings = (OctreeSettings)EditorGUILayout.ObjectField("Octree Settings Object", settings, typeof(OctreeSettings), true);
        bakedTexture = (Texture2D)EditorGUILayout.ObjectField("Baked Octree Texture", bakedTexture, typeof(Texture2D), false);
        
        displayDepth = EditorGUILayout.IntSlider("Display Max Depth", displayDepth, 0, 7);
        showOnlyOccupied = EditorGUILayout.Toggle("Show Only Occupied", showOnlyOccupied);
        drawInScene = EditorGUILayout.Toggle("Draw in Scene View", drawInScene);

        EditorGUILayout.Space();
        if (GUILayout.Button("Repaint Scene"))
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Cyan = Intermediate Node\n" +
            "Green = Leaf Node\n" +
            "Settings are pulled directly from the assigned OctreeSettings GameObject.", 
            MessageType.Info
        );
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!drawInScene || bakedTexture == null || settings == null) return;

        if (bakedTexture.format != TextureFormat.RGBA64)
        {
            return;
        }

        try
        {
            NativeArray<ushort> pixelData = bakedTexture.GetPixelData<ushort>(0);
            if (!pixelData.IsCreated || pixelData.Length == 0) return;

            DrawOctreeRecursive(0, settings.bounds, 0, displayDepth, pixelData, showOnlyOccupied);
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