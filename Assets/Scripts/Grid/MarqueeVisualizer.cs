using UnityEngine;

namespace PlantRoguelike.Grid
{
    // Renders a translucent rectangular quad on a grid's XZ plane between two
    // cell coordinates. Lazy-created, reusable by any tool that needs rect
    // selection feedback (place, remove, move, paint, inspect...).
    public sealed class MarqueeVisualizer
    {
        private readonly GridController grid;
        private readonly float          yOffset;

        private GameObject quad;
        private Material   material;

        public MarqueeVisualizer(GridController grid, float yOffset = 0.02f)
        {
            this.grid    = grid;
            this.yOffset = yOffset;
        }

        public void Show(Vector2Int min, Vector2Int max, Color color)
        {
            Ensure();

            float cs = grid.CellSize;
            var origin = grid.Origin;
            float w = (max.x - min.x + 1) * cs;
            float h = (max.y - min.y + 1) * cs;

            quad.transform.position = new Vector3(
                origin.x + min.x * cs,
                origin.y + yOffset,
                origin.z + min.y * cs);
            quad.transform.localScale = new Vector3(w, 1f, h);

            material.SetColor("_BaseColor", color);
            quad.SetActive(true);
        }

        public void Hide()
        {
            if (quad != null) quad.SetActive(false);
        }

        public void Dispose()
        {
            if (quad != null)
            {
                if (Application.isPlaying) Object.Destroy(quad);
                else Object.DestroyImmediate(quad);
                quad = null;
            }
            if (material != null)
            {
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
                material = null;
            }
        }

        private void Ensure()
        {
            if (quad != null) return;

            material = TransparentURPMaterial.Create();

            quad = new GameObject("MarqueeQuad") { hideFlags = HideFlags.HideAndDontSave };
            var mf = quad.AddComponent<MeshFilter>();
            var mr = quad.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildUnitXZQuad();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // Unit quad on XZ plane spanning [0,0]..[1,1], normal +Y.
        private static Mesh BuildUnitXZQuad()
        {
            var mesh = new Mesh { name = "MarqueeQuadMesh" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 1f),
                new Vector3(0f, 0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
