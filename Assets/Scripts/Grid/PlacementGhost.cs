using UnityEngine;

namespace PlantRoguelike.Grid
{
    // Single-cell preview of a PlaceableData under the cursor. Rebuilds when the
    // placeable changes; tint switches between valid/invalid colors.
    public sealed class PlacementGhost
    {
        private readonly GridController grid;
        private readonly Color          validColor;
        private readonly Color          invalidColor;

        private GameObject    ghost;
        private Material      material;
        private PlaceableData builtFor;

        public PlacementGhost(GridController grid, Color validColor, Color invalidColor)
        {
            this.grid         = grid;
            this.validColor   = validColor;
            this.invalidColor = invalidColor;
        }

        public void Show(PlaceableData placeable, Vector2Int coord, bool valid)
        {
            if (placeable == null || placeable.prefab == null) { Hide(); return; }
            EnsureBuilt(placeable);
            if (ghost == null) return;

            ghost.SetActive(true);
            ghost.transform.position = grid.FootprintCenterWorld(coord, placeable.size);
            material.SetColor("_BaseColor", valid ? validColor : invalidColor);
        }

        public void Hide()
        {
            if (ghost != null) ghost.SetActive(false);
        }

        public void Dispose()
        {
            if (ghost != null)
            {
                if (Application.isPlaying) Object.Destroy(ghost);
                else Object.DestroyImmediate(ghost);
                ghost = null;
            }
            if (material != null)
            {
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
                material = null;
            }
            builtFor = null;
        }

        private void EnsureBuilt(PlaceableData placeable)
        {
            if (ghost != null && builtFor == placeable) return;
            Dispose();

            material = TransparentURPMaterial.Create();
            ghost = Object.Instantiate(placeable.prefab);
            ghost.name = "PlacementGhost";
            ghost.hideFlags = HideFlags.HideAndDontSave;
            ApplyMaterial(ghost);

            builtFor = placeable;
        }

        private void ApplyMaterial(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
