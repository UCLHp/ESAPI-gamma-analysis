# GammaAnalysis

## Overview
Gamma Index Calculator - Eclipse ESAPI Script
Overview
This Eclipse Scripting API (ESAPI) plugin calculates 3D gamma index between two dose distributions and optionally creates a gamma distribution plan for visualization in Eclipse.
Features

Global or Local normalization - Choose between global (maximum dose) or local (reference dose at each point) normalization
Customizable criteria - Set Distance-to-Agreement (DTA), Dose Difference (DD), and minimum dose threshold
Gamma distribution visualization - Optionally create a plan showing the gamma distribution overlaid on CT
Performance optimized - Handles full 3D dose grids with pre-computed lookup tables and flat array optimization

Computational Methods
Core Gamma Calculation
The gamma index (γ) at each reference point is calculated as:
γ² = (distance² / DTA²) + (dose_difference² / DD²)
Where:

Distance: 3D Euclidean distance between reference voxel and target voxel positions
DTA: Distance-to-Agreement criterion (mm)
DD: Dose Difference criterion (% of reference dose)

Search Algorithm

Voxel center search: For each reference voxel above the minimum dose threshold, searches neighboring target voxels within a radius determined by DTA
Early termination: Stops searching once γ ≤ 1.0 is found (pass criterion)
Edge interpolation: If no passing voxel center is found, performs linear interpolation along the 6 face-adjacent edges to find potential sub-voxel gamma minima

Interpolation Method
For edges between voxels with different doses, the algorithm:

Solves analytically for the point along the edge where gamma is minimized
Uses the formula: X = [numerator] / [K₁·ΔX/ΔD + ΔD/ΔX]
Only accepts solutions within the valid edge range [0, ΔX]
Calculates gamma at the interpolated position using trilinear position and linear dose interpolation

Performance Optimizations

Flat array storage: Dose grids stored as 1D arrays for better cache locality
Pre-computed position tables: Target voxel positions calculated once and stored
Squared distance comparisons: Avoids expensive square root operations until final result
Early rejection: Skips voxels based on Z or Y+Z distance before full 3D calculation

Usage

Load patient with multiple plans containing dose
Run script from Eclipse
Select reference and target plans
Set gamma criteria (DTA, DD, minimum dose threshold)
Choose global or local normalization
Click "Calculate Gamma Index"
Optionally generate gamma distribution plan for visualization

Requirements

Eclipse ESAPI v15.6 or later
Write-enabled script context (IsWriteable = true)
Plans must have calculated dose

Output

Pass rate (percentage of voxels with γ ≤ 1.0)
Total evaluated voxels (above minimum dose threshold)
Optional gamma distribution plan showing gamma values as "dose"

Technical Notes

Gamma values stored as percentages in the generated plan (γ = 1.0 → 1%)
Voxels below minimum dose threshold assigned γ = -1 (displayed as 0% in plan)
Search radius automatically scaled based on DTA and target grid resolution

## Limitations / known bugs
Get in touch if you have any questions. This work has been performed using Eclipse v16.1.


## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.


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
