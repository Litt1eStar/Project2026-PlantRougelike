using UnityEngine;

namespace PlantRoguelike.Grid
{
    [ExecuteAlways]
    [RequireComponent(typeof(GridController))]
    public sealed class GridVisualizer : MonoBehaviour
    {
        [SerializeField] private Shader shader;
        [SerializeField] private bool   showLines     = true;
        [SerializeField] private Color  lineColor     = new Color(1f, 1f, 1f, 0.4f);
        [Range(0.001f, 0.2f)]
        [SerializeField] private float  lineThickness = 0.04f;

        public bool ShowLines
        {
            get => showLines;
            set { showLines = value; Refresh(); }
        }

        private GridController controller;
        private GameObject     quad;
        private MeshRenderer   renderer_;
        private Material       material;

        private static readonly int IdGridDim       = Shader.PropertyToID("_GridDim");
        private static readonly int IdLineColor     = Shader.PropertyToID("_LineColor");
        private static readonly int IdLineThickness = Shader.PropertyToID("_LineThickness");

        private void OnEnable()
        {
            controller = GetComponent<GridController>();
            EnsureQuad();
            Refresh();
        }

        private void OnDisable()
        {
            if (quad != null)
            {
                if (Application.isPlaying) Destroy(quad);
                else DestroyImmediate(quad);
                quad = null;
            }
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
                material = null;
            }
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void EnsureQuad()
        {
            if (quad != null) return;

            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "GridVisualizerQuad";
            quad.hideFlags = HideFlags.HideAndDontSave;
            quad.transform.SetParent(transform, worldPositionStays: false);

            // Quad faces +Z by default; rotate to lie on XZ plane facing +Y.
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Remove the auto-added collider — visualizer shouldn't block raycasts.
            var col = quad.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            renderer_ = quad.GetComponent<MeshRenderer>();
            renderer_.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer_.receiveShadows = false;

            var sh = shader != null ? shader : Shader.Find("PlantRoguelike/GridLines");
            material = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            renderer_.sharedMaterial = material;
        }

        private void Refresh()
        {
            if (controller == null || quad == null || material == null) return;

            if (renderer_ != null) renderer_.enabled = showLines;

            int   w  = controller.Width;
            int   h  = controller.Height;
            float cs = controller.CellSize;

            // Position the quad so its (0,0) corner is at the grid origin.
            var t = quad.transform;
            t.localPosition = new Vector3(w * cs * 0.5f, 0f, h * cs * 0.5f);
            t.localScale    = new Vector3(w * cs, h * cs, 1f);

            material.SetVector(IdGridDim, new Vector4(w, h, 0f, 0f));
            material.SetColor (IdLineColor, lineColor);
            material.SetFloat (IdLineThickness, lineThickness);
        }
    }
}
