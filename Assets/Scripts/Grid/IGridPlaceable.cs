using UnityEngine;

namespace PlantRoguelike.Grid
{
    public interface IGridPlaceable
    {
        PlaceableData Data       { get; }
        Vector2Int    Origin     { get; }
        int           OccupantId { get; set; }

        void OnPlaced(GridController grid, Vector2Int origin);
        void OnMoved(GridController grid, Vector2Int newOrigin);
        void OnRemoved();
        void SetVisualHidden(bool hidden);
    }
}
