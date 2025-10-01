
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
            _mpb.SetTexture("_Udon_shadowCasterTex" + "_" + shadowcasterIndex.ToString(), ShadowcasterTexture);
            _mpb.SetColor("_Udon_shadowCasterColor" + "_" + shadowcasterIndex.ToString(), TextureColor);
            _mpb.SetColor("_Udon_OutSideColor" + "_" + shadowcasterIndex.ToString(), OutsideColor);
            _mpb.SetFloat("_Udon_MinBrightnessShadow" + "_" + shadowcasterIndex.ToString(), MinBrightness);
            mat.SetPropertyBlock(_mpb);
        }
    }
    void LateUpdate()
    {

        foreach (Renderer mat in rendererTargets)
        {
            if (mat == null) continue;
            mat.GetPropertyBlock(_mpb);

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

            _mpb.SetVector("_Udon_Plane_Origin_" + shadowcasterIndex.ToString(), new Vector4(transform.position.x, transform.position.y, transform.position.z, 0));
            _mpb.SetVector("_Udon_Plane_Uinv_" + shadowcasterIndex.ToString(),   new Vector4(Uinv.x, Uinv.y, Uinv.z, 0));
            _mpb.SetVector("_Udon_Plane_Vinv_" + shadowcasterIndex.ToString(),   new Vector4(Vinv.x, Vinv.y, Vinv.z, 0));
            _mpb.SetVector("_Udon_Plane_Normal_" + shadowcasterIndex.ToString(), new Vector4(N.x, N.y, N.z, 0));

            mat.SetPropertyBlock(_mpb);
        }

    }
}
