using UnityEngine;

namespace PlantRoguelike.Grid
{
    public abstract class PlaceableData : ScriptableObject
    {
        public string     id;
        public Vector2Int size = Vector2Int.one;
        public CellType[] allowedCellTypes = { CellType.Grass, CellType.Soil };
        public GameObject prefab;

        // Default factory: attaches TestPlaceableEntity to the host and uses its
        // prefab-spawning behavior. Override in subclasses that need a custom
        // IGridPlaceable type (cost, growth, AI, etc.).
        public virtual IGridPlaceable CreateInstance(GameObject host)
        {
            var entity = host.AddComponent<TestPlaceableEntity>();
            entity.Data = this;
            return entity;
        }
    }
}
