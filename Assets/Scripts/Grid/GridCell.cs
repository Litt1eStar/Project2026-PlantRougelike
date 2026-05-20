using System.Runtime.InteropServices;

namespace PlantRoguelike.Grid
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GridCell
    {
        public CellType type;
        public byte     flags;
        public int      occupantId;
        public float    nutrient;

        public bool IsEmpty => occupantId == 0;
    }
}
