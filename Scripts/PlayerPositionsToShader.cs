using System;
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public partial class PlayerPositionsToShader : UdonSharpBehaviour 
{
    [Header("Lightsources")]
    [Tooltip("Place Transforms here which should also emit Light (attach LightdataStorage to them).")]
    public Transform[] otherLightSources; 

    [Header("Renderers that use a supported shader")]
    public Renderer[] targets; 

    [Header("Strength")]
    [Tooltip("Local player light range")]
    public float lightStrengthLocal = 10f;

    [Tooltip("Remote players light range")]
    public float lightStrengthRemote = 5f;

    [Tooltip("Players light intensity")]
    public float playerLightIntensity = 5f;
    public float remoteLightIntensity = 2f;


    [Header("Shader property names (advanced users)")]
    [Tooltip("Vector4 array: xyz = position, w = range")]
    public string positionsProperty = "_PlayerPositions";

    [Tooltip("Actual array count")]
    public string countProperty = "_PlayerCount";

    [Tooltip("RGBA array: rgb = color, a = intensity")]
    public string colorProperty = "_LightColors";

    [Tooltip("Vector4 array: xyz = direction, w = spot in degrees")]
    public string directionsProperty = "_LightDirections";

    [Tooltip("float array: light type (1=area, 2=cone, etc)")]
    public string typeProperty = "_LightType";

    [Header("Max Lights (advanced users)")]
    [Tooltip("Hard cap / array size. 80 = default cap")]
    public int maxLights = 80;



    // Internals
    private Vector4[] _positions;
    private bool _positons_isDirty = false;
    private Vector4[] _lightColors;
    private bool _lightColors_isDirty = false;
    private Vector4[] _directions;
    private bool _directions_isDirty = false;

    private float[] _TypeArray;
    private bool _TypeArray_isDirty = false;

    private VRCPlayerApi[] _players;
    private MaterialPropertyBlock _mpb;

    public int currentCount { get; private set; }

    void Start()
    {
        if (maxLights < 1) maxLights = 1;

        _positions   = new Vector4[maxLights];
        _lightColors = new Vector4[maxLights];
        _directions  = new Vector4[maxLights];
        _TypeArray   = new float[maxLights];

        _players = new VRCPlayerApi[maxLights];
        _mpb = new MaterialPropertyBlock();

        UpdateData();
        PushToRenderers();
    }

    void LateUpdate()
    {
        UpdateData();
        PushToRenderers();
    }

    private void UpdateData()
    {
        currentCount = VRCPlayerApi.GetPlayerCount();


        VRCPlayerApi.GetPlayers(_players);

        // --- Players as light sources ---
        for (int i = 0; i < currentCount && currentCount < maxLights; i++)
        {
            VRCPlayerApi p = _players[i];
            if (Utilities.IsValid(p))
            {
                Vector3 pos = p.GetPosition();
                float lightRange = p.isLocal ? lightStrengthLocal : lightStrengthRemote;
                float intensity = p.isLocal ? playerLightIntensity : remoteLightIntensity;


                Vector4 posTemp = new Vector4(pos.x, pos.y + 1f, pos.z, lightRange);
                if (_positions[i] != posTemp)
                {
                    _positions[i] = posTemp;
                    _positons_isDirty = true;
                }

                Vector4 colorTemp = new Vector4(1f, 1f, 1f, intensity);
                if (_lightColors[i] != colorTemp)
                {
                    _lightColors[i] = colorTemp;
                    _lightColors_isDirty = true;
                }


                //Quaternion rot = p.GetRotation(); //We skip this for players, as they have round lights
                Vector3 fwd = Vector3.up;
                Vector4 TempDir = new Vector4(fwd.x, fwd.y, fwd.z, 10f);
                if (_directions[i] != TempDir)
                {
                    _directions[i] = new Vector4(TempDir.x, TempDir.y, TempDir.z, 10f);
                    _directions_isDirty = true;
                }

                if (_TypeArray[i] != 0f)
                {
                    _TypeArray[i] = 0f;
                    _TypeArray_isDirty = true;
                }


            }
            else
            {
                if (_positions[i] != Vector4.zero)
                {
                    _positions[i] = Vector4.zero;
                    _positons_isDirty = true;
                }
                if (_lightColors[i] != Vector4.zero)
                {
                    _lightColors[i] = Vector4.zero;
                    _lightColors_isDirty = true;
                }
                if (_directions[i] != Vector4.zero)
                {
                    _directions[i] = Vector4.zero;
                    _directions_isDirty = true;
                }
                if (_TypeArray[i] != 0f)
                {
                    _TypeArray[i] = 0f;
                    _TypeArray_isDirty = true;
                }
            }
        }

        // --- Scene light sources ---
        if (otherLightSources != null)
        {
            for (int j = 0; j < otherLightSources.Length && currentCount < maxLights; j++)
            {
                Transform t = otherLightSources[j];
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                LightdataStorage data = t.GetComponent<LightdataStorage>();

                Vector3 pos = t.position;
                float   range = (data != null) ? data.range * t.localScale.x: t.localScale.x;

                // NOTE: we pack intensity into color.w (to match your current shader usage)
                Vector4 col = (data != null) ? data.GetFinalColor() : new Vector4(1f, 1f, 1f, 1f);
                float   intensity = (data != null) ? data.intensity * t.localScale.x : 1f;

                //Vector3 fwd = new Vector3(t.localRotation.x, t.localRotation.y, t.localRotation.z);

                Quaternion rot = t.rotation;
                Vector3 fwd = rot * Vector3.down;


                float   cosHalf = (data != null) ? data.GetCosHalfAngle() : 0f;

                Vector4 posTemp = new Vector4(pos.x, pos.y, pos.z, range);
                if (_positions[currentCount] != posTemp)
                {
                    _positions[currentCount] = posTemp;
                    _positons_isDirty = true;
                }
                Vector4 colorTemp = new Vector4(col.x, col.y, col.z, intensity);
                if (_lightColors[currentCount] != colorTemp)
                {
                    _lightColors[currentCount] = colorTemp;
                    _lightColors_isDirty = true;
                }
                Vector4 dirTemp = new Vector4(fwd.x, fwd.y, fwd.z, cosHalf);
                if (_directions[currentCount] != dirTemp)
                {
                    _directions[currentCount] = dirTemp;
                    _directions_isDirty = true;
                }

                // ✅ Use your custom enum id (Omni=0, Spot=1, Directional=2)
                int typeId = (data != null) ? data.GetTypeId() : 0;
                if (_TypeArray[currentCount] != (float)typeId)
                {
                    _TypeArray[currentCount] = (float)typeId;
                    _TypeArray_isDirty = true;
                }

                currentCount++;
            }
        }

        for (int i = currentCount; i < maxLights; i++)
        {
            if (_positions[i] != Vector4.zero)
            {
                _positions[i] = Vector4.zero;
                _positons_isDirty = true;
            }

            if (_lightColors[i] != Vector4.zero)
            {
                _lightColors[i] = Vector4.zero;
                _lightColors_isDirty = true;
            }

            if (_directions[i] != Vector4.zero)
            {
                _directions[i] = Vector4.zero;
                _directions_isDirty = true;
            }

            if (_TypeArray[i] != 0f)
            {
                _TypeArray[i] = 0f;
                _TypeArray_isDirty = true;
            }
        }
    }

    private void PushToRenderers()
    {
        if (targets == null || targets.Length == 0) return;

        // Snapshot which things are dirty this frame
        bool pushPositions = _positons_isDirty;
        bool pushColors    = _lightColors_isDirty;
        bool pushDirs      = _directions_isDirty;
        bool pushTypes     = _TypeArray_isDirty && !string.IsNullOrEmpty(typeProperty);

        for (int r = 0; r < targets.Length; r++)
        {
            Renderer rd = targets[r];
            if (!Utilities.IsValid(rd)) continue;

            rd.GetPropertyBlock(_mpb);

            if (pushPositions) _mpb.SetVectorArray(positionsProperty, _positions);
            if (pushColors)    _mpb.SetVectorArray(colorProperty,    _lightColors);
            if (pushDirs)      _mpb.SetVectorArray(directionsProperty, _directions);
            if (pushTypes)     _mpb.SetFloatArray(typeProperty, _TypeArray);

            _mpb.SetFloat(countProperty, currentCount);
            rd.SetPropertyBlock(_mpb);
        }

        // Only now mark them clean
        if (pushPositions) { _positons_isDirty = false; Debug.Log("Updated Positions"); }
        if (pushColors)    { _lightColors_isDirty = false; Debug.Log("Updated LightColors"); }
        if (pushDirs)      { _directions_isDirty = false; Debug.Log("Updated Directions"); }
        if (pushTypes)     { _TypeArray_isDirty = false; Debug.Log("Updated TypeArray"); }
    }
}
