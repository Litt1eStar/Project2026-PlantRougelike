using System;
using UnityEngine;

namespace PlantRoguelike.Grid
{
    [RequireComponent(typeof(GridController))]
    public sealed class GridPlacer : MonoBehaviour
    {
        [SerializeField] private Camera             aimCamera;
        [SerializeField] private GridModeController modeController;
        [SerializeField] private PlaceableData      currentPlaceable;

        [Header("Ghost")]
        [SerializeField] private Color validColor   = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        [Header("Marquee")]
        [SerializeField] private Color placeValidColor    = new Color(0.3f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color placeInvalidColor  = new Color(1f, 0.3f, 0.3f, 0.35f);
        [SerializeField] private Color removeValidColor   = new Color(1f, 0.6f, 0.2f, 0.35f);
        [SerializeField] private Color removeInvalidColor = new Color(0.5f, 0.5f, 0.5f, 0.25f);
        [SerializeField] private float marqueeYOffset     = 0.02f;

        [Header("Move")]
        [SerializeField] private float doubleClickThreshold = 0.3f;

        private GridController    controller;
        private PlacementGhost    ghost;
        private MarqueeVisualizer marquee;

        private Vector2Int?    placeDragStart;
        private Vector2Int?    removeDragStart;
        private IGridPlaceable selectedItem;        // carried during move
        private IGridPlaceable inspectedItem;       // selected in Selection mode
        private float          lastLeftDownTime = -999f;
        private Vector2Int     lastLeftDownCell;

        // Fires when Selection-mode click changes the inspected item.
        // null = nothing selected (e.g., clicked empty cell).
        public event Action<IGridPlaceable> OnItemSelected;

        private void Awake()
        {
            controller = GetComponent<GridController>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (modeController == null) modeController = GetComponent<GridModeController>();

            ghost   = new PlacementGhost(controller, validColor, invalidColor);
            marquee = new MarqueeVisualizer(controller, marqueeYOffset);
        }

        private void OnEnable()
        {
            if (modeController != null) modeController.OnModeChanged += HandleModeChanged;
        }

        private void OnDisable()
        {
            if (modeController != null) modeController.OnModeChanged -= HandleModeChanged;
            CancelActiveInteractions();
            ghost?.Dispose();
            marquee?.Dispose();
        }

        private void HandleModeChanged(GridInteractionMode _)
        {
            CancelActiveInteractions();
        }

        private void CancelActiveInteractions()
        {
            if (selectedItem != null)
            {
                selectedItem.SetVisualHidden(false);
                selectedItem = null;
            }
            placeDragStart = null;
            removeDragStart = null;
            ghost?.Hide();
            marquee?.Hide();
        }

        private void Update()
        {
            if (controller == null || aimCamera == null)
            {
                ghost?.Hide();
                marquee?.Hide();
                return;
            }

            bool hasCell = TryGetHoveredCell(out var coord);

            var mode = modeController != null ? modeController.Mode : GridInteractionMode.Building;

            if (mode == GridInteractionMode.Selection)
            {
                ghost.Hide();
                marquee.Hide();
                UpdateSelection(hasCell, coord);
                return;
            }

            // Building / Planting: place, remove, move.
            // Move (double-LMB pickup) takes top priority. While carrying an
            // item, all other inputs are suppressed.
            if (UpdateMove(hasCell, coord))
            {
                marquee.Hide();
                return;
            }

            if (currentPlaceable == null)
            {
                ghost.Hide();
                marquee.Hide();
                return;
            }

            // Right-drag (remove) takes priority over left-drag (place).
            if (placeDragStart == null && UpdateRemoveDrag(hasCell, coord))
            {
                ghost.Hide();
                return;
            }

            if (currentPlaceable.size == Vector2Int.one)
                UpdatePlaceDrag(hasCell, coord);
            else
                UpdateSingleClickPlace(hasCell, coord);
        }

        // Selection Mode: LMB picks the IGridPlaceable under the cursor via a
        // physics raycast so visuals that overflow their cell footprint still
        // select correctly. LMB on empty (no entity hit) deselects.
        private void UpdateSelection(bool hasCell, Vector2Int coord)
        {
            if (!Input.GetMouseButtonDown(0)) return;

            var ray = aimCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                var item = hit.collider.GetComponentInParent<IGridPlaceable>();
                if (item != null)
                {
                    SetInspected(item);
                    return;
                }
            }

            SetInspected(null);
        }

        private void SetInspected(IGridPlaceable item)
        {
            if (inspectedItem == item) return;
            inspectedItem = item;

            if (item != null)
            {
                var name = item.Data != null ? (item.Data.id ?? item.Data.name) : "<null data>";
                Debug.Log($"[GridPlacer] Selected {name} at {item.Origin}", item as UnityEngine.Object);
            }
            else
            {
                Debug.Log("[GridPlacer] Selection cleared");
            }
            OnItemSelected?.Invoke(item);
        }

