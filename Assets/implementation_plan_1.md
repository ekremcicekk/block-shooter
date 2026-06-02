# Procedural Bevel System — Implementation Specification

> [!IMPORTANT]
> This document is the **complete implementation blueprint**. No code should be written until this spec is reviewed and approved.

---

## 1. Function Inventory

### 1.1 Functions That Remain UNCHANGED

| # | Function | Lines | Reason No Change Needed |
|---|----------|-------|------------------------|
| 1 | `AddTop` | L153-163 | Generic rectangle emitter. Callers will pass inset coordinates — the function itself is agnostic. |
| 2 | `AddWallX` | L165-172 | Generic wall emitter. Callers will pass `yT - bW` as `yTop` — the function itself is agnostic. |
| 3 | `AddWallZ` | L174-181 | Same as AddWallX. |
| 4 | `AddQuad` | L236-249 | Lowest-level quad primitive. Completely generic. |
| 5 | `AddCornerFan` | L188-210 | XZ corner fan on top surface. Callers will pass adjusted `R` values — function itself unchanged. |
| 6 | `AddCurvedWall` | L216-234 | XZ curved wall extrusion. Callers will pass `yT - bW` — function itself unchanged. |
| 7 | `Awake` | L22 | No change. |

> [!TIP]
> The entire existing helper function library (6 functions) remains untouched. All bevel logic is achieved by (a) modifying the **calling code** inside `BuildMesh`, and (b) adding **new helper functions** for bevel strips and corner joins.

### 1.2 Functions That Must Be MODIFIED

| # | Function | Lines | What Changes |
|---|----------|-------|-------------|
| 1 | `BuildMesh` | L24-148 | Major restructuring. See Section 2 for complete pipeline. |

**Detailed changes to `BuildMesh`:**

1. **New fields read**: Read `wallBevelWidth` and `wallBevelSegments` at method start, compute clamped values `bW` and `bSeg`.

2. **Phase 0 added**: Pre-compute a per-cell wall map **before** any geometry emission. Currently absent.

3. **Phase 1 rewritten** (L54-68): Currently emits full-size `AddTop` + `AddWallX/Z` per cell. Replaced with:
   - Inset `AddTop` (coordinates reduced by `bW` on wall sides)
   - `AddWallX/Z` with `yT - bW` instead of `yT`
   - `AddBevelStripX/Z` calls for each wall
   - `AddBevelCorner` calls at cell corners

4. **Phase 2 modified** (L70-86): Wing top rectangles get inset coordinates on edges adjacent to inner boundary walls. Currently uses fixed `cx[0]` / `cx[gridCols]` boundaries — these shift by `bW` where filled cells create inner walls.

5. **Phase 3 modified** (L88-96): Outer wall calls change `yT` → `yT - bW`. New `AddBevelStripX/Z` and `AddBevelStripCurved` calls added for each outer wall.

6. **Phase 4 modified** (L98-137): Inner boundary wall calls change `yT` → `yT - bW`. New bevel strip calls added. cornerBL/cornerBR curves and fans get adjusted parameters.

7. **Phase 5 added**: Dedicated corner join pass for outer/inner boundary corners.

### 1.3 Functions That Must Be ADDED

| # | Function | Purpose |
|---|----------|---------|
| 1 | `AddBevelStripX` | Bevel strip along a wall perpendicular to the X axis (wall at constant x, spanning z) |
| 2 | `AddBevelStripZ` | Bevel strip along a wall perpendicular to the Z axis (wall at constant z, spanning x) |
| 3 | `AddBevelStripCurved` | Bevel strip along a curved wall (follows XZ arc, quarter-circle Y-profile at each arc sample) |
| 4 | `AddBevelCornerConvex` | Convex corner join — cone fan connecting two perpendicular bevel strips |
| 5 | `AddBevelCornerConcave` | Concave corner join — single triangle filling the inner corner where two walls meet at a re-entrant angle |

### 1.4 New Fields

