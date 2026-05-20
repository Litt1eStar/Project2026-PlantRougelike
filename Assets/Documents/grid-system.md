# Grid System

The grid system manages a 2D cell grid in the XZ plane, the things that occupy
it (architecture, traps, crops, …), and the player-facing input modes used to
interact with them. This document is the reference for developers extending or
integrating with the system.

> **Source location:** `Assets/Scripts/Grid/`
> **Shader:** `Assets/Shaders/SelectionOutline.shader`
> **Render pipeline:** URP (the runtime materials use `Universal Render Pipeline/Unlit` and a custom URP outline shader)
> **Input:** legacy `UnityEngine.Input` (see *Known Limitations*)

---

## 1. Overview

The system is split into three layers:

1. **Data layer** — `GridController` owns the cell array and the occupancy
   table. It exposes `CanPlace` / `TryPlace` / `TryMove` / `Remove` operations
   and fires `OnPlaced` / `OnMoved` / `OnRemoved` events. It knows nothing about
   input or rendering.
2. **Entity layer** — `PlaceableData` is the type definition (a ScriptableObject
   with size, allowed cell types, prefab, factory). `IGridPlaceable` is a live
   instance bound to a `GameObject`. `TestPlaceableEntity` is the default
   implementation used unless a `PlaceableData` subclass overrides
   `CreateInstance`.
3. **Interaction layer** — `GridModeController` holds the active mode
   (Selection / Building / Planting). `GridPlacer` reads that mode and drives
   input → grid operations, plus the runtime visual feedback
   (`PlacementGhost`, `MarqueeVisualizer`, `SelectionHighlighter`).

```
┌──────────────────── Interaction layer ────────────────────┐
│  GridModeController ──[Mode]──▶ GridPlacer ──▶ Visuals    │
│                                    │          (Ghost,     │
│                                    │           Marquee,   │
│                                    │           Highlight) │
└──────────────────────┬─────────────┴──────────────────────┘
                       │  CanPlace / TryPlace / TryMove / Remove
                       ▼
┌──────────────────── Data layer ───────────────────────────┐
│  GridController  ◀──[events]──▶  external observers       │
│   ├─ NativeArray<GridCell>                                │
│   └─ Dictionary<int, IGridPlaceable>                      │
└──────────────────────┬────────────────────────────────────┘
                       │
                       ▼
┌──────────────────── Entity layer ─────────────────────────┐
│  PlaceableData (ScriptableObject, abstract)               │
│      └─ CreateInstance(host) ─▶ IGridPlaceable            │
│                                                           │
│  IGridPlaceable  (Data, Origin, OccupantId,               │
│                   OnPlaced/OnMoved/OnRemoved,             │
│                   SetVisualHidden)                        │
│      └─ TestPlaceableEntity : MonoBehaviour               │
└───────────────────────────────────────────────────────────┘
```

---

## 2. Core Concepts

### 2.1 Cells & coordinates

- Cells live on the XZ plane. `Y` is up.
- A cell coordinate is `Vector2Int` indexing into `[0..Width) × [0..Height)`.
- `GridController.Origin` is the world-space corner of cell `(0, 0)`.
  Cell `(x, y)` occupies the world rectangle
  `[Origin + (x, 0, y) * CellSize, Origin + ((x+1), 0, (y+1)) * CellSize]`.
- Conversion helpers:
  - `Vector3 ToWorld(Vector2Int)` — returns the cell *center*.
  - `Vector2Int FromWorld(Vector3)` — floors world XZ into a coord (may be out of bounds).
  - `Vector3 FootprintCenterWorld(origin, size)` — center of a multi-cell footprint.

### 2.2 Occupancy

`GridController` stores two pieces of state:

| Storage | Type | Purpose |
|---|---|---|
| `cells` | `NativeArray<GridCell>` | One entry per cell with `CellType` and `int occupantId`. |
| `occupants` | `Dictionary<int, IGridPlaceable>` | Id → live placeable. Id `0` means *empty*. |

A multi-cell placeable writes its `OccupantId` into every cell of its
footprint. `GetOccupant(coord)` resolves through both tables.

### 2.3 PlaceableData vs IGridPlaceable

