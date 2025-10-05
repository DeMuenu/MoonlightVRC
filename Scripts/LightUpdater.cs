using System;
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Rendering;

public partial class LightUpdater : UdonSharpBehaviour 
{
    [Header("Lightsources")]
    [Tooltip("Place Transforms here which should also emit Light (attach LightdataStorage to them).")]
    public Transform[] otherLightSources; 


    [Header("Strength")]
    [Tooltip("Local player light range")]
    public float lightStrengthLocal = 10f;

    [Tooltip("Remote players light range")]
    public float lightStrengthRemote = 5f;

    [Tooltip("Players light intensity")]
    public float playerLightIntensity = 5f;
    public float remoteLightIntensity = 2f;

    [Tooltip("0 = no shadows, 1-4 = shadow map index")]
    public float PlayerShadowMapIndex = 0f; // 0 = no shadows, 1-4 = shadow map index


    public float updateInterval = 0.025f;

    [Header("Shader property names (advanced users)")]
    [Tooltip("Vector4 array: xyz = position, w = range")]
    public string positionsProperty = "_Udon_LightPositions";

    [Tooltip("Actual array count")]
    public string countProperty = "_Udon_PlayerCount";

    [Tooltip("RGBA array: rgb = color, a = intensity")]
    public string colorProperty = "_Udon_LightColors";

    [Tooltip("Vector4 array: xyz = direction, w = spot in degrees")]
    public string directionsProperty = "_Udon_LightDirections";

    [Tooltip("float array: light type (1=area, 2=cone, etc)")]
    public string typeProperty = "_Udon_LightType";

    [Tooltip("float array: shadow map index (0=none, 1-4=shadow map index)")]
    public string shadowMapIndexProperty = "_Udon_ShadowMapIndex";

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
    private float[] _ShadowMapArray;
    private bool _ShadowMap_isDirty = false;

    private VRCPlayerApi[] _players;


    public int currentCount { get; private set; }

    private float _nextUpdate = 0f;

    private int UdonID_PlayerPositions;
    private int UdonID_LightCount;
    private int UdonID_LightColors;
    private int UdonID_LightDirections;
    private int UdonID_LightType;
    private int UdonID_ShadowMapIndex;

    void Start()
    {
        if (maxLights < 1) maxLights = 1;

        _positions   = new Vector4[maxLights];
        _lightColors = new Vector4[maxLights];
        _directions  = new Vector4[maxLights];
        _TypeArray   = new float[maxLights];
        _ShadowMapArray = new float[maxLights];

        _players = new VRCPlayerApi[maxLights];

        UdonID_PlayerPositions = VRCShader.PropertyToID(positionsProperty);
        UdonID_LightCount = VRCShader.PropertyToID(countProperty);
        UdonID_LightColors = VRCShader.PropertyToID(colorProperty);
        UdonID_LightDirections = VRCShader.PropertyToID(directionsProperty);
        UdonID_LightType = VRCShader.PropertyToID(typeProperty);
        UdonID_ShadowMapIndex = VRCShader.PropertyToID(shadowMapIndexProperty);


        UpdateData();
        PushToRenderers();
    }

    void LateUpdate()
    {
        if (Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + updateInterval;

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
                if (_ShadowMapArray[i] != PlayerShadowMapIndex)
                {
                    _ShadowMapArray[i] = PlayerShadowMapIndex;
                    _ShadowMap_isDirty = true;
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
                if (_ShadowMapArray[i] != 0f)
                {
                    _ShadowMapArray[i] = 0f;
                    _ShadowMap_isDirty = true;
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


                float   Lightangle = (data != null) ? data.GetCosHalfAngle() : 0f;

                Vector4 posTemp = Vector4.zero;
                if (data.lightType == LightType.Sphere)
                {
                    posTemp = new Vector4(pos.x, pos.y, pos.z, range);
                }
                else
                {
                    posTemp = new Vector4(pos.x, pos.y, pos.z, Mathf.Cos(Mathf.Deg2Rad * ((data.spotAngleDeg * 0.5f) + Mathf.Max(data.range, 0))));
                }
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
                Vector4 dirTemp = new Vector4(fwd.x, fwd.y, fwd.z, Lightangle);
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

                float shadowMapIndex = (data != null) ? data.shadowMapIndex : 0f;
                if (_ShadowMapArray[currentCount] != shadowMapIndex)
                {
                    _ShadowMapArray[currentCount] = shadowMapIndex;
                    _ShadowMap_isDirty = true;
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

            if (_ShadowMapArray[i] != 0f)
            {
                _ShadowMapArray[i] = 0f;
                _ShadowMap_isDirty = true;
            }
        }
    }

    private void PushToRenderers()
    {

        // Snapshot which things are dirty this frame
        bool pushPositions = _positons_isDirty;
        bool pushColors = _lightColors_isDirty;
        bool pushDirs = _directions_isDirty;
        bool pushTypes = _TypeArray_isDirty && !string.IsNullOrEmpty(typeProperty);
        bool pushShadowMap = _ShadowMap_isDirty;


        if (pushPositions) VRCShader.SetGlobalVectorArray(UdonID_PlayerPositions, _positions);
        if (pushColors) VRCShader.SetGlobalVectorArray(UdonID_LightColors, _lightColors);
        if (pushDirs) VRCShader.SetGlobalVectorArray(UdonID_LightDirections, _directions);
        if (pushTypes) VRCShader.SetGlobalFloatArray(UdonID_LightType, _TypeArray);
        if (pushShadowMap) VRCShader.SetGlobalFloatArray(UdonID_ShadowMapIndex, _ShadowMapArray);

        VRCShader.SetGlobalFloat(UdonID_LightCount, currentCount);
        Debug.Log($"[MoonlightVRC] Pushed {currentCount} lights to shader.");

        // Only now mark them clean
        if (pushPositions) { _positons_isDirty = false; }
        if (pushColors) { _lightColors_isDirty = false; }
        if (pushDirs) { _directions_isDirty = false; }
        if (pushTypes) { _TypeArray_isDirty = false; }
        if (pushShadowMap) { _ShadowMap_isDirty = false; }
    }
}
