using System.Collections.Generic;
using UnityEngine;

namespace PlantRoguelike.Grid
{
    // Renders an outline around an IGridPlaceable while it's selected by
    // spawning duplicate MeshRenderers as children that use the
    // "PlantRoguelike/SelectionOutline" shader (inverted-hull outline).
    //
    // The item must be reachable via a MonoBehaviour (cast through it to walk
    // the GameObject hierarchy). Skinned meshes are not supported; add a
    // SkinnedMeshRenderer branch if needed later.
    public sealed class SelectionHighlighter
    {
        private readonly Color outlineColor;
        private readonly float outlineWidth;

        private Material              material;
        private readonly List<GameObject> duplicates = new();
        private IGridPlaceable        current;

        public SelectionHighlighter(Color outlineColor, float outlineWidth)
        {
            this.outlineColor = outlineColor;
            this.outlineWidth = outlineWidth;
        }

        public void Set(IGridPlaceable item)
        {
            if (current == item) return;
            ClearDuplicates();
            current = item;
            if (item != null) Spawn(item);
        }

        public void Clear() => Set(null);

        public void Dispose()
        {
            ClearDuplicates();
            current = null;
            if (material != null)
            {
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
                material = null;
            }
        }

        private void Spawn(IGridPlaceable item)
        {
            if (item is not MonoBehaviour host) return;
            EnsureMaterial();
            if (material == null) return;

            foreach (var mf in host.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;

                var dup = new GameObject("SelectionOutline")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = mf.gameObject.layer,
                };
                dup.transform.SetParent(mf.transform, false);

                var newMf = dup.AddComponent<MeshFilter>();
                var newMr = dup.AddComponent<MeshRenderer>();
                newMf.sharedMesh = mf.sharedMesh;
                newMr.sharedMaterial = material;
                newMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                newMr.receiveShadows = false;

                duplicates.Add(dup);
            }
        }

        private void ClearDuplicates()
        {
            foreach (var d in duplicates)
            {
                if (d == null) continue;
                if (Application.isPlaying) Object.Destroy(d);
                else Object.DestroyImmediate(d);
            }
            duplicates.Clear();
        }

        private void EnsureMaterial()
        {
            if (material != null) return;
            var shader = Shader.Find("PlantRoguelike/SelectionOutline");
            if (shader == null) return;
            material = new Material(shader);
            material.SetColor("_OutlineColor", outlineColor);
            material.SetFloat("_OutlineWidth", outlineWidth);
        }
    }
}