- **`PlaceableData`** *(type definition, ScriptableObject)*

  ```csharp
  public abstract class PlaceableData : ScriptableObject {
      public string     id;
      public Vector2Int size;
      public CellType[] allowedCellTypes;
      public GameObject prefab;
      public virtual IGridPlaceable CreateInstance(GameObject host) { … }
  }
  ```

  Authored as an asset. Multiple living entities share one `PlaceableData`.
  Override `CreateInstance` when you need a custom runtime entity behavior
  (cost on place, growth ticks, AI hooks, …).

- **`IGridPlaceable`** *(live instance)*

  ```csharp
  public interface IGridPlaceable {
      PlaceableData Data       { get; }
      Vector2Int    Origin     { get; }
      int           OccupantId { get; set; }   // controller writes this; you read it
      void OnPlaced(GridController grid, Vector2Int origin);
      void OnMoved (GridController grid, Vector2Int newOrigin);
      void OnRemoved();
      void SetVisualHidden(bool hidden);
  }
  ```

  Lifecycle callbacks are issued **by `GridController`** during the
  corresponding `Try*` calls. Treat `OccupantId` as opaque — only the
  controller assigns it.

### 2.4 Interaction Modes

Defined by `GridInteractionMode`:

| Mode | Purpose | Inputs |
|---|---|---|
| `Selection` | Inspect placed items. No mutation. | LMB on entity selects; LMB on empty deselects. |
| `Building` | Place / remove / move architecture. | LMB-drag place, RMB-drag remove, double-LMB pick to move. |
| `Planting` | Place crops via a hoe equip. | Same controls as Building. Mode entered by the equipment system. |

The `Building` and `Planting` modes share input behavior; they differ in
*intent* and in what the equipment / UI layer sets `GridPlacer.currentPlaceable`
to. Future per-mode rules (validation, cost, animation) can branch on
`GridModeController.Mode`.

---

## 3. Public API

### 3.1 `GridController`

```csharp
// Geometry
int     Width  { get; }
int     Height { get; }
float   CellSize { get; }
Vector3 Origin   { get; }
Vector3 ToWorld(Vector2Int c);
Vector2Int FromWorld(Vector3 worldPos);
Vector3 FootprintCenterWorld(Vector2Int originCoord, Vector2Int size);
bool InBounds(Vector2Int c);

// Queries
GridCell        GetCell(Vector2Int c);
bool            IsOccupied(Vector2Int c);
IGridPlaceable  GetOccupant(Vector2Int c);
bool            CanPlace(PlaceableData data, Vector2Int originCoord);
bool            CanMove (IGridPlaceable item, Vector2Int newOrigin);
IReadOnlyList<Vector2Int> GetFootprint(PlaceableData data, Vector2Int originCoord);

// Mutations
bool TryPlace(IGridPlaceable item, Vector2Int originCoord);
bool TryMove (IGridPlaceable item, Vector2Int newOrigin);
bool Remove  (Vector2Int coord);

// Events  (subscribe from anywhere)
event Action<Vector2Int, IGridPlaceable>             OnPlaced;
event Action<Vector2Int, IGridPlaceable>             OnRemoved;
event Action<Vector2Int, Vector2Int, IGridPlaceable> OnMoved;   // (oldOrigin, newOrigin, item)
```

### 3.2 `GridModeController`

```csharp
GridInteractionMode Mode { get; }
event Action<GridInteractionMode> OnModeChanged;
void SetMode(GridInteractionMode mode);
```

Debug keybinds (inspector-tunable): `1`→Selection, `2`→Building, `3`→Planting.
**Equipment / UI systems should call `SetMode`**, not the keys, for production
flow.

### 3.3 `GridPlacer`

```csharp
event Action<IGridPlaceable> OnItemSelected;   // fires in Selection mode; null = cleared
```

Inspector fields are grouped under **Ghost**, **Marquee**, **Move**, **Selection
Highlight** — see file header for the exact set.

---

## 4. End-to-End Flows

### 4.1 Placement (1×1 rect drag)

1. **Building/Planting mode**, hover a cell. `GridPlacer` shows the single-cell
   ghost tinted by `CanPlace`.
2. LMB down on a valid cell → `placeDragStart = coord`. Ghost hidden;
   marquee starts.
