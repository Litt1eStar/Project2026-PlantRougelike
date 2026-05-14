# Camera Follow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a smooth-follow camera that tracks the player on the X and Z axes while keeping a fixed height.

**Architecture:** A single `CameraFollow` MonoBehaviour on the Main Camera uses `Vector3.Lerp` in `LateUpdate` to smoothly interpolate toward the player's position each frame, locking the Y axis to the camera's initial height.

**Tech Stack:** Unity 6, C#

---

### Task 1: Create the CameraFollow script

**Files:**
- Create: `Assets/Scripts/CameraFollow.cs`

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/CameraFollow.cs` with this content:

```csharp
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 5f;

    private float fixedY;

    private void Awake()
    {
        fixedY = transform.position.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(target.position.x, fixedY, target.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
```

- [ ] **Step 2: Attach script to Main Camera in the scene**

Use the Unity skill `gameobject-component-add` to add `CameraFollow` to the Main Camera GameObject.

- [ ] **Step 3: Set the Player reference on the component**

Use `gameobject-component-modify` to set `target` to the Player Transform reference.

- [ ] **Step 4: Verify in Play mode**

Enter Play mode and confirm the camera smoothly follows the player on X/Z while staying at its original height.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CameraFollow.cs Assets/Scripts/CameraFollow.cs.meta
git commit -m "feat: add smooth camera follow script for player"
```
