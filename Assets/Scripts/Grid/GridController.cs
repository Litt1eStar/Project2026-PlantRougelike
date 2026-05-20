using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace PlantRoguelike.Grid
{
    public sealed class GridController : MonoBehaviour
    {
        [Header("Dimensions")]
        [Min(1)] [SerializeField] private int   width    = 32;
        [Min(1)] [SerializeField] private int   height   = 32;
        [Min(0.01f)] [SerializeField] private float cellSize = 1f;

        [Header("Origin")]
        [Tooltip("Transform whose XZ position anchors the grid's (0,0) corner. Defaults to this GameObject's transform when null. Sampled once at Awake.")]
        [SerializeField] private Transform originTransform;

        private NativeArray<GridCell>             cells;
        private Dictionary<int, IGridPlaceable>   occupants;
        private int                               nextOccupantId = 1;
        private Vector3                           origin;
        private bool                              initialized;

        public int   Width    => width;
        public int   Height   => height;
        public float CellSize => cellSize;
        public Vector3 Origin => origin;

        public event Action<Vector2Int, IGridPlaceable>             OnPlaced;
        public event Action<Vector2Int, IGridPlaceable>             OnRemoved;
        public event Action<Vector2Int, Vector2Int, IGridPlaceable> OnMoved;

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (cells.IsCreated) cells.Dispose();
            occupants = null;
            initialized = false;
        }

        private void Initialize()
        {
            if (initialized) return;

            var t = originTransform != null ? originTransform : transform;
            origin = t.position;

            cells = new NativeArray<GridCell>(width * height, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < cells.Length; i++)
                cells[i] = new GridCell { type = CellType.Grass };

            occupants = new Dictionary<int, IGridPlaceable>(64);
            initialized = true;
        }

        // ---------- Coordinate conversion ----------

        public int Index(Vector2Int c) => c.y * width + c.x;

        public bool InBounds(Vector2Int c) =>
            (uint)c.x < (uint)width && (uint)c.y < (uint)height;

        public Vector3 ToWorld(Vector2Int c) =>
            origin + new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        public Vector2Int FromWorld(Vector3 worldPos) =>
            new Vector2Int(
                Mathf.FloorToInt((worldPos.x - origin.x) / cellSize),
                Mathf.FloorToInt((worldPos.z - origin.z) / cellSize));

        public Vector3 FootprintCenterWorld(Vector2Int originCoord, Vector2Int size) =>
            origin + new Vector3((originCoord.x + size.x * 0.5f) * cellSize,
                                 0f,
                                 (originCoord.y + size.y * 0.5f) * cellSize);

        // ---------- Queries ----------

        public GridCell GetCell(Vector2Int coord)
        {
            if (!InBounds(coord)) return default;
            return cells[Index(coord)];
        }

        public bool IsOccupied(Vector2Int coord)
        {
            if (!InBounds(coord)) return true;
            return cells[Index(coord)].occupantId != 0;
        }

        public IGridPlaceable GetOccupant(Vector2Int coord)
        {
            if (!InBounds(coord)) return null;
            int id = cells[Index(coord)].occupantId;
            if (id == 0) return null;
            occupants.TryGetValue(id, out var o);
            return o;
        }

        public bool CanPlace(PlaceableData data, Vector2Int originCoord)
        {
            if (data == null) return false;
            var size = data.size;
            if (size.x <= 0 || size.y <= 0) return false;

            for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
            {
                var c = new Vector2Int(originCoord.x + dx, originCoord.y + dy);
                if (!InBounds(c)) return false;

                var cell = cells[Index(c)];
                if (cell.occupantId != 0) return false;
                if (!IsCellTypeAllowed(cell.type, data.allowedCellTypes)) return false;
            }
            return true;
        }

        public IReadOnlyList<Vector2Int> GetFootprint(PlaceableData data, Vector2Int originCoord)
        {
            var size = data.size;
            var list = new List<Vector2Int>(size.x * size.y);
            for (int dy = 0; dy < size.y; dy++)
                for (int dx = 0; dx < size.x; dx++)
                    list.Add(new Vector2Int(originCoord.x + dx, originCoord.y + dy));
            return list;
        }

        // ---------- Mutations ----------

        public bool TryPlace(IGridPlaceable item, Vector2Int originCoord)
        {
            if (item == null) return false;
            if (!CanPlace(item.Data, originCoord)) return false;

            int id = nextOccupantId++;
            item.OccupantId = id;
            occupants[id] = item;

            var size = item.Data.size;
            for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
            {
                var c = new Vector2Int(originCoord.x + dx, originCoord.y + dy);
                var cell = cells[Index(c)];
                cell.occupantId = id;
                cells[Index(c)] = cell;
            }

            item.OnPlaced(this, originCoord);
            OnPlaced?.Invoke(originCoord, item);
            return true;
        }

        public bool CanMove(IGridPlaceable item, Vector2Int newOrigin)
        {
            if (item == null || item.Data == null) return false;
            if (item.OccupantId == 0 || !occupants.ContainsKey(item.OccupantId)) return false;

            var size = item.Data.size;
            int myId = item.OccupantId;

            for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
            {
                var c = new Vector2Int(newOrigin.x + dx, newOrigin.y + dy);
                if (!InBounds(c)) return false;

                var cell = cells[Index(c)];
                if (cell.occupantId != 0 && cell.occupantId != myId) return false;
                if (!IsCellTypeAllowed(cell.type, item.Data.allowedCellTypes)) return false;
            }
            return true;
        }

        public bool TryMove(IGridPlaceable item, Vector2Int newOrigin)
        {
            if (!CanMove(item, newOrigin)) return false;
            if (item.Origin == newOrigin) return true;

            int myId = item.OccupantId;
            var oldOrigin = item.Origin;
            var size = item.Data.size;

            // Free old cells owned by us.
            for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
            {
                var c = new Vector2Int(oldOrigin.x + dx, oldOrigin.y + dy);
                if (!InBounds(c)) continue;
                var cell = cells[Index(c)];
                if (cell.occupantId == myId)
                {
                    cell.occupantId = 0;
                    cells[Index(c)] = cell;
                }
            }

            // Claim new cells.
            for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
            {
                var c = new Vector2Int(newOrigin.x + dx, newOrigin.y + dy);
                var cell = cells[Index(c)];
                cell.occupantId = myId;
                cells[Index(c)] = cell;
            }

            item.OnMoved(this, newOrigin);
            OnMoved?.Invoke(oldOrigin, newOrigin, item);
            return true;
        }

        public bool Remove(Vector2Int coord)
        {
            if (!InBounds(coord)) return false;
            int id = cells[Index(coord)].occupantId;
            if (id == 0) return false;
            if (!occupants.TryGetValue(id, out var item)) return false;

            var origin = item.Origin;
            var size   = item.Data.size;
            for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
            {
                var c = new Vector2Int(origin.x + dx, origin.y + dy);
                if (!InBounds(c)) continue;
                var cell = cells[Index(c)];
                if (cell.occupantId == id)
                {
                    cell.occupantId = 0;
                    cells[Index(c)] = cell;
                }
            }

            occupants.Remove(id);
            item.OnRemoved();
            OnRemoved?.Invoke(origin, item);
            return true;
        }

        // ---------- Helpers ----------

        private static bool IsCellTypeAllowed(CellType type, CellType[] allowed)
        {
            if (allowed == null || allowed.Length == 0) return true;
            for (int i = 0; i < allowed.Length; i++)
                if (allowed[i] == type) return true;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var t = originTransform != null ? originTransform : transform;
            var o = Application.isPlaying ? origin : t.position;
            var size = new Vector3(width * cellSize, 0f, height * cellSize);
            var center = o + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, new Vector3(size.x, 0.01f, size.z));
        }
#endif
    }
}