3. Drag updates the marquee; tint is `placeValid` / `placeInvalid` based on
   `IsRectAllPlaceable(min, max)`. The rect is **all-or-nothing** — any
   collision or out-of-bounds cell makes the entire drop invalid.
4. LMB up:
   - If rect valid: for each cell, `currentPlaceable.CreateInstance(host)`
     → `controller.TryPlace(entity, cell)` → if it fails (shouldn't, given the
     pre-check) the host is destroyed.
   - If invalid: no-op.

Multi-cell `PlaceableData` (size != (1,1)) **skips drag** entirely and uses
single-click placement.

### 4.2 Move (double-LMB pickup)

1. Double-LMB on an occupied cell within `doubleClickThreshold` →
   `selectedItem = controller.GetOccupant(coord)`. The original entity's
   renderers are hidden via `SetVisualHidden(true)`. A ghost of
   `selectedItem.Data` follows the cursor.
2. Single LMB drops:
   - `controller.TryMove(item, hoveredCell)` — validation ignores the item's
     own current cells (so dropping in-place succeeds). On failure: snap back
     silently.
   - Renderers re-enabled regardless of outcome.
3. RMB during carry **cancels**, and as a side effect loads the picked item's
   `PlaceableData` into `currentPlaceable` so the user can keep placing the
   same kind.

### 4.3 Removal (RMB rect drag)

1. RMB down → `removeDragStart = coord`. Marquee starts.
2. Drag updates the marquee; orange when ≥1 occupant inside, gray when empty.
3. RMB up → `controller.Remove(c)` for every occupied cell in the rect.
   Multi-cell occupants are removed exactly once even though the iteration
   visits each of their cells (subsequent visits find the cell empty).

### 4.4 Selection (Selection mode)

1. LMB issues a `Physics.Raycast` from the camera.
2. The hit collider's `GetComponentInParent<IGridPlaceable>` is the new
   selection (or `null` if no entity hit — treat as deselect).
3. `SelectionHighlighter` spawns inverted-hull outline duplicates as children
   of each `MeshFilter` in the selected entity's hierarchy.
4. `OnItemSelected(item)` fires for any subscribers (detail UI, audio cues, …).
5. Switching out of Selection mode auto-clears the highlight.

---

## 5. Extending the System

This is the section to read if you're adding a feature. Each entry lists the
seam, the minimum-viable change, and the gotchas.

### 5.1 Add a new placeable type

**Most common task.** No code changes for the vast majority of cases.

1. Create a `ScriptableObject` subclass of `PlaceableData`. If you don't need
   custom runtime behavior, you can even reuse `TestPlaceableData` (just
   different asset configuration).
2. Author the asset: set `id`, `size`, `allowedCellTypes`, `prefab`.
3. Assign the asset to `GridPlacer.currentPlaceable` (via UI, inventory, or
   directly in the inspector for testing).

The default `PlaceableData.CreateInstance` attaches a `TestPlaceableEntity` to
the host, which handles positioning and visual spawning. That's sufficient for
inert decorations and dumb props.

### 5.2 Custom runtime behavior on a placeable

When a placeable needs more than "exist at a cell" — cost on placement, growth
ticks, AI registration, save-state, animation:

1. Write a `MonoBehaviour` that implements `IGridPlaceable`. Use
   `TestPlaceableEntity` as a template — copy its `OnPlaced`/`OnMoved`/
   `OnRemoved`/`SetVisualHidden` and add your fields/Updates around it.
2. Override `CreateInstance` on your `PlaceableData` subclass:

   ```csharp
   public override IGridPlaceable CreateInstance(GameObject host) {
       var entity = host.AddComponent<MyCustomEntity>();
       entity.Data = this;
       return entity;
   }
   ```

3. If your data type carries extra fields (cost, growth-stage prefabs, etc.),
   declare them on the subclass — `CreateInstance` can read them at spawn
   time.

**Don't** put gameplay logic on the prefab GameObject directly; the prefab is
instantiated as a *child* of the host in `OnPlaced`, so its components run
after the grid registration completes and you lose the seam.

### 5.3 React to grid changes from another system

Economy, save/load, AI awareness, statistics — anything that watches the grid.
Subscribe to the controller's events.