        // Returns true if a move flow is active this frame (pick or carrying).
        // Pickup = double-left-click on an occupied cell.
        // Drop   = single-left-click while carrying.
        // Cancel = right-click while carrying. Object stays at its original cell
        //          and its PlaceableData is loaded as currentPlaceable so the
        //          user can keep placing more of the same kind.
        private bool UpdateMove(bool hasCell, Vector2Int coord)
        {
            if (selectedItem != null && Input.GetMouseButtonDown(1))
            {
                currentPlaceable = selectedItem.Data;
                selectedItem.SetVisualHidden(false);
                selectedItem = null;
                ghost.Hide();
                return true;
            }

            bool leftDown = Input.GetMouseButtonDown(0);

            if (selectedItem != null)
            {
                if (hasCell)
                {
                    bool canMove = controller.CanMove(selectedItem, coord);
                    ghost.Show(selectedItem.Data, coord, canMove);
                }
                else
                {
                    ghost.Hide();
                }

                if (leftDown)
                {
                    if (hasCell) controller.TryMove(selectedItem, coord);
                    selectedItem.SetVisualHidden(false);
                    selectedItem = null;
                    ghost.Hide();
                    // Consume the click so place-drag doesn't pick it up.
                    lastLeftDownTime = -999f;
                }
                return true;
            }

            // Not carrying: detect double-click on an occupied cell to pick up.
            if (leftDown && hasCell)
            {
                bool isDouble = (Time.unscaledTime - lastLeftDownTime) <= doubleClickThreshold
                                && coord == lastLeftDownCell;
                lastLeftDownTime = Time.unscaledTime;
                lastLeftDownCell = coord;

                if (isDouble)
                {
                    var occupant = controller.GetOccupant(coord);
                    if (occupant != null)
                    {
                        selectedItem = occupant;
                        selectedItem.SetVisualHidden(true);
                        // Kill any place-drag started by the first click of this pair.
                        placeDragStart = null;
                        marquee.Hide();
                        return true;
                    }
                }
            }

            return false;
        }

        // Returns true if a remove drag is active this frame.
        private bool UpdateRemoveDrag(bool hasCell, Vector2Int coord)
        {
            if (Input.GetMouseButtonDown(1) && hasCell)
                removeDragStart = coord;

            if (removeDragStart.HasValue && Input.GetMouseButton(1))
            {
                var (min, max) = RectBounds(removeDragStart.Value, hasCell ? coord : removeDragStart.Value);
                bool anyOccupied = RectHasOccupant(min, max);
                marquee.Show(min, max, anyOccupied ? removeValidColor : removeInvalidColor);
                return true;
            }

            if (removeDragStart.HasValue && Input.GetMouseButtonUp(1))
            {
                var (min, max) = RectBounds(removeDragStart.Value, hasCell ? coord : removeDragStart.Value);
                if (RectHasOccupant(min, max)) RemoveRect(min, max);
                removeDragStart = null;
                marquee.Hide();
                return true;
            }

            return false;
        }

        private void UpdatePlaceDrag(bool hasCell, Vector2Int coord)
        {
            if (Input.GetMouseButtonDown(0) && hasCell)
                placeDragStart = coord;

            if (placeDragStart.HasValue && Input.GetMouseButton(0))
            {
                ghost.Hide();
                var (min, max) = RectBounds(placeDragStart.Value, hasCell ? coord : placeDragStart.Value);
                bool allFree = IsRectAllPlaceable(min, max);
                marquee.Show(min, max, allFree ? placeValidColor : placeInvalidColor);
                return;
            }

            if (placeDragStart.HasValue && Input.GetMouseButtonUp(0))
            {
                var (min, max) = RectBounds(placeDragStart.Value, hasCell ? coord : placeDragStart.Value);
                if (IsRectAllPlaceable(min, max)) PlaceRect(min, max);
                placeDragStart = null;
                marquee.Hide();
                return;
            }

            // Hover preview.
            if (!hasCell) { ghost.Hide(); return; }
            ghost.Show(currentPlaceable, coord, controller.CanPlace(currentPlaceable, coord));
        }

        private void UpdateSingleClickPlace(bool hasCell, Vector2Int coord)
        {
            marquee.Hide();
            placeDragStart = null;

            if (!hasCell) { ghost.Hide(); return; }

            bool canPlace = controller.CanPlace(currentPlaceable, coord);
            ghost.Show(currentPlaceable, coord, canPlace);

            if (Input.GetMouseButtonDown(0) && canPlace)
                Place(coord);
        }

        // ---------- Grid ops ----------

        private void Place(Vector2Int coord)
        {
            var host = new GameObject(currentPlaceable.id ?? currentPlaceable.name);
            var entity = currentPlaceable.CreateInstance(host);

            if (entity == null || !controller.TryPlace(entity, coord))
                Destroy(host);
        }

        private void PlaceRect(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var c = new Vector2Int(x, y);
                if (controller.CanPlace(currentPlaceable, c)) Place(c);
            }
        }

        private void RemoveRect(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var c = new Vector2Int(x, y);
                if (controller.IsOccupied(c)) controller.Remove(c);
            }
        }

        private bool IsRectAllPlaceable(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                if (!controller.CanPlace(currentPlaceable, new Vector2Int(x, y))) return false;
            return true;
        }

        private bool RectHasOccupant(Vector2Int min, Vector2Int max)
        {
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                if (controller.IsOccupied(new Vector2Int(x, y))) return true;
            return false;
        }

        // ---------- Helpers ----------

        private static (Vector2Int min, Vector2Int max) RectBounds(Vector2Int a, Vector2Int b)
        {
            return (
                new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)),
                new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y))
            );
        }

        private bool TryGetHoveredCell(out Vector2Int coord)
        {
            coord = default;
            var ray   = aimCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, controller.Origin.y, 0f));
            if (!plane.Raycast(ray, out float enter)) return false;

            var c = controller.FromWorld(ray.GetPoint(enter));
            if (!controller.InBounds(c)) return false;
            coord = c;
            return true;
        }
    }
}
