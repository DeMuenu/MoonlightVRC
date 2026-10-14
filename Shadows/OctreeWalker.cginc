#ifndef OCTREE_WALKER_INCLUDED
#define OCTREE_WALKER_INCLUDED

// ==========================================
// EXPECTED UNIFORMS (Must be declared in parent shader)
// ==========================================
// Texture2D<float4> _OctreeTex;
// float4 _OctreeTex_TexelSize;
// float4 _BoundsCenter;
// float4 _BoundsSize;
// int _MaxWalkDepth;


// _OctreeTex ("16-bit Octree Texture", 2D) = "white" {}
// _BoundsCenter ("Bounds Center", Vector) = (0,0,0,0)
// _BoundsSize ("Bounds Size", Vector) = (10,10,10,0)
// _MaxWalkDepth ("Max Walk Depth", Int) = 5

// // Test points for line visualization in the shader
// _Pos1 ("World Pos 1", Vector) = (-3, 0, -3, 0)
// _Pos2 ("World Pos 2", Vector) = (3, 0, 3, 0)
// _StepCount ("Raymarch Steps", Int) = 32

struct OctreeNode
{
    bool isOccupied;
    bool isLeaf;
    uint children[8];
};

// Helper to get 2D pixel coordinate from 1D pixel index
uint2 GetOctreeCoord(uint pixelIndex)
{
    uint width = (uint)_OctreeTex_TexelSize.z;
    return uint2(pixelIndex % width, pixelIndex / width);
}

// Read a specific 16-bit channel (0=R, 1=G, 2=B, 3=A) from RGBA64 texture
uint ReadChannel16(uint pixelIndex, uint channel)
{
    uint2 coord = GetOctreeCoord(pixelIndex);
    float4 col = _OctreeTex.Load(int3(coord, 0));
    float val = (channel == 0) ? col.r : (channel == 1) ? col.g : (channel == 2) ? col.b : col.a;
    return (uint)round(val * 65535.0);
}

// Load node data from the 3-pixel layout
OctreeNode LoadOctreeNode(uint nodeIndex)
{
    uint p0 = nodeIndex * 3;
    uint p1 = nodeIndex * 3 + 1;
    uint p2 = nodeIndex * 3 + 2;

    OctreeNode node;
    node.isOccupied = ReadChannel16(p0, 0) > 0;
    node.isLeaf = ReadChannel16(p0, 1) > 0;

    node.children[0] = ReadChannel16(p1, 0);
    node.children[1] = ReadChannel16(p1, 1);
    node.children[2] = ReadChannel16(p1, 2);
    node.children[3] = ReadChannel16(p1, 3);

    node.children[4] = ReadChannel16(p2, 0);
    node.children[5] = ReadChannel16(p2, 1);
    node.children[6] = ReadChannel16(p2, 2);
    node.children[7] = ReadChannel16(p2, 3);

    return node;
}

// Determine which of the 8 child octants the point falls into
uint GetOctant(float3 pos, float3 center)
{
    uint octant = 0;
    if (pos.x >= center.x) octant |= 1;
    if (pos.y >= center.y) octant |= 2;
    if (pos.z >= center.z) octant |= 4;
    return octant;
}

// Query a position and retrieve both its occupancy status AND its exact node bounds
bool QueryNodeAtPos(float3 worldPos, out bool isOccupied, out float3 nodeCenter, out float3 nodeExtents)
{
    nodeCenter = _BoundsCenter.xyz;
    nodeExtents = _BoundsSize.xyz * 0.5f;

    if (any(abs(worldPos - nodeCenter) > nodeExtents))
    {
        isOccupied = false;
        return false;
    }

    uint currentNodeIndex = 0;
    int depth = 0;

    [unroll(10)]
    for (int i = 0; i < 10; i++)
    {
        if (depth >= _MaxWalkDepth) break;

        OctreeNode node = LoadOctreeNode(currentNodeIndex);

        if (!node.isOccupied || node.isLeaf)
        {
            isOccupied = node.isOccupied;
            return true;
        }

        uint octant = GetOctant(worldPos, nodeCenter);
        uint childIndex = node.children[octant];

        if (childIndex == 0)
        {
            isOccupied = false;
            return true; // Empty space inside parent node
        }

        // Update bounds for child node
        float3 childOffset = float3(
            (octant & 1) ? nodeExtents.x * 0.5f : -nodeExtents.x * 0.5f,
            (octant & 2) ? nodeExtents.y * 0.5f : -nodeExtents.y * 0.5f,
            (octant & 4) ? nodeExtents.z * 0.5f : -nodeExtents.z * 0.5f
        );
        nodeCenter += childOffset;
        nodeExtents *= 0.5f;

        currentNodeIndex = childIndex;
        depth++;
    }

    isOccupied = true;
    return true;
}

// DYNAMIC RAYMARCH: Steps from node-to-node using geometry bounds (never misses objects)
bool CheckLineInterrupted(float3 pos1, float3 pos2, int maxSteps, out float3 hitPos)
{
    float3 rayDir = pos2 - pos1;
    float totalLength = length(rayDir);
    
    if (totalLength < 0.0001f)
    {
        hitPos = pos1;
        bool occupied;
        float3 c, e;
        QueryNodeAtPos(pos1, occupied, c, e);
        return occupied;
    }

    rayDir /= totalLength;
    float3 currentPos = pos1;
    float travelled = 0.0f;

    int steps = clamp(maxSteps, 2, 64);

    [unroll(64)]
    for (int i = 0; i < 64; i++)
    {
        if (i >= steps || travelled >= totalLength) break;

        bool isOccupied;
        float3 nodeCenter, nodeExtents;
        QueryNodeAtPos(currentPos, isOccupied, nodeCenter, nodeExtents);

        if (isOccupied)
        {
            hitPos = currentPos;
            return true; // Interrupted by an occupied node!
        }

        // Calculate exact distance to exit the current node's bounding box along the ray
        float3 t1 = (nodeCenter - nodeExtents - currentPos) / rayDir;
        float3 t2 = (nodeCenter + nodeExtents - currentPos) / rayDir;
        float3 tExit = max(t1, t2);
        
        float exitDist = 1e20f;
        if (tExit.x > 0) exitDist = min(exitDist, tExit.x);
        if (tExit.y > 0) exitDist = min(exitDist, tExit.y);
        if (tExit.z > 0) exitDist = min(exitDist, tExit.z);
        
        // Ensure forward progress with a tiny safety epsilon
        exitDist = max(exitDist, 0.001f);

        travelled += exitDist;
        currentPos = pos1 + rayDir * min(travelled, totalLength);
    }

    hitPos = pos2;
    return false; // Path is clear
}

#endif // OCTREE_WALKER_INCLUDED