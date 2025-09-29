
using System.Security.Permissions;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ShadowcasterUpdater : UdonSharpBehaviour
{

    public Renderer[] rendererTargets;
    public string propertyName = "_Udon_WorldToLocal";
    private MaterialPropertyBlock _mpb;

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
    }
    

    void LateUpdate()
    {
        var w2l = transform.worldToLocalMatrix;


        foreach (Renderer mat in rendererTargets)
        {
            if (mat == null) continue;
            mat.GetPropertyBlock(_mpb);
            _mpb.SetMatrix(propertyName, w2l);
            mat.SetPropertyBlock(_mpb);
        }

    }
}
