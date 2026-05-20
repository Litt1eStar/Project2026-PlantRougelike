using UnityEngine;
using UnityEngine.Rendering;

namespace PlantRoguelike.Grid
{
    // Builds an alpha-blended URP/Unlit material instance. Shared by ghost and
    // marquee. Caller owns disposal.
    internal static class TransparentURPMaterial
    {
        public static Material Create()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return mat;
        }
    }
}
