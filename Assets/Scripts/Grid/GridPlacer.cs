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

        [Header("Selection Box")]
        [SerializeField] private Color selectionValidColor   = new Color(0.3f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color selectionInvalidColor = new Color(1f, 0.3f, 0.3f, 0.35f);
        [SerializeField] private Color removeValidColor      = new Color(1f, 0.6f, 0.2f, 0.35f);
        [SerializeField] private Color removeInvalidColor    = new Color(0.5f, 0.5f, 0.5f, 0.25f);
        [SerializeField] private float selectionYOffset      = 0.02f;

        private GridController controller;
        private GameObject     ghost;
        private Material       ghostMaterial;
        private PlaceableData  ghostFor;

        private Vector2Int? placeDragStart;
        private Vector2Int? removeDragStart;

        private GameObject selectionQuad;
        private Material   selectionMaterial;

        private void Awake()
        {
            controller = GetComponent<GridController>();
            if (aimCamera == null) aimCamera = Camera.main;
        }

        private void OnDisable()
        {
            DestroyGhost();
            DestroySelectionQuad();
        }

        private void Update()
        {
            if (controller == null || aimCamera == null || currentPlaceable == null)
            {
                if (ghost != null) ghost.SetActive(false);
                HideSelectionQuad();
                return;
            }

            bool hasCell = TryGetHoveredCell(out var coord);
            bool isOneByOne = currentPlaceable.size == Vector2Int.one;

            // Right-button drag = remove rect. Mutually exclusive with place drag.
            if (placeDragStart == null && UpdateRemoveDrag(hasCell, coord))
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }

            if (isOneByOne)
            {
                UpdateOneByOne(hasCell, coord);
            }
            else
            {
                UpdateMultiCell(hasCell, coord);
            }
        }

        // Returns true if a remove drag is active this frame (suppress place handling).
        private bool UpdateRemoveDrag(bool hasCell, Vector2Int coord)
        {
            if (Input.GetMouseButtonDown(1) && hasCell)
                removeDragStart = coord;

            if (removeDragStart.HasValue && Input.GetMouseButton(1))
            {
                var end = hasCell ? coord : removeDragStart.Value;
                var (min, max) = RectBounds(removeDragStart.Value, end);
                bool anyOccupied = RectHasOccupant(min, max);
                ShowSelectionQuad(min, max, anyOccupied ? removeValidColor : removeInvalidColor);
                return true;
            }

            if (removeDragStart.HasValue && Input.GetMouseButtonUp(1))
            {
                var end = hasCell ? coord : removeDragStart.Value;
                var (min, max) = RectBounds(removeDragStart.Value, end);
                if (RectHasOccupant(min, max)) RemoveRect(min, max);
                removeDragStart = null;
                HideSelectionQuad();
                return true;
            }

            return false;
        }

        private void UpdateOneByOne(bool hasCell, Vector2Int coord)
        {
            if (Input.GetMouseButtonDown(0) && hasCell)
            {
                placeDragStart = coord;
            }

            if (placeDragStart.HasValue && Input.GetMouseButton(0))
            {
                if (ghost != null) ghost.SetActive(false);

                var end = hasCell ? coord : placeDragStart.Value;
                var (min, max) = RectBounds(placeDragStart.Value, end);
                bool allFree = IsRectAllPlaceable(min, max);
                ShowSelectionQuad(min, max, allFree ? selectionValidColor : selectionInvalidColor);
                return;
            }

            if (placeDragStart.HasValue && Input.GetMouseButtonUp(0))
            {
                var end = hasCell ? coord : placeDragStart.Value;
                var (min, max) = RectBounds(placeDragStart.Value, end);
                if (IsRectAllPlaceable(min, max))
                    PlaceRect(min, max);
                placeDragStart = null;
                HideSelectionQuad();
            }

            // Not dragging: show single-cell ghost.
            if (!placeDragStart.HasValue)
            {
                if (!hasCell)
                {
                    if (ghost != null) ghost.SetActive(false);
                    return;
                }
                EnsureGhost();
                UpdateGhost(coord, controller.CanPlace(currentPlaceable, coord));
            }
        }

        private void UpdateMultiCell(bool hasCell, Vector2Int coord)
        {
            HideSelectionQuad();
            placeDragStart = null;

            if (!hasCell)
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }

            EnsureGhost();
            bool canPlace = controller.CanPlace(currentPlaceable, coord);
            UpdateGhost(coord, canPlace);

            if (Input.GetMouseButtonDown(0) && canPlace)
                Place(coord);
        }

        private static (Vector2Int min, Vector2Int max) RectBounds(Vector2Int a, Vector2Int b)
        {
            var min = new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            var max = new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return (min, max);
        }

        private bool IsRectAllPlaceable(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                if (!controller.CanPlace(currentPlaceable, new Vector2Int(x, y)))
                    return false;
            return true;
        }

        private void PlaceRect(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var c = new Vector2Int(x, y);
                if (controller.CanPlace(currentPlaceable, c))
                    Place(c);
            }
        }

        private bool RectHasOccupant(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                if (controller.IsOccupied(new Vector2Int(x, y)))
                    return true;
            return false;
        }

        // Remove every occupant whose footprint intersects the rect. A multi-cell
        // occupant is removed once; subsequent cells of its footprint become empty
        // and are skipped.
        private void RemoveRect(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var c = new Vector2Int(x, y);
                if (controller.IsOccupied(c))
                    controller.Remove(c);
            }
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

            ghostMaterial = CreateGhostMaterial();
            ghost = Instantiate(currentPlaceable.prefab);
            ghost.name = "PlacementGhost";
            ghost.hideFlags = HideFlags.HideAndDontSave;
            ApplyGhostMaterial(ghost);

            ghostFor = currentPlaceable;
        }

        private void UpdateGhost(Vector2Int coord, bool valid)
        {
            if (ghost == null) return;
            ghost.SetActive(true);
            ghost.transform.position = controller.FootprintCenterWorld(coord, currentPlaceable.size);
            ghostMaterial.SetColor("_BaseColor", valid ? validColor : invalidColor);
        }

        private static Material CreateGhostMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return mat;
        }

        private void ApplyGhostMaterial(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
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
            ghostFor = null;
        }

        // ---------- Selection quad ----------

        private void EnsureSelectionQuad()
        {
            if (selectionQuad != null) return;

            selectionMaterial = CreateGhostMaterial();

            selectionQuad = new GameObject("PlacementSelectionQuad");
            selectionQuad.hideFlags = HideFlags.HideAndDontSave;

            var mf = selectionQuad.AddComponent<MeshFilter>();
            var mr = selectionQuad.AddComponent<MeshRenderer>();
            mf.sharedMesh = CreateXZQuadMesh();
            mr.sharedMaterial = selectionMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void ShowSelectionQuad(Vector2Int min, Vector2Int max, Color color)
        {
            EnsureSelectionQuad();

            float cs = controller.CellSize;
            var origin = controller.Origin;
            float w = (max.x - min.x + 1) * cs;
            float h = (max.y - min.y + 1) * cs;

            selectionQuad.transform.position = new Vector3(
                origin.x + min.x * cs,
                origin.y + selectionYOffset,
                origin.z + min.y * cs);
            selectionQuad.transform.localScale = new Vector3(w, 1f, h);

            selectionMaterial.SetColor("_BaseColor", color);
            selectionQuad.SetActive(true);
        }

        private void HideSelectionQuad()
        {
            if (selectionQuad != null) selectionQuad.SetActive(false);
        }

        private void DestroySelectionQuad()
        {
            if (selectionQuad != null)
            {
                if (Application.isPlaying) Destroy(selectionQuad);
                else DestroyImmediate(selectionQuad);
                selectionQuad = null;
            }
            if (selectionMaterial != null)
            {
                if (Application.isPlaying) Destroy(selectionMaterial);
                else DestroyImmediate(selectionMaterial);
                selectionMaterial = null;
            }
        }

        // Unit quad on XZ plane spanning [0,0]..[1,1], normal +Y.
        private static Mesh CreateXZQuadMesh()
        {
            var mesh = new Mesh { name = "PlacementSelectionQuad" };
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
