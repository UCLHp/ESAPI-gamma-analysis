# GammaAnalysis

## Overview

Gamma Index Calculator - Eclipse ESAPI Script

This Eclipse Scripting API (ESAPI) plugin calculates a 3D gamma index between two dose
distributions and optionally creates a gamma distribution plan for visualisation in Eclipse.

Works with any modality. The calculation operates entirely on `Dose` objects (voxel grids)
and never touches beams, so photon, electron and proton plans are all handled by the same
code path. Both `Course.ExternalPlanSetups` and `Course.IonPlanSetups` are enumerated when
populating the plan dropdowns.

## Terminology

The UI labels the two selected plans **Grid A** and **Grid B**. Their roles are not
symmetric:

| | Role |
|---|---|
| **Grid A** | The distribution **searched within**. Candidate comparison points are drawn from this grid — first voxel centres, then interpolated sub-voxel positions. |
| **Grid B** | The distribution **iterated through**. One gamma value is produced per Grid B voxel, and the pass rate is the fraction of evaluated Grid B voxels with γ ≤ 1. |

**All normalisation is taken from Grid B**, which is why the UI labels it
"Grid_B Plan (Normalise wrt this)":

- Global DD criterion → Grid B maximum dose
- Local DD criterion → the Grid B dose at the voxel being evaluated
- Minimum dose threshold → a percentage of the Grid B maximum dose, applied to Grid B doses

Grid A's maximum dose is calculated and reported but is not used in any criterion.

## Features

- **Global or Local normalisation** — global uses the Grid B maximum dose, local uses the
  Grid B dose at each evaluated voxel
- **Customisable criteria** — Distance-to-Agreement (DTA), Dose Difference (DD), and minimum
  dose threshold