Added to [ShooterDeckMeshBuilder](file:///c:/Users/ekrm_/OneDrive/Belgeler/GitHub/DodoGames/block-shooter/Assets/Project%20Files/Game/Scripts/Grid/ShooterDeckMeshBuilder.cs) class body (after L19):

```
[Tooltip("Cross-section bevel width on wall-top edges. 0 = sharp (current behavior).")]
public float wallBevelWidth    = 0.06f;
[Tooltip("1 = flat chamfer. 3-6 = smooth round.")]
public int   wallBevelSegments = 3;
```

Added to [LevelEditorConfig](file:///c:/Users/ekrm_/OneDrive/Belgeler/GitHub/DodoGames/block-shooter/Assets/Project%20Files/Game/Scripts/Data/LevelEditorConfig.cs) under `[Header("Shooter Deck")]` (after L60):

```
[Tooltip("Cross-section bevel width on wall-top edges. 0 = sharp.")]
public float deckWallBevelWidth    = 0.06f;
[Tooltip("1 = flat chamfer. 3-6 = smooth round.")]
public int   deckWallBevelSegments = 3;
```

Two lines added to [LevelEditorWindow.BuildHierarchy](file:///c:/Users/ekrm_/OneDrive/Belgeler/GitHub/DodoGames/block-shooter/Assets/Project%20Files/Game/Scripts/LevelEditor/LevelEditorWindow.cs#L1739-L1740) (after L1740):

```
deckBuilder.wallBevelWidth    = _cfg.deckWallBevelWidth;
deckBuilder.wallBevelSegments = _cfg.deckWallBevelSegments;
```

---

## 2. Complete Geometry Pipeline

```
Step 0 ─── Clamp & Precompute
Step 1 ─── Cell Geometry  (empty cells: inset tops + beveled walls + cell corners)
Step 2 ─── Wing Tops      (left/right/back wings with boundary-aware insets)
Step 3 ─── Outer Walls    (beveled outer straight walls)
Step 4 ─── Outer Corners  (beveled outer curved walls + outer corner joins)
Step 5 ─── Inner Boundary (beveled inner boundary walls + inner corner joins)
Step 6 ─── Assemble Mesh  (combine verts/tris, RecalculateNormals, assign to MeshFilter)
```

### Step 0: Clamp & Precompute

**Input**: `bool[,] isEmpty`, all public fields.

**Operations**:
```
R    = Clamp(bevelSize, 0, Min(sideWingWidth, backDepth) * 0.9)      // existing
S    = Max(1, bevelSegments)                                          // existing
bW   = Clamp(wallBevelWidth, 0, cellSize * 0.25)                     // NEW
bSeg = Max(1, wallBevelSegments)                                      // NEW

cx[] = cell edge X coordinates (unchanged)
cz[] = cell edge Z coordinates (unchanged)

// NEW: Per-cell wall map
For each empty cell (c, r):
    wL[c,r] = (c > 0)            && !E(c-1, r)    // wall on left
    wR[c,r] = (c < gridCols-1)   && !E(c+1, r)    // wall on right
    wB[c,r] = (r > 0)            && !E(c, r-1)    // wall on back
    wF[c,r] = (r < gridRows-1)   && !E(c, r+1)    // wall on front

// NEW: Inner boundary wall map (for wings)
For r = 0..gridRows-1:
    ibL[r] = !E(0, r)              // inner boundary wall on left grid edge
    ibR[r] = !E(gridCols-1, r)     // inner boundary wall on right grid edge
For c = 0..gridCols-1:
    ibB[c] = !E(c, 0)              // inner boundary wall on back grid edge
```

### Step 1: Cell Geometry

For each empty cell (c, r):

**1a. Inset Top Face**
```
x0 = cx[c]   + (wL[c,r] ? bW : 0)
x1 = cx[c+1] - (wR[c,r] ? bW : 0)
z0 = cz[r]   + (wB[c,r] ? bW : 0)
z1 = cz[r+1] - (wF[c,r] ? bW : 0)
AddTop(verts, uvs, trisTop, x0, x1, z0, z1, yT)
```

**1b. Walls + Bevel Strips**
```
if wL[c,r]:
    AddWallX(verts, uvs, trisWall, cx[c], cz[r], cz[r+1], yT - bW, yB, false)
    AddBevelStripX(verts, uvs, trisWall, cx[c], cz[r], cz[r+1], yT, bW, bSeg, false)

if wR[c,r]:
    AddWallX(verts, uvs, trisWall, cx[c+1], cz[r], cz[r+1], yT - bW, yB, true)
    AddBevelStripX(verts, uvs, trisWall, cx[c+1], cz[r], cz[r+1], yT, bW, bSeg, true)

if wB[c,r]:
    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r], yT - bW, yB, false)
    AddBevelStripZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r], yT, bW, bSeg, false)

if wF[c,r]:
    AddWallZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r+1], yT - bW, yB, true)
    AddBevelStripZ(verts, uvs, trisWall, cx[c], cx[c+1], cz[r+1], yT, bW, bSeg, true)
```

**1c. Corner Joins (4 corners per cell)**

For each of the 4 corners of cell (c, r), evaluate corner type and emit appropriate geometry. See Section 8 for full detection logic.

### Step 2: Wing Tops

The wing top surfaces need insets where inner boundary walls exist. The wings are split into **segments** along the grid edge.

**Left wing right edge** (at `x = cx[0]`):

Currently 3 rectangles + 2 fans. The right edge of these rectangles (`x1 = cx[0]`) shifts to `cx[0] + bW` in Z-ranges where `ibL[r]` is true (filled cell creates inner boundary wall).

Since the wing rectangles span the full Z range, they must be **split per row**:

```
For r = 0..gridRows-1:
    float z0_row = cz[r]
    float z1_row = cz[r+1]
    float rightEdge = ibL[r] ? cx[0] + bW : cx[0]
    // Clip z0_row/z1_row to avoid overlap with corner arcs (cornerBL)
    AddTop(verts, uvs, trisTop, xL_adjusted, rightEdge, z0_row, z1_row, yT)
```

The wing strips outside the grid Z-range (`zBack` to `cz[0]` and `cz[gridRows]` to `zFront`) keep their current form with outer wall bevel insets.

> [!NOTE]
> The wing segmentation only affects the grid-facing edge. The outer edge (at `xL`, `xR`) is already handled by outer wall bevel insets. The back wing behaves similarly along its front edge at `cz[0]`.

### Step 3: Outer Straight Walls

Each outer straight wall changes `yT` → `yT - bW` and gets a bevel strip:

```
// Left outer wall
AddWallX(verts, uvs, trisWall, xL, zBack+R, zFront-R, yT - bW, yB, false)
AddBevelStripX(verts, uvs, trisWall, xL, zBack+R, zFront-R, yT, bW, bSeg, false)

// Right outer wall
AddWallX(verts, uvs, trisWall, xR, zBack+R, zFront-R, yT - bW, yB, true)
AddBevelStripX(verts, uvs, trisWall, xR, zBack+R, zFront-R, yT, bW, bSeg, true)

// Back outer wall
AddWallZ(verts, uvs, trisWall, xL+R, xR-R, zBack, yT - bW, yB, false)
AddBevelStripZ(verts, uvs, trisWall, xL+R, xR-R, zBack, yT, bW, bSeg, false)
```

### Step 4: Outer Curved Walls + Corner Joins

Each outer curved wall changes `yT` → `yT - bW` and gets a curved bevel strip:

```
// Example: back-left corner
AddCurvedWall(verts, uvs, trisWall, xL+R, zBack+R, R, S, 180f, yT - bW, yB)
AddBevelStripCurved(verts, uvs, trisWall, xL+R, zBack+R, R, S, 180f, yT, bW, bSeg)

// The corner fan uses inset radius R - bW:
AddCornerFan(verts, uvs, trisTop, xL+R, zBack+R, R - bW, S, 180f, yT)
```

The gap between the inset fan (radius `R - bW`) and the bevel ring (at radius `R`) is already filled by the `AddBevelStripCurved` function.

Repeated for all 4 outer corners (back-left, back-right, front-left, front-right).

### Step 5: Inner Boundary Walls

Same pattern as current Phase 4 but with `yT - bW` and bevel strips:

```
For r = 0..gridRows-1:
    if ibL[r]:
        float z0 = (r == 0 && cornerBL) ? cz[0] + R : cz[r]
        AddWallX(verts, uvs, trisWall, cx[0], z0, cz[r+1], yT - bW, yB, true)
        AddBevelStripX(verts, uvs, trisWall, cx[0], z0, cz[r+1], yT, bW, bSeg, true)
    // same for ibR[r]

For c = 0..gridCols-1:
    if ibB[c]:
        float x0 = (c == 0 && cornerBL) ? cx[0] + R : cx[c]
        float x1 = (c == gridCols-1 && cornerBR) ? cx[gridCols] - R : cx[c+1]
        AddWallZ(verts, uvs, trisWall, x0, x1, cz[0], yT - bW, yB, false)
        AddBevelStripZ(verts, uvs, trisWall, x0, x1, cz[0], yT, bW, bSeg, false)

// cornerBL/cornerBR curves + fans:
if cornerBL:
    AddCurvedWall(verts, uvs, trisWall, cx[0]+R, cz[0]+R, R, S, 180f, yT - bW, yB)
    AddBevelStripCurved(verts, uvs, trisWall, cx[0]+R, cz[0]+R, R, S, 180f, yT, bW, bSeg)
    AddCornerFan(verts, uvs, trisTop, cx[0]+R, cz[0]+R, R - bW, S, 180f, yT)
```

### Step 6: Assemble Mesh

Identical to current L139-148. No changes.

---

## 3. Bevel Generation Process

### 3.1 Wall Generation (Modified Calls)

Every existing `AddWallX` / `AddWallZ` / `AddCurvedWall` call changes its `yTop` argument from `yT` to `yT - bW`. The wall body is **shortened** — it no longer reaches the top surface. The gap is filled by the bevel strip.

When `bW == 0`: `yT - bW == yT` → identical to current behavior.

### 3.2 Bevel Strip Generation

#### `AddBevelStripX` — Wall perpendicular to X axis

**Signature**: `(v, u, t, float x, float z0, float z1, float yT, float bW, int bSeg, bool normalRight)`

**Geometry**: A band of `bSeg` quads spanning from `z0` to `z1`, curving from the inset top surface down to the wall top.

**Cross-section profile** (looking along Z, for `normalRight = true` → top surface is to LEFT, wall is to RIGHT):

```
For k = 0..bSeg:
    angle = (π/2) × k / bSeg
    dx = bW × cos(angle)          // horizontal offset from wall plane (toward top surface)
    dy = bW × sin(angle)          // vertical offset from wall top (toward yT)
    
    point_x = x - dx              // move LEFT from wall (toward top surface)
    point_y = yT - bW + dy        // start at wall top (yT-bW), arc UP toward yT
```

At `k = 0`: `dx = bW, dy = 0` → point at `(x - bW, yT - bW)` = wall top + bW inset horizontally. **Wait** — this should be `(x - bW, yT)` at the top surface edge.

Let me reclarify the profile formula. The arc goes from the **top surface edge** (inner) to the **wall top** (outer):

```
For k = 0..bSeg:
    angle = (π/2) × k / bSeg
    
    // At k=0: on the inset top surface edge (x - bW, yT)
    // At k=bSeg: on the wall top (x, yT - bW)
    
    point_x = x - bW × cos(angle)
    point_y = yT - bW × (1 - sin(angle))
            = yT - bW + bW × sin(angle)
```

Verification:
- `k = 0`: `angle = 0` → `x_k = x - bW`, `y_k = yT - bW + 0 = yT - bW`

That's wrong — at k=0 we want `(x - bW, yT)` not `(x - bW, yT - bW)`.

Correct formula:
```
For k = 0..bSeg:
    angle = (π/2) × k / bSeg
    
    point_x = x - bW × cos(angle)      // at k=0: x - bW (top edge), at k=bSeg: x (wall)
    point_y = yT - bW × sin(angle)     // at k=0: yT (top level), at k=bSeg: yT - bW (wall top)
```

Verification:
- `k = 0`: `angle = 0`, `cos = 1, sin = 0` → `(x - bW, yT)` ✅ top surface edge
- `k = bSeg`: `angle = π/2`, `cos = 0, sin = 1` → `(x, yT - bW)` ✅ wall top

For `normalRight = false` → top surface is to RIGHT, wall is to LEFT:
```
    point_x = x + bW × cos(angle)      // mirror: move RIGHT toward top surface
    point_y = yT - bW × sin(angle)     // same Y profile
```

**Vertex layout**: Two rows of `(bSeg + 1)` points, at `z0` and `z1`, forming `bSeg` quads:

```
Row at z0: P0_z0, P1_z0, ..., PbSeg_z0
Row at z1: P0_z1, P1_z1, ..., PbSeg_z1

For each k = 0..bSeg-1:
    Quad: (Pk_z0, Pk_z1, Pk+1_z1, Pk+1_z0)
```

**Total per call**: `2 × (bSeg + 1)` vertices, `bSeg × 2` triangles.

#### `AddBevelStripZ` — Wall perpendicular to Z axis

Mirror of `AddBevelStripX`. Profile curves in the Z direction instead of X.

**Signature**: `(v, u, t, float x0, float x1, float z, float yT, float bW, int bSeg, bool normalFwd)`

```
For k = 0..bSeg:
    angle = (π/2) × k / bSeg
    
    if normalFwd:  // top surface is BEHIND (z < wall_z), wall faces forward
        point_z = z - bW × cos(angle)
    else:          // top surface is IN FRONT (z > wall_z), wall faces backward
        point_z = z + bW × cos(angle)
    
    point_y = yT - bW × sin(angle)
```

Two rows at `x0` and `x1`. Same vertex/triangle count as `AddBevelStripX`.

#### `AddBevelStripCurved` — Wall following XZ arc

**Signature**: `(v, u, t, float arcCx, float arcCz, float R, int arcSegs, float startDeg, float yT, float bW, int bSeg)`

Generates a grid of `(arcSegs + 1) × (bSeg + 1)` vertices forming a band that follows the XZ arc and curves in the Y-radial cross-section.

```
For arcK = 0..arcSegs:
    arcAngle = (startDeg + arcK × 90 / arcSegs) × Deg2Rad
    
    // Unit vector from arc center to wall surface (outward)
    outX = cos(arcAngle)
    outZ = sin(arcAngle)
    
    For bevK = 0..bSeg:
        bevAngle = (π/2) × bevK / bSeg
        
        // Radial offset: from R-bW (inner/top) to R (outer/wall)
        radialOffset = R - bW × cos(bevAngle)
        
        point_x = arcCx + outX × radialOffset
        point_z = arcCz + outZ × radialOffset
        point_y = yT - bW × sin(bevAngle)
```

Verification at bevK=0: `radialOffset = R - bW` → on the inset top surface at `yT` ✅
Verification at bevK=bSeg: `radialOffset = R` → on the wall at `yT - bW` ✅

**Triangulation**: For each `(arcK, bevK)` cell, emit 2 triangles forming a quad.

**Total**: `(arcSegs + 1) × (bSeg + 1)` vertices, `arcSegs × bSeg × 2` triangles.

### 3.3 Corner Join Generation

#### `AddBevelCornerConvex` — Outside corner

**Signature**: `(v, u, t, float cornerX, float cornerZ, float bW, int bSeg, float startDeg)`

Where `startDeg` determines the corner orientation (0°, 90°, 180°, 270° matching the quadrant).

**Geometry**: A cone-like fan from the apex `(cornerX, cornerZ, yT - bW)` to a quarter-circle arc at `yT`.

```
// Apex: the point where two wall tops meet
apex = (cornerX, yT - bW, cornerZ)

// Arc at top surface level — quarter circle of radius bW
For k = 0..bSeg:
    angle = (startDeg + k × 90 / bSeg) × Deg2Rad
    arcPoint_x = cornerX + bW × cos(angle)
    arcPoint_z = cornerZ + bW × sin(angle)
    arcPoint_y = yT

// Triangle fan: apex → arc[k+1] → arc[k] for correct winding
```

**Total**: `bSeg + 2` vertices (1 apex + bSeg+1 arc points), `bSeg` triangles.

> [!NOTE]
> The `startDeg` for each corner quadrant is determined by which two walls meet. For the **top-right** corner of a cell where both right wall (facing +X) and front wall (facing +Z) exist, the bevel arc sweeps in the (+X, +Z) quadrant → `startDeg = 0°`.

**Corner orientation mapping for cell (c, r):**

| Cell Corner | Walls Present | Corner Position | startDeg |
|-------------|--------------|----------------|----------|
| Bottom-Left | wL + wB | `(cx[c], cz[r])` | 180° |
| Bottom-Right | wR + wB | `(cx[c+1], cz[r])` | 270° |
| Top-Left | wL + wF | `(cx[c], cz[r+1])` | 90° |
| Top-Right | wR + wF | `(cx[c+1], cz[r+1])` | 0° |

#### `AddBevelCornerConcave` — Inside corner

**Signature**: `(v, u, t, float cornerX, float cornerZ, float bW, float yT, float startDeg)`

**When emitted**: When `!sideA && !sideB && diagonal_is_filled` at a grid vertex. This happens when two empty cells share a corner but the diagonal cell is filled, creating a re-entrant (concave) angle.

**Geometry**: A single triangle that fills the notch:

```
// Three points:
P0 = (cornerX,                  yT,      cornerZ)              // grid vertex at top level
P1 = (cornerX + bW × cos(a0),   yT,      cornerZ + bW × sin(a0))  // end of bevel strip A
P2 = (cornerX + bW × cos(a1),   yT,      cornerZ + bW × sin(a1))  // end of bevel strip B
// where a0, a1 are the angles of the two wall directions

// Single triangle: P0 → P1 → P2 (or reversed for correct winding)
```

**Ownership rule**: Only the cell with `min(c, r)` emits this triangle to prevent duplication. Specifically: at grid vertex `(cx[c], cz[r])`, the concave fill is owned by the empty cell with the **smallest (col, row)** pair among the empty cells adjacent to this vertex.

**Total**: 3 vertices, 1 triangle.

---

## 4. Visible Wall Edge Detection

All wall-top edges are **explicitly known** from the construction logic — no detection/scanning is needed.

**Inter-cell walls** (Step 1): The existing conditions directly identify edges:
```
wL[c,r] = (c > 0)          && !E(c-1, r)   → wall at x = cx[c],   z: cz[r]..cz[r+1]
wR[c,r] = (c < gridCols-1) && !E(c+1, r)   → wall at x = cx[c+1], z: cz[r]..cz[r+1]
wB[c,r] = (r > 0)          && !E(c, r-1)   → wall at z = cz[r],   x: cx[c]..cx[c+1]
wF[c,r] = (r < gridRows-1) && !E(c, r+1)   → wall at z = cz[r+1], x: cx[c]..cx[c+1]
```

**Outer walls** (Step 3): Always present — 3 straight walls (left, right, back) + 4 curved corners. Unconditionally beveled.

**Inner boundary walls** (Step 5): `ibL[r] = !E(0, r)`, `ibR[r] = !E(gridCols-1, r)`, `ibB[c] = !E(c, 0)`. Beveled when present.

**Front edge**: No wall exists along the front (`z = zFront` between the two front corner curves). The wing tops and cell tops simply end here. **No bevel on the front edge.** This is correct — the front faces the player/slot area and the wing corner curves already handle the front-left and front-right transitions.

---

## 5. Corner Join Detection

### 5.1 Inter-Cell Corners (Step 1c)

At each of the 4 corners of empty cell (c, r), evaluate:

**Top-Right corner** at `(cx[c+1], cz[r+1])`:
```
sideA = wR[c,r]                                        // right wall exists
sideB = wF[c,r]                                        // front wall exists
diag  = (c < gridCols-1 && r < gridRows-1) && !E(c+1, r+1)  // diagonal filled
```

**Decision matrix:**

| sideA | sideB | diag | Action |
|-------|-------|------|--------|
| ✓ | ✓ | * | `AddBevelCornerConvex(cx[c+1], cz[r+1], bW, bSeg, 0°)` |
| ✓ | ✗ | * | No corner piece. Bevel strip runs full length to `cz[r+1]`. |
| ✗ | ✓ | * | No corner piece. Bevel strip runs full length to `cx[c+1]`. |
| ✗ | ✗ | ✓ | `AddBevelCornerConcave(cx[c+1], cz[r+1], bW, yT, ...)` — **only if this cell owns it** |
| ✗ | ✗ | ✗ | Nothing. |

**Analogous matrices** apply for the other 3 corners (Top-Left, Bottom-Right, Bottom-Left) with appropriate `startDeg` values and axis reflections.

### 5.2 Outer/Inner Boundary Corners (Steps 4-5)

These are the existing `cornerBL` / `cornerBR` cases plus the 4 outer wing corners. Each gets:
- `AddBevelStripCurved` (bevel ring following the XZ arc)
- Inset `AddCornerFan` (radius `R - bW`)
- The gap between inset fan and bevel ring is naturally filled by the `AddBevelStripCurved` geometry.

No separate corner join pieces are needed for these — the curved bevel strip inherently handles the smooth transition.

---

## 6. Bevel Width Application

### 6.1 Clamping

```
bW = Clamp(wallBevelWidth, 0, cellSize × 0.25)
```

**Rationale**: A cell with walls on opposite sides loses `2 × bW` from its top face. At `bW = cellSize × 0.25`, the remaining top is `cellSize × 0.5` — the minimum viable visible surface. The `tileHeight` is NOT a clamping factor because `bW` and `tileHeight` are orthogonal (bW is horizontal inset, tileHeight is total wall height).

Additionally: `bW = Clamp(bW, 0, tileHeight)`. The bevel Y-drop cannot exceed the wall height — otherwise the bevel would extend below `yB`.

Combined: `bW = Clamp(wallBevelWidth, 0, Min(cellSize × 0.25, tileHeight))`

### 6.2 Effect on Coordinates

| Element | Coordinate Change |
|---------|------------------|
| Cell top face | Each edge with a wall is inset by `bW` |
| Wall yTop | `yT` → `yT - bW` |
| Wing top face edge | Inset by `bW` where inner boundary wall exists |
| Outer corner fan radius | `R` → `R - bW` |
| Inner boundary corner fan radius | `R` → `R - bW` |

### 6.3 Backward Compatibility

When `bW = 0`:
- All insets are zero → top faces use original coordinates
- Wall `yTop = yT - 0 = yT` → original wall height
- Bevel strip functions emit zero-width strips (skipped via early return)
- Corner join functions emit zero-size corners (skipped via early return)
- **Output is bit-identical to current code**

---

## 7. Bevel Segments Application

### 7.1 Clamping

```
bSeg = Clamp(wallBevelSegments, 1, 8)
```

Upper limit of 8 prevents excessive subdivision for negligible visual gain on mobile.

### 7.2 Arc Sampling

The bevel cross-section is a quarter-circle arc sampled at `bSeg + 1` points:

```
For k = 0..bSeg:
    angle_k = (π/2) × k / bSeg
    cos_k = cos(angle_k)
    sin_k = sin(angle_k)
```

| bSeg | Points | Quads per wall segment | Visual |
|------|--------|----------------------|--------|
| 1 | 2 | 1 | Flat 45° chamfer |
| 2 | 3 | 2 | Two-facet curve |
| 3 | 4 | 3 | Smooth curve (recommended) |
| 4 | 5 | 4 | Smoother curve |
| 8 | 9 | 8 | Very smooth (overkill for mobile) |

### 7.3 Precomputed Lookup

Since `cos(angle_k)` and `sin(angle_k)` are used for every wall, precompute them once at the start of `BuildMesh`:

```
float[] bevCos = new float[bSeg + 1];
float[] bevSin = new float[bSeg + 1];
for (int k = 0; k <= bSeg; k++) {
    float a = (Mathf.PI * 0.5f) * k / bSeg;
    bevCos[k] = Mathf.Cos(a);
    bevSin[k] = Mathf.Sin(a);
}
```

Passed to all `AddBevelStrip*` functions as arrays to avoid redundant trig.

---

## 8. UV Generation

### 8.1 Top Faces (unchanged)

World-space `(x, z)` — existing behavior, no change:
```
u.Add(new Vector2(x, z));
```

### 8.2 Wall Faces (unchanged)

Normalized `(0,0)` → `(1,1)` per quad — existing behavior via `AddQuad`, no change.

### 8.3 Bevel Strips

**Strategy**: World-space UVs matching the top surface system. The bevel is visually a continuation of the top surface curving away from camera — seamless UV transition.

For `AddBevelStripX` at wall `x`, spanning `z0..z1`:
```
For each point (px, py, pz) on the bevel arc:
    u.Add(new Vector2(px, pz));
```

This naturally continues the top face UV pattern into the bevel. Since `bW` is small (0.06 units), the UV distortion from the curved surface is negligible.

### 8.4 Corner Joins

**Convex**: World-space `(px, pz)` for all points — matches adjacent bevel strip UVs.

**Concave**: World-space `(px, pz)` — single triangle, UV continuity is trivial.

---

## 9. Normal Generation

### 9.1 Strategy: RecalculateNormals + Vertex Isolation

The existing code uses `mesh.RecalculateNormals()` which computes per-vertex normals by averaging face normals of all triangles sharing that vertex.

**Key principle**: **Vertices at crease boundaries are duplicated** (same position, separate vertex index). This creates automatic hard edges. The current code already does this — `AddTop` and `AddWallX` create separate vertices even at shared positions.

### 9.2 Bevel Strip Normals

Each bevel strip is an **isolated vertex strip** — its vertices are separate from both the top face and the wall face. This produces:

| Boundary | Normal Behavior |
|----------|----------------|
| Top face ↔ Bevel strip start | **Hard edge** — separate vertices at same position. Top face normal = (0, 1, 0). Bevel strip start normal ≈ tilted outward. |
| Within bevel strip | **Smooth** — adjacent strip rows share vertices. `RecalculateNormals` averages → smooth curve. |
| Bevel strip end ↔ Wall face | **Hard edge** — separate vertices at same position. Wall normal = (±1, 0, 0) or (0, 0, ±1). Bevel strip end normal ≈ tilted outward. |

### 9.3 Curved Bevel Normals

For `AddBevelStripCurved`, the vertices along the arc direction also share indices → smooth normals along the curve AND along the bevel profile. This creates a smooth toroidal surface, which is the desired behavior.

### 9.4 Corner Join Normals

Convex corner fan vertices are isolated → `RecalculateNormals` gives outward-pointing normals per triangle. For low `bSeg` (1-3), the faceting is subtle enough to be acceptable. For higher `bSeg`, the fan triangles are small enough that auto-normals appear smooth.

---

## 10. Manifold / Watertight Geometry Guarantee

### 10.1 Definition

"Watertight" in this context means: **no visual cracks between adjacent primitives when viewed from above**. The mesh is intentionally open at the bottom (no floor face), so true topological watertightness is not a goal.

### 10.2 Guarantee Mechanism

Every adjacent geometry primitive pair shares **exact vertex positions** at their boundary. Even though vertex indices are separate (for hard edges), the positions coincide to floating-point precision.

| Boundary | Position Match |
|----------|---------------|
| Top face edge ↔ Bevel strip row 0 | Both use `(x ± bW, yT, z)` — same coordinate expressions |
| Bevel strip row bSeg ↔ Wall top edge | Both use `(x, yT - bW, z)` — same coordinate expressions |
| Bevel strip end ↔ Corner join edge | Both use same `(cornerX ± bW×cos(angle), yT - bW×sin(angle), cornerZ ± bW×sin/cos(angle))` |
| Wing top edge ↔ Bevel strip of inner boundary wall | Both use `cx[0] + bW` (or equivalent) |

### 10.3 Floating-Point Consistency

All coordinates are computed from the **same source values** (`cx[]`, `cz[]`, `bW`, `yT`) using the **same arithmetic expressions**. No intermediate rounding or separate computation paths exist. This guarantees bit-exact position matching.

---

## 11. Degenerate Case Handling

### 11.1 Single Empty Cell

```
Grid: 4×2, only cell (2, 1) is empty.
```

**Walls**: All 4 sides have walls (all neighbors are filled or OOB via Phase 1 guards).
**Top face**: Inset by `bW` on all 4 sides → `(cellSize - 2×bW)²` rectangle.
**Bevel strips**: 4 strips, one per side.
**Corner joins**: 4 convex corners (all corners have both adjacent walls).

**Guard**: When `cellSize - 2×bW < 0.001`, skip the top face entirely (degenerate). This only occurs if `bW > cellSize/2`, which is prevented by clamping (`bW ≤ cellSize × 0.25`).

**Result**: ✅ Fully correct geometry.

### 11.2 Thin Corridor (1-cell wide)

```
Grid: 4×2
[FILLED] [EMPTY] [FILLED] [FILLED]
[FILLED] [EMPTY] [FILLED] [FILLED]
```

**Walls**: Cells (1,0) and (1,1) are empty. Each has walls on left and right (but NOT between them — they're both empty).
**Top faces**: Inset on left and right only → narrow rectangles `(cellSize - 2×bW) × cellSize`.
**Bevel strips**: 2 per cell (left + right).
**Corner joins**: At the 4 external corners of the corridor, convex joins. At the 2 internal corners (where cells meet), no joins (no wall between them).

**Result**: ✅ Correct. The corridor top surface is continuous between the two cells.

### 11.3 Small Bevel Width (`bW → 0.001`)

**Behavior**: All geometry is technically correct but the bevel strip becomes visually invisible. The `bSeg` quads are sub-pixel size.

**Guard**: When `bW < 0.001`, skip all bevel strip and corner join calls → fall back to current (sharp edge) behavior. This avoids wasting triangles on invisible geometry.

### 11.4 Large Bevel Width (`bW → cellSize × 0.25`)

**Behavior**: Top faces shrink to 50% of original size on fully-enclosed cells. Bevel strips are proportionally larger. Visual: very rounded, pillow-like appearance.

**Guard**: Clamping at `cellSize × 0.25` prevents the top face from vanishing. Additionally, `bW ≤ tileHeight` prevents the bevel from exceeding the wall height.

**Sub-case**: `bW ≥ R` (bevel width exceeds XZ corner radius). The inset fan radius `R - bW` becomes negative. **Guard**: `fanRadius = Max(0.001, R - bW)`. When the fan degenerates, it effectively becomes a point → the curved bevel strip fills the entire corner area.

### 11.5 T-Junctions

```
Grid: 4×2
[EMPTY]  [FILLED] [EMPTY]  [EMPTY]
[EMPTY]  [EMPTY]  [EMPTY]  [EMPTY]
```

At the grid vertex `(cx[2], cz[1])` — between cells (1,0), (2,0), (1,1), (2,1):
- Cell (1,0): empty, wall on top (cell (1,1) is filled)
- Cell (2,0): empty, no wall on left (cell (1,0) is empty)
- Cell (1,1): FILLED — not processed
- Cell (2,1): empty, no wall on left (cell (1,1) is filled? Wait — cell (1,1) IS cell (1,1)...)

Let me re-index. Using 0-indexed cols and rows:
- Cell (1,1) = col 1, row 1 = FILLED
- Cell (1,0) = col 1, row 0 = EMPTY
  - wR[1,0] = !E(2,0) = false (cell (2,0) is empty) → no right wall
  - wF[1,0] = !E(1,1) = true (cell (1,1) is filled) → front wall exists
- Cell (2,0) = col 2, row 0 = EMPTY
  - wL[2,0] = !E(1,0) = false (cell (1,0) is empty) → no left wall
  - wB[2,0] = depends on row -1 (OOB, handled by Phase 4)

At vertex `(cx[2], cz[1])`:
- Cell (1,0) top-right corner: sideA=wR=false, sideB=wF=true → **straight continuation**, no corner piece.
- Cell (2,0) top-left corner: sideA=wL=false, sideB=wF[2,0]=!E(2,1)=? 

The bevel strip of cell (1,0)'s front wall runs from `cx[1]` to `cx[2]`. At `cx[2]`, the strip ends flush against the top surface of cell (2,0) (which extends to `cx[2]` with no left-side inset).

**Result**: ✅ No visual gap. The bevel strip runs to the cell edge, the adjacent cell's top surface fills the rest.

### 11.6 L-Shapes

```
Grid: 4×2
[EMPTY]  [EMPTY]  [FILLED] [FILLED]
[EMPTY]  [FILLED] [FILLED] [FILLED]
```

Cell (0,0), (1,0), (0,1) are empty forming an L-shape.

At the **inner corner** vertex `(cx[1], cz[1])`:
- Cell (0,0) top-right: sideA=wR[0,0]=true (cell (1,0) is... wait, (1,0) is EMPTY). wR[0,0] = !E(1,0) = false.

Let me re-examine:
- Cell (0,0): wR = !E(1,0) = false. wF = !E(0,1) = false. Diagonal (1,1) is FILLED.
- This is the **concave corner** case: `!sideA && !sideB && diag_filled`.

A `AddBevelCornerConcave` triangle is emitted at `(cx[1], cz[1])`.

Meanwhile, cells (1,0) and (0,1) generate their own walls toward the filled cells:
- Cell (1,0): wR = !E(2,0) = true (FILLED). Generates right wall + bevel.
- Cell (0,1): wF = !E(0,2) → OOB → no front wall via Phase 1.

But cell (1,0) also has wF = !E(1,1) = true. Generates front wall + bevel. At vertex (cx[2], cz[1]), cell (1,0) has wR=true and wF=true → **convex corner**.

**Result**: ✅ L-shape produces correct mix of convex + concave corners.

### 11.7 U-Shapes

```
Grid: 4×2
[EMPTY]  [FILLED] [EMPTY]  [EMPTY]
[EMPTY]  [EMPTY]  [EMPTY]  [EMPTY]
```

Two separate regions connected at the bottom row. Cell (0,0), (0,1), (1,0), (2,0), (3,0), (2,1), (3,1) are empty. Cell (1,1) is filled.

At vertex `(cx[1], cz[1])` (between rows 0 and 1):
- Cell (0,0): wR = !E(1,0) = false. wF = !E(0,1) = false. Diag (1,1) = FILLED → **concave corner**.
- Cell (1,0): wL = !E(0,0) = false. wF = !E(1,1) = true → front wall.

The concave corner fill and the front wall bevel coexist at this vertex.

**Result**: ✅ Correct topology.

### 11.8 All Cells Empty (Full Board)

No inter-cell walls. No insets on cell tops. Only outer walls and inner boundary walls (all ibL/ibR/ibB = false since all cells are empty).

**Result**: ✅ Only outer wall bevels are generated. Cell tops remain full-size.

---

## 12. Performance Estimates

### 12.1 Vertex Count

**Per inter-cell wall**:
- Wall body: 4 vertices (unchanged)
- Bevel strip: `2 × (bSeg + 1)` vertices (NEW)
- Total per wall: `4 + 2(bSeg + 1)`

**Per convex corner join**:
- `bSeg + 2` vertices

**Per concave corner join**:
- 3 vertices

**Baseline** (no bevel, typical 4×2 grid, mixed layout):
- ~100-200 cell vertices + ~300-400 wing/outer vertices = **~500 vertices total**

**With bevel** (bSeg = 3, same layout):

| Component | Count | Verts Each | Total Verts |
|-----------|-------|-----------|-------------|
| Inter-cell walls | ~12 | 4 + 8 = 12 | 144 |
| Inter-cell convex corners | ~8 | 5 | 40 |
| Inter-cell concave corners | ~2 | 3 | 6 |
| Outer straight walls (3) | 3 | 4 + 8 = 12 | 36 |
| Outer curved walls (4) | 4 | `S×2 + (S+1)(bSeg+1)` ≈ 40 | 160 |
| Inner boundary walls | ~4 | 12 | 48 |
| Corner fans (inset) | ~6 | S+2 ≈ 8 | 48 |
| Cell tops (inset) | ~6 | 4 | 24 |
| Wing tops (segmented) | ~10 | 4 | 40 |
| **Total** | | | **~546** |

Compared to current baseline of ~500 → **~10% increase for typical layouts**.

### 12.2 Worst Case (7×6 Grid, Checkerboard)

| Component | Count | Verts Each | Total Verts |
|-----------|-------|-----------|-------------|
| Inter-cell walls | ~84 | 12 | 1008 |
| Inter-cell convex corners | ~84 | 5 | 420 |
| Outer walls + curves | ~7 | ~40 | 280 |
| Cell tops | ~21 | 4 | 84 |
| Wing tops | ~15 | 4 | 60 |
| **Total** | | | **~1852** |

Current baseline for same layout: ~850 → **~2.2× increase**.

### 12.3 Triangle Count

Each vertex roughly maps to ~1 triangle in this mesh topology.

- Typical: ~550 triangles (from ~200 current)
- Worst case: ~1900 triangles (from ~850 current)

### 12.4 Mobile Performance Impact

| Metric | Value | Assessment |
|--------|-------|-----------|
| Max vertex count | ~2000 | **Negligible**. Mobile GPUs handle 50K+ vertices at 60fps. |
| Max triangle count | ~1900 | **Negligible**. |
| Draw calls | 2 (unchanged) | Same 2 submeshes, same 2 materials. |
| Mesh build time | ~0.5ms (editor-time only) | Not runtime — zero gameplay impact. |
| Memory | ~48KB mesh data worst case | **Negligible**. |

> [!TIP]
> The mesh is **baked at editor time** and stored as a `.asset` file in the prefab. Runtime never calls `BuildMesh`. Therefore, the bevel system has **exactly zero runtime performance cost**.

---

## Appendix: Submesh Assignment

Bevel strips and corner joins go into **submesh 1 (wall material)**.

**Rationale**: Reference games (Block Blast, Color Block Jam) show the bevel/chamfer as a continuation of the wall surface — same color/material as the sides. The bevel is the rounded top edge of the wall, not the edge of the top surface.

**Implementation**: All `AddBevelStrip*` and `AddBevelCorner*` functions write triangles to the `trisWall` list (same as `AddWallX`, `AddWallZ`, `AddCurvedWall`).

No submesh count change. No material count change. No draw call change.
