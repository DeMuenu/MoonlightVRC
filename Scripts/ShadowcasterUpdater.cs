
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

    public string propertyName = "_Udon_WorldToLocal";
    private MaterialPropertyBlock _mpb;


    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        ApplyTextureData();
    }

    public void ApplyTextureData()
    {
        foreach (Renderer mat in rendererTargets)
        {
            if (mat == null) continue;
            mat.GetPropertyBlock(_mpb);
            _mpb.SetTexture("_Udon_ShadowcasterTex" + "_" + (string)shadowcasterIndex, ShadowcasterTexture);
            _mpb.SetColor("_Udon_shadowCasterColor" + "_" + (string)shadowcasterIndex, TextureColor);
            _mpb.SetColor("_Udon_OutSideColor" + "_" + (string)shadowcasterIndex, OutsideColor);
            _mpb.SetFloat("_Udon_MinBrightnessShadow" + "_" + (string)shadowcasterIndex, MinBrightness);
            mat.SetPropertyBlock(_mpb);
        }
    }
    void LateUpdate()
    {
        var w2l = transform.worldToLocalMatrix;


        foreach (Renderer mat in rendererTargets)
        {
            if (mat == null) continue;
            mat.GetPropertyBlock(_mpb);
            _mpb.SetMatrix(propertyName + "_" + (string)shadowcasterIndex, w2l);
            mat.SetPropertyBlock(_mpb);
        }

    }
}
