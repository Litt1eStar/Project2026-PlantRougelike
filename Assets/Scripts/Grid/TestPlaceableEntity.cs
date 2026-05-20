using UnityEngine;

namespace PlantRoguelike.Grid
{
    public sealed class TestPlaceableEntity : MonoBehaviour, IGridPlaceable
    {
        [SerializeField] private PlaceableData data;

        public PlaceableData Data       { get => data; set => data = value; }
        public Vector2Int    Origin     { get; private set; }
        public int           OccupantId { get; set; }

        public void OnPlaced(GridController grid, Vector2Int origin)
        {
            Origin = origin;
            transform.position = grid.FootprintCenterWorld(origin, data.size);

            if (data != null && data.prefab != null)
                Instantiate(data.prefab, transform.position, Quaternion.identity, transform);
        }

        public void OnRemoved()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
