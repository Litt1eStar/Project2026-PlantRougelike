using UnityEngine;

namespace PlantRoguelike.Grid
{
    public abstract class PlaceableData : ScriptableObject
    {
        public string     id;
        public Vector2Int size = Vector2Int.one;
        public CellType[] allowedCellTypes = { CellType.Grass, CellType.Soil };
        public GameObject prefab;
    }
}
