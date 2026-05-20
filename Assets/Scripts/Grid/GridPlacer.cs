using UnityEngine;

namespace PlantRoguelike.Grid
{
    [RequireComponent(typeof(GridController))]
    public sealed class GridPlacer : MonoBehaviour
    {
        [SerializeField] private Camera        aimCamera;
        [SerializeField] private PlaceableData currentPlaceable;

        [Header("Ghost")]
        [SerializeField] private Color validColor   = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        private GridController controller;
        private GameObject     ghost;
        private Material       ghostMaterial;
        private Renderer[]     ghostRenderers;
        private PlaceableData  ghostFor;

        private void Awake()
        {
            controller = GetComponent<GridController>();
            if (aimCamera == null) aimCamera = Camera.main;
        }

        private void OnDisable()
        {
            DestroyGhost();
        }

        private void Update()
        {
            if (controller == null || aimCamera == null || currentPlaceable == null)
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }

            if (!TryGetHoveredCell(out var coord))
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }

            EnsureGhost();
            bool canPlace = controller.CanPlace(currentPlaceable, coord);
            UpdateGhost(coord, canPlace);

            if (Input.GetMouseButtonDown(0) && canPlace)
                Place(coord);
            else if (Input.GetMouseButtonDown(1))
                controller.Remove(coord);
        }

        private bool TryGetHoveredCell(out Vector2Int coord)
        {
            coord = default;
            var ray   = aimCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, controller.Origin.y, 0f));
            if (!plane.Raycast(ray, out float enter)) return false;

            var hit = ray.GetPoint(enter);
            var c = controller.FromWorld(hit);
            if (!controller.InBounds(c)) return false;
            coord = c;
            return true;
        }

        private void Place(Vector2Int coord)
        {
            var host = new GameObject(currentPlaceable.id ?? currentPlaceable.name);
            var entity = host.AddComponent<TestPlaceableEntity>();
            entity.Data = currentPlaceable;

            if (!controller.TryPlace(entity, coord))
                Destroy(host);
        }

        // ---------- Ghost ----------

        private void EnsureGhost()
        {
            if (ghost != null && ghostFor == currentPlaceable) return;

            DestroyGhost();
            if (currentPlaceable == null || currentPlaceable.prefab == null) return;

            ghost = Instantiate(currentPlaceable.prefab);
            ghost.name = "PlacementGhost";
            ghost.hideFlags = HideFlags.HideAndDontSave;

            foreach (var col in ghost.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            ghostMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ghostMaterial.SetFloat("_Surface", 1f);   // 0 opaque, 1 transparent
            ghostMaterial.SetFloat("_Blend",   0f);   // 0 alpha
            ghostMaterial.SetOverrideTag("RenderType", "Transparent");
            ghostMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghostMaterial.SetInt("_ZWrite", 0);
            ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
            foreach (var r in ghostRenderers)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            ghostFor = currentPlaceable;
        }

        private void UpdateGhost(Vector2Int coord, bool valid)
        {
            if (ghost == null) return;
            ghost.SetActive(true);
            ghost.transform.position = controller.ToWorld(coord);
            ghostMaterial.SetColor("_BaseColor", valid ? validColor : invalidColor);
        }

        private void DestroyGhost()
        {
            if (ghost != null)
            {
                if (Application.isPlaying) Destroy(ghost);
                else DestroyImmediate(ghost);
                ghost = null;
            }
            if (ghostMaterial != null)
            {
                if (Application.isPlaying) Destroy(ghostMaterial);
                else DestroyImmediate(ghostMaterial);
                ghostMaterial = null;
            }
            ghostRenderers = null;
            ghostFor = null;
        }
    }
}