```csharp
controller.OnPlaced  += (origin, item) => economy.Pay(item.Data);
controller.OnRemoved += (origin, item) => save.Recordremoval(item);
controller.OnMoved   += (oldOrigin, newOrigin, item) => audio.PlayMove();
```

The events fire **after** the grid state has been updated, so `controller`
queries inside handlers reflect the post-mutation state. The `OnRemoved`
handler still has the `item` reference even though the GameObject was just
destroyed — read `item.Data` if you need information about what was there
(don't call into its MonoBehaviour members).

### 5.4 Add a new interaction mode

E.g., a `Demolish` or `Inspect` mode with specialized rules.

1. Add the case to `GridInteractionMode`.
2. Add a debug keybind in `GridModeController` (optional but useful).
3. In `GridPlacer.Update`, add an `else if (mode == GridInteractionMode.X)`
   branch dispatching to your new handler method, mirroring `UpdateSelection`.
4. If your mode needs marquee or ghost feedback, reuse `marquee.Show(...)` and
   `ghost.Show(...)` — they don't care which tool drives them.

The mode-change cancellation logic in `HandleModeChanged` already wipes any
active drag/carry, so transitions into your new mode start clean.

### 5.5 Add a new rect-based tool

E.g., paint cell type, area inspector, blueprint stamp.

Pattern:

```csharp
// In GridPlacer.Update, when your mode is active:
if (Input.GetMouseButtonDown(0) && hasCell) myDragStart = coord;
if (myDragStart.HasValue && Input.GetMouseButton(0)) {
    var (min, max) = RectBounds(myDragStart.Value, coord);
    marquee.Show(min, max, MyComputeTint(min, max));
}
if (myDragStart.HasValue && Input.GetMouseButtonUp(0)) {
    var (min, max) = RectBounds(myDragStart.Value, coord);
    MyApplyToRect(min, max);
    myDragStart = null;
    marquee.Hide();
}
```

**Important:** keep press-hold-release as three top-level `if` branches.
`Input.GetMouseButton` returns `false` on the frame of release, so nesting the
`Up` check inside the `Button` (held) check makes it never fire — this bit us
during early development.

### 5.6 Drive a detail UI panel from selection

```csharp
// Wire once at startup.
gridPlacer.OnItemSelected += panel.Show;   // panel.Show(IGridPlaceable item)
```

The panel receives the live entity. Read `item.Data` for the type definition
(name, icon, description), or downcast to a known subclass for richer fields.
When `item == null`, hide the panel.

### 5.7 Customize the selection highlight

Today, `SelectionHighlighter` spawns child mesh duplicates with the URP outline
shader. If you want a different effect (glow, color tint, hologram), you have
two paths:

- **Easy:** edit `Assets/Shaders/SelectionOutline.shader` — for example, output
  emissive color with a postprocess bloom layer enabled in your URP renderer.
- **Bigger change:** replace `SelectionHighlighter.Spawn` with whatever
  feedback you want (material swap with cached restore, Renderer Feature
  toggle, particle effect spawn, …). The `Set(item)` / `Clear()` contract is
  what `GridPlacer` cares about.

### 5.8 Save / load

There's no persistence layer yet. The recommended approach when adding one:

1. Listen to `OnPlaced` / `OnMoved` / `OnRemoved` and accumulate the world
   state. Or, walk the controller via a yet-to-be-added enumerator (today,
   `occupants` is private; expose it via an iterator method when you need it).
2. To restore: for each saved `(PlaceableData id, origin)` tuple, look up the
   `PlaceableData` asset by id, then `data.CreateInstance(new GameObject(id))`
   → `controller.TryPlace(entity, origin)`.

If you find yourself reaching into private `GridController` fields, prefer
adding a public read-only API instead — that's the seam for save/load.

---

## 6. File Reference

| File | Role |
|---|---|
| `GridCell.cs` | `struct GridCell { CellType type; int occupantId; }` — one entry per grid square. |
| `CellType.cs` | Enum of allowed terrain types (e.g., Grass, Soil). |
| `GridController.cs` | Owns the grid array & occupants; queries & mutations; events. |
| `GridVisualizer.cs` | Optional grid-line gizmo / runtime mesh for the cell grid. |
| `PlaceableData.cs` | Abstract ScriptableObject with the `CreateInstance` factory hook. |
| `TestPlaceableData.cs` | Concrete default — a Test Placeable asset menu item. |
| `IGridPlaceable.cs` | Interface implemented by every live placeable. |
| `TestPlaceableEntity.cs` | Default `IGridPlaceable` MonoBehaviour. Reference implementation. |
| `GridInteractionMode.cs` | `enum { Selection, Building, Planting }`. |
| `GridModeController.cs` | Owns current mode + debug keybinds + `OnModeChanged`. |
| `GridPlacer.cs` | Input orchestrator. Reads mode, dispatches to per-mode handlers. |
| `PlacementGhost.cs` | Single-cell hover preview. Lazy-builds material + prefab instance. |
| `MarqueeVisualizer.cs` | Translucent rectangle quad for drag selection. |
| `SelectionHighlighter.cs` | Spawns outline mesh duplicates for the selected entity. |
| `TransparentURPMaterial.cs` | Internal factory for the URP/Unlit transparent material shared by ghost & marquee. |
| `Assets/Shaders/SelectionOutline.shader` | URP inverted-hull outline shader. |

---

## 7. Scene Setup Checklist

For a fresh scene that needs the grid system:

1. Create an empty GameObject (e.g., `Grid`).
2. Add `GridController` — set Width, Height, CellSize, and optionally an
   `Origin Transform`.
3. Add `GridModeController` — set initial mode (usually `Selection`).
4. Add `GridPlacer` — assign `Aim Camera` (or leave null for `Camera.main`).
   Assign a `PlaceableData` to `Current Placeable` for testing.
5. (Optional) Add `GridVisualizer` for debug grid lines.
6. Ensure your placeable prefabs have **Colliders** on the visual meshes —
   `Physics.Raycast` is used for Selection-mode picking.

---

## 8. Manual Test Checklist

When changing the system, exercise each mode end-to-end:

**Selection mode (key `1`)**
- [ ] LMB on a placed entity → console logs the selection, outline appears.
- [ ] LMB on a different entity → outline migrates.
- [ ] LMB on empty ground → outline clears.
- [ ] RMB does nothing.

**Building mode (key `2`)**
- [ ] LMB-drag a rect over empty cells → blue tint, places on release.
- [ ] LMB-drag a rect overlapping an existing entity → red tint, places nothing.
- [ ] RMB-drag a rect over entities → orange tint, removes on release.
- [ ] RMB-drag over empty cells → gray tint, no-op.
- [ ] Double-LMB on entity → it disappears visually, ghost follows cursor.
- [ ] LMB drop on valid cell → entity reappears at new cell.
- [ ] LMB drop on invalid cell → entity snaps back.
- [ ] RMB during carry → cancel, `currentPlaceable` becomes the carried item's data.

**Planting mode (key `3`)**
- [ ] Same controls as Building. Will diverge when crop-specific logic lands.

**Cross-cutting**
- [ ] Switching mode mid-drag or mid-carry cancels cleanly with no dangling
      ghost / marquee / hidden renderers.

---

## 9. Known Limitations

- **Input.** Legacy `UnityEngine.Input` API. The Editor warns this is marked
  for deprecation. Migrating to the new Input System requires touching
  `GridPlacer` and `GridModeController`.
- **Render pipeline.** Hard-coded URP shaders. Switching to Built-in or HDRP
  needs `TransparentURPMaterial`, `SelectionOutline.shader`, and the
  `PlacementGhost` material assignment to be ported.
- **Selection requires colliders.** Entities without colliders on their
  visuals can't be picked in Selection mode.
- **Skinned meshes don't outline.** `SelectionHighlighter` only walks
  `MeshFilter` components. Add a `SkinnedMeshRenderer` branch when needed.
- **No multi-cell rect placement.** Drag rect place is gated to 1×1 placeables.
  Larger sizes use single-click. Tiling >1×1 across a rect is not implemented.
- **`Camera.main` fallback.** Multi-camera scenes must assign `Aim Camera`
  explicitly.
- **No save/load.** State is in-memory only.
- **No cell-type editor.** Every cell defaults to `CellType.Grass`. There's no
  authoring tool yet for varied terrain.
