using UnityEngine;

namespace PlantRoguelike.Grid
{
    public abstract class PlaceableData : ScriptableObject
    {
        public string       id;
        public Vector2Int[] footprint = { Vector2Int.zero };
        public CellType[]   allowedCellTypes = { CellType.Grass, CellType.Soil };
        public GameObject   prefab;
    }
}