- **Handles dissimilar dose grids** — the two plans may differ in voxel size, array
  dimensions, origin and extent. Comparison is restricted automatically to the region of space
  represented by both grids (see [Evaluated Region](#evaluated-region))
- **Gamma distribution visualisation** — optionally create a plan showing the gamma
  distribution overlaid on CT
- **Read-only unless plotting** — the analysis itself does not modify the patient (see
  [Requirements](#requirements))
- **Performance optimised** — handles full 3D dose grids using pre-computed lookup tables and
  flat array storage

## Computational Methods

### Core Gamma Calculation

The gamma index (γ) at each Grid B voxel is calculated as:

```
γ² = (distance² / DTA²) + (dose_difference² / DD²)
```

Where:

- **distance** — 3D Euclidean distance between the Grid B voxel position and the candidate
  Grid A position
- **DTA** — Distance-to-Agreement criterion (mm)
- **DD** — Dose Difference criterion, as a percentage of the Grid B normalisation dose

### Search Algorithm

For each Grid B voxel above the minimum dose threshold, the search runs in three phases and
stops as soon as a passing point (γ ≤ 1) is found:

1. **Voxel centre search** — searches Grid A voxel centres within a radius derived from the
   DTA and the Grid A grid resolution.
2. **Edge interpolation** — if no passing voxel centre is found, solves analytically for the
   gamma minimum along each of the 6 face-adjacent edges from the nearest Grid A voxel.
3. **Trilinear interpolation** — if still not passing, samples the unit cubes immediately
   surrounding the nearest Grid A voxel at a step of 1/5 of a voxel in each dimension, using
   full trilinear dose interpolation.

The search radius is one DTA. That is sufficient to decide pass/fail, because beyond one DTA
the spatial term alone gives γ > 1 regardless of dose agreement. It is **not** sufficient to
report the true minimum gamma for a failing voxel — see
[Limitations](#limitations--known-issues).

### Interpolation Method

**Phase 2 (edge interpolation)** treats dose as varying linearly along a single axis-aligned
edge between two Grid A voxels. It solves analytically for the position along that edge which
minimises gamma:

```
X = [(D_B - D_a) + K₁·d·ΔX/ΔD] / [K₁·ΔX/ΔD + ΔD/ΔX]      where K₁ = DD² / DTA²
```

Solutions outside the valid edge range [0, ΔX] are rejected. Position and dose are then
interpolated linearly at the solved point and gamma evaluated there.

**Phase 3 (trilinear interpolation)** does not solve analytically. It samples each candidate
unit cube on a regular grid and evaluates the full trilinear dose interpolation at every
sample point.

### Evaluated Region

A Grid B voxel is compared only if **both** of the following hold:

1. Its Grid B dose is at or above the minimum dose threshold.
2. Its position falls within the physical extent of the Grid A dose grid.

Voxels failing either test are excluded from the pass rate entirely — they enter neither the
numerator nor the denominator. They are not counted as failures.

The second test restricts the comparison to the region of space represented by **both** dose
grids. No alignment step is involved: `Dose.Origin` is a DICOM coordinate, so the two grids
are already related through the shared patient frame, and grids of different sizes,
resolutions, origins and extents are mapped onto each other correctly. What the mapping
cannot do is invent Grid A data where the Grid A matrix does not reach, and those positions
are what this test removes.

Because every Grid B voxel is inside Grid B by definition, testing "inside Grid A" is
sufficient to enforce the intersection. **It does not matter which plan the user puts in
which slot** — the test adapts either way.

Coverage is **geometric only**. It asks whether the Grid A matrix extends to that position,
not whether the dose there is non-zero. A voxel with high dose in one grid and zero in the
other is a genuine disagreement and is still allowed to fail. (There is also no way to
distinguish "zero because no dose" from "zero because outside the calculation volume" through
`GetVoxels` — both read as 0.)

If nothing can be evaluated, the run is abandoned with a message box and no results row or
gamma plan is produced. Two cases are distinguished: no overlap between the grids, and no
voxel reaching the minimum dose threshold.

**Accepted limitation.** A voxel inside Grid A but within one DTA of its boundary has part of
its search sphere outside the Grid A data, so its gamma is biased high. Such voxels are
treated as ordinary voxels. The bias is one-directional — a truncated search can hide a
passing point but never invent one — so this can only depress the pass rate slightly, never
inflate it. The affected shell is thin and this was accepted as the simpler, less error-prone
behaviour over eroding the evaluated volume by one DTA.

**Normalisation is not restricted to this region.** `Grid_BMaxDose`, which drives both the
minimum dose threshold and the global DD criterion, is still taken over the whole of Grid B
including any excluded region. This is a deliberate choice — the plan maximum is a meaningful
quantity in its own right, and a cropped Grid A almost always surrounds the high-dose region
anyway — but it is worth knowing.

### Performance Optimisations

- **Flat array storage** — dose grids stored as 1D arrays for better cache locality
- **Pre-computed position tables** — Grid A voxel positions calculated once and stored
- **Squared distance comparisons** — avoids square roots until the final result
- **Early rejection** — skips candidates on Z, then Y+Z distance, before the full 3D
  calculation
- **Early termination** — abandons the search as soon as γ ≤ 1 is found

## Usage

1. Load a patient with two or more plans containing calculated dose
2. Run the script from Eclipse
3. Select the Grid A and Grid B plans (see [Terminology](#terminology))
4. Set the gamma criteria (DTA, DD, minimum dose threshold)
5. Choose global or local normalisation
6. Set "Produce Gamma Plot?" — choose **No** to keep the run entirely read-only
7. Click "Calculate Gamma Index"

Results accumulate in a table, so several parameter combinations can be compared in one
session.

## Requirements

- Eclipse ESAPI. Developed and tested against **v16.1**; behaviour on earlier versions has not
  been verified.
- Both plans must have calculated dose.
- `[assembly: ESAPIScript(IsWriteable = true)]` is still declared, because creating the gamma
  plot writes to the database.
- **`Patient.BeginModifications()` is only called when the gamma plot is actually created.**
  With "Produce Gamma Plot?" set to No, the script performs no writes, so the analysis can be
  run on read-only or approved patients. Attempting to produce a plot on such a patient
  surfaces as a handled error message rather than a crash.
- `ExternalPlanSetup.CopyEvaluationDose` is a licensed API call and is used only by the gamma
  plot.

## Output

- Pass rate (percentage of evaluated voxels with γ ≤ 1.0)
- Total evaluated voxels — Grid B voxels that are both above the minimum dose threshold and
  inside the Grid A extent (see [Evaluated Region](#evaluated-region))
- Grid A and Grid B maximum doses
- Optionally, a gamma distribution plan showing gamma values as "dose"

`GammaResult` also carries `EvaluatedVoxels` and `UncoveredVoxels`. The latter is the number
of above-threshold Grid B voxels excluded for falling outside the Grid A extent. Neither is
currently shown in the results table.

### Technical Notes

- Gamma values are stored as percentages in the generated plan (γ = 1.0 → 1%)
- Excluded voxels are assigned the sentinel γ = -1 internally and written as 0% in the plan.
  Two distinct cases share this sentinel — below the minimum dose threshold, and outside the
  Grid A extent — and the plot does not distinguish them from each other or from a genuine
  γ = 0 perfect match
- The search radius is scaled automatically from the DTA and the Grid A grid resolution
- The gamma plan Id encodes the parameters used and is truncated to 13 characters

## Limitations / known issues

Please read this section before using any number from this script clinically.

> **Change of behaviour.** Grid B voxels falling outside the Grid A extent were previously
> counted as failures. They are now excluded from the pass rate (see
> [Evaluated Region](#evaluated-region)). Pass rates for comparisons involving dissimilar
> grid extents will differ from those produced by earlier versions.

### Dose units and the absolute-dose scaling

`PlanSetup.TotalDose` is read *after* `DoseValuePresentation` has been set to `Relative`.
`TotalDose` honours the current presentation, so it is expected to report `100 "%"` rather
than the prescription in Gy. The conversion `scaleFactor = planDose / 100.0` therefore
evaluates to 1.0, and both dose arrays remain in percent of their own plan's prescription
rather than absolute Gy.

Consequences:

- Comparisons are like-for-like only when **both plans share the same prescription**. Two
  plans with different prescriptions are compared percentage-against-percentage.
- The results column is headed `Grid_BMaxDose(Gy)` but the reported unit may not be Gy.
- Nothing checks that the two plans report the same unit (Gy vs cGy).

This should be verified on site by printing `TotalDose.Dose` and `TotalDose.UnitAsString` for
a plan whose prescription is known. The behaviour is currently accepted and in routine use for
same-prescription comparisons; the code is commented accordingly.

### Grid geometry assumptions

- **A rotated Grid A dose grid is not handled, and this is not checked.** The Grid A position
  lookup tables use only the diagonal component of each direction cosine, which is exact for
  standard patient orientations (HFS, HFP, FFS, FFP) but wrong for a rotated grid. Negative
  direction cosines are fine; only off-diagonal terms break it.

  Note the asymmetry: **Grid B may be rotated safely.** Grid B world positions are built from
  the full three-component direction vectors and are correct for any orientation, as is the
  world-to-Grid-A index conversion. It is specifically Grid A's own position table that is
  wrong. Since Grid A and Grid B are just whichever plans the user selected, swapping the two
  dropdowns can turn a wrong answer into a right one.

  In practice a rotated dose grid requires a rotated image frame — a gantry-tilted CT, or
  plans on image sets with different orientations.

- **No frame of reference or image check.** Two plans on different CTs would be compared in
  each image's own coordinate system, producing a confidently meaningless result. The
  coverage test relies on both `Dose.Origin` values being in the same DICOM frame, so a
  mismatch will often surface as the "No Overlapping Region" message rather than a wrong
  number. That is an accidental safety net, not a designed check.

### Gamma values above 1

Because the search is truncated at one DTA, reported gamma values above 1 are a **lower
bound**, not the true minimum. The pass rate is unaffected. Treat the colour scale on a failing
region as qualitative.

### Normalisation convention

Local gamma is normalised to the **evaluated** distribution (Grid B). Many published
descriptions and commercial tools normalise the local criterion to the reference distribution
instead. Pass rates from this script will not necessarily agree with such a tool unless that
difference is accounted for.

### Gamma plot

The gamma plot is a convenience visualisation, not a quantitative product:

- Gamma values are written into a dose matrix copied from the Grid B plan. That matrix's
  integer voxel scaling spans the plan's dose range, not the 0–2 range of a gamma index, so the
  displayed map may be coarsely quantised.
- Sub-threshold voxels are written as 0, indistinguishable from a perfect match. Low-dose
  regions therefore appear to pass perfectly.
- The plan Id is truncated to 13 characters, so different parameter combinations can collide,
  and re-running with identical parameters throws on a duplicate Id (caught and reported).
- `Course.AddExternalPlanSetup` always creates a new primary reference point, so repeated runs
  accumulate plans and reference points in the course.

### Other

- The calculation runs synchronously on the WPF UI thread with no progress reporting. Eclipse
  will appear frozen for the duration, which can be significant on large or fine dose grids.
- Only the active course is scanned. Plans in other courses are not offered.
- `PlanSum`s are not offered, because a `PlanSum` has no `TotalDose` for the scaling above.
- Phase 3 refines only the cubes within ±1 voxel of the nearest Grid A voxel, which is a
  narrower region than Phase 1 searches. The Phase 3 Z-axis distance prune is also looser than
  the X and Y prunes. Neither affects pass/fail.

Get in touch if you have any questions. This work has been performed using Eclipse v16.1.

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you
would like to change.

## License

```
Copyright (C) 2025 Matthew Southerby / Steven Court

GammaAnalysis: Eclipse Scripting API (ESAPI) plugin calculates 3D gamma index between two dose distributions and optionally creates a gamma distribution plan for visualization in Eclipse.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License

along with this program.  If not, see <https://www.gnu.org/licenses/>.
```