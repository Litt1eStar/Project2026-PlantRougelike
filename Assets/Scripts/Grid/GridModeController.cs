using System;
using UnityEngine;

namespace PlantRoguelike.Grid
{
    // Single source of truth for the current grid-interaction mode. UI buttons,
    // gameplay code, and the future inventory/equipment system call SetMode;
    // GridPlacer (and other consumers) read Mode and subscribe to OnModeChanged.
    //
    // Mode triggers from outside this module:
    //  - Selection / Building come from UI buttons or the debug keybinds below.
    //  - Planting is entered when the equipment system equips a hoe and exited
    //    when the hoe is unequipped. The equipment system owns that decision;
    //    the grid module just exposes SetMode for it to call.
    public sealed class GridModeController : MonoBehaviour
    {
        [SerializeField] private GridInteractionMode initialMode = GridInteractionMode.Selection;

        [Header("Keybinds (debug)")]
        [SerializeField] private KeyCode selectionKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode buildingKey  = KeyCode.Alpha2;
        [SerializeField] private KeyCode plantingKey  = KeyCode.Alpha3;

        public GridInteractionMode Mode { get; private set; }

        public event Action<GridInteractionMode> OnModeChanged;

        private void Awake() => Mode = initialMode;

        private void Update()
        {
            if      (Input.GetKeyDown(selectionKey)) SetMode(GridInteractionMode.Selection);
            else if (Input.GetKeyDown(buildingKey))  SetMode(GridInteractionMode.Building);
            else if (Input.GetKeyDown(plantingKey))  SetMode(GridInteractionMode.Planting);
        }

        public void SetMode(GridInteractionMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            OnModeChanged?.Invoke(Mode);
        }
    }
}
