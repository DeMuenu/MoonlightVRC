﻿
using System.Security.Permissions;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ShadowcasterUpdater : UdonSharpBehaviour
{

    public Renderer[] rendererTargets;
    public Texture2D ShadowcasterTexture;
    public Color OutsideColor = Color.white;
    public Color TextureColor = Color.white;
    public float MinBrightness = 0.0f;

    public int shadowcasterIndex = 1;

    private MaterialPropertyBlock _mpb;

    private int _propShadowTex;
    private int _propShadowColor;
    private int _propOutsideColor;
    private int _propMinBrightness;
    private int _propPlaneOrigin;
    private int _propPlaneUinv;
    private int _propPlaneVinv;
    private int _propPlaneNormal;

    void Start()
    {
        _mpb = new MaterialPropertyBlock();

        string suf = "_" + shadowcasterIndex.ToString();
        _propShadowTex = VRCShader.PropertyToID("_Udon_shadowCasterTex" + suf);
        _propShadowColor = VRCShader.PropertyToID("_Udon_shadowCasterColor" + suf);
        _propOutsideColor = VRCShader.PropertyToID("_Udon_OutSideColor" + suf);
        _propMinBrightness = VRCShader.PropertyToID("_Udon_MinBrightnessShadow" + suf);

        _propPlaneOrigin = VRCShader.PropertyToID("_Udon_Plane_Origin" + suf);
        _propPlaneUinv = VRCShader.PropertyToID("_Udon_Plane_Uinv" + suf);
        _propPlaneVinv = VRCShader.PropertyToID("_Udon_Plane_Vinv" + suf);
        _propPlaneNormal = VRCShader.PropertyToID("_Udon_Plane_Normal" + suf);

        ApplyTextureData();
    }

    public void ApplyTextureData()
    {
        foreach (Renderer mat in rendererTargets)
        {
            if (mat == null) continue;
            mat.GetPropertyBlock(_mpb);
            if (ShadowcasterTexture != null) _mpb.SetTexture(_propShadowTex, ShadowcasterTexture);
            _mpb.SetColor(_propShadowColor, TextureColor);
            _mpb.SetColor(_propOutsideColor, OutsideColor);
            _mpb.SetFloat(_propMinBrightness, MinBrightness);
            mat.SetPropertyBlock(_mpb);
        }
    }
    void LateUpdate()
    {
        float quadHalfWidth = 0.5f;
        float quadHalfHeight = 0.5f;

        // World-space basis directions from transform
        Vector3 Udir = transform.rotation * Vector3.right;  // plane local +X
        Vector3 Vdir = transform.rotation * Vector3.up;     // plane local +Y

        // World-space half extents after non-uniform scaling
        float halfW = quadHalfWidth  * transform.lossyScale.x;
        float halfH = quadHalfHeight * transform.lossyScale.y;

        // Reciprocal axes so dot(r, Uinv/Vinv) -> [-0.5, 0.5]
        Vector3 Uinv = Udir / (2.0f * Mathf.Max(halfW, 1e-6f));
        Vector3 Vinv = Vdir / (2.0f * Mathf.Max(halfH, 1e-6f));

        // Unit normal
        Vector3 N = Vector3.Normalize(Vector3.Cross(Udir, Vdir));

        Vector4 originVec = new Vector4(transform.position.x, transform.position.y, transform.position.z, 0);
        Vector4 uinvVec = new Vector4(Uinv.x, Uinv.y, Uinv.z, 0);
        Vector4 vinvVec = new Vector4(Vinv.x, Vinv.y, Vinv.z, 0);
        Vector4 nVec = new Vector4(N.x, N.y, N.z, 0);

        foreach (Renderer mat in rendererTargets)
        {
            if (mat == null) continue;
            mat.GetPropertyBlock(_mpb);

            _mpb.SetVector(_propPlaneOrigin, originVec);
            _mpb.SetVector(_propPlaneUinv,   uinvVec);
            _mpb.SetVector(_propPlaneVinv,   vinvVec);
            _mpb.SetVector(_propPlaneNormal, nVec);

            mat.SetPropertyBlock(_mpb);
        }

    }
}
