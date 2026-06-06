# Combat-Optimized Procedural Arena Generator

![Unity](https://img.shields.io/badge/Unity-6.x-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/C%23-Unity-blue?style=flat&logo=c-sharp)
![Status](https://img.shields.io/badge/Status-Manuscript_In_Preparation-orange)
![License](https://img.shields.io/badge/License-Academic-lightgrey)

> A Unity-based framework that automates the generation and validation of 3D interior layouts for First-Person Shooter games, using empirically calibrated spatial metrics to filter tactically viable combat arenas without relying on agent-based simulation or manual playtesting.

---

## Demo

<img width="669" height="529" alt="image" src="https://github.com/user-attachments/assets/d35781c6-5175-4a7c-8b35-d2e85c15f408" />

<video src="Demo.mp4" autoplay loop muted playsinline width="100%"></video>

<img width="841" height="801" alt="System Architecture" src="https://github.com/user-attachments/assets/bee6b2ce-dc8e-45a6-a162-588a9ff5c79f" />

---

## Overview

Procedural Content Generation (PCG) in FPS games frequently prioritises structural variation over functional gameplay quality, resulting in layouts that lack the tactical coherence required for engaging combat. Existing evaluation approaches rely on manual playtesting or computationally expensive agent-based simulation, both of which are difficult to scale across large volumes of generated content.

This framework addresses that gap by integrating a rule-based procedural generator with an automated spatial validation suite directly within the Unity Engine. Rather than relying on subjective human judgement or live behavioural simulation, it formalises abstract FPS design principles — cover utility, sightline control, navigational flow — into quantifiable spatial algorithms that evaluate each generated layout in under 75ms.

Validation thresholds are empirically calibrated against three human-authored benchmark maps from established competitive FPS titles, ensuring that accepted layouts fall within spatial ranges consistent with proven game designs. A user perception survey (n=21) further confirmed that layouts passing the metric-driven constraints are consistently perceived as having higher tactical balance and overall gameplay suitability.

---

## Key Results

| Metric | Value |
|--------|-------|
| Average execution time per map | ~75.11 ms |
| Batch size | 1,000 iterations |
| Acceptance rate | 46.7% (467/1,000) |
| Primary rejection cause | Tactical flow & sightline constraints (483 failures) |
| Survey preference for validated layouts | 78.0% of decisive votes (191/245) |

<img width="579" height="408" alt="image" src="https://github.com/user-attachments/assets/644fc21e-7cc3-40ae-8bd9-6e27fe39d553" />

<img width="729" height="405" alt="image" src="https://github.com/user-attachments/assets/8da4b294-8326-4e7c-8f37-9ec13bde9b0a" />

<img width="672" height="1015" alt="image" src="https://github.com/user-attachments/assets/25341b3c-78ae-42ca-85fc-4fa6bec98669" />

---

## Spatial Validation Metrics

The validation suite evaluates each generated layout across 8 quantitative metrics:

| Metric | Description | Threshold |
|--------|-------------|-----------|
| Cover Density ($\rho$) | Ratio of cover-reserved area to total playable area | 0.04 – 0.15 |
| Distribution Evenness ($U$) | Coefficient of variation across 4×4 spatial grid | ≥ 0.40 |
| Clustering Score ($C$) | Fraction of covers forming valid tactical networks (3.5m radius) | 0.30 – 0.85 |
| Cover Effectiveness ($E$) | Average ray-blocking ratio across standing and crouched stances | ≥ 0.25 |
| Sightline Exposure | Mean isovist-based openness score across navigable nodes | 0.30 – 0.70 |
| Visibility Balance ($\Delta$) | Asymmetry delta between Team A and Team B exposure territories | ≤ 15% |
| Navigable Area Ratio | Walkable surface area relative to total playable bounds | 0.55 – 0.85 |
| Chokepoint Centrality | Maximum traffic concentration from Monte Carlo path simulation | ≥ 0.15 |

Thresholds were derived by aligning unconstrained procedural distributions with spatial ranges observed across three human-authored benchmark maps: `aim_map`, `aim_redline` (Counter-Strike franchise), and `Warehouse` (PUBG Mobile).

---

## Benchmark Comparison

| Metric | aim_map | aim_redline | Warehouse | Validated avg |
|--------|---------|-------------|-----------|---------------|
| Cover Density | 0.08 | 0.10 | 0.05 | 0.063 |
| Avg Cover Effectiveness | 0.66 | 0.74 | 0.62 | 0.681 |
| Distribution Evenness | 0.48 | 0.45 | 0.49 | 0.668 |
| Sightline Exposure | 0.57 | 0.74 | 0.77 | 0.444 |
| Visibility Balance (%) | 0.6 | 9.1 | 3.0 | 7.63 |
| Navigable Area (%) | 74 | 84 | 95 | 65.7 |
| Chokepoint Centrality | 0.25 | 0.25 | 0.16 | 0.352 |

---

## Core Features

- **Procedural Geometry Generation**
  Generates multi-room layouts within bounded environments using stochastic placement with rejection sampling.

- **Cover Field Generator**
  Distributes three tactical cover archetypes (Pillars, Barriers, L-Blocks) calibrated against Source Engine player controller dimensions.

- **Tactical Analysis Suite**
  Evaluates generated layouts across 8 combat-oriented spatial metrics without agent-based simulation:
  - Cover Density, Evenness, Clustering, and Effectiveness
  - Sightline Exposure and Visibility Balance
  - Navigable Area Ratio and Chokepoint Centrality

- **Batch Simulation Pipeline**
  Automates large-scale generation, validation, and CSV export for statistical analysis.

---

## System Architecture

The pipeline operates as a closed-loop generate-and-test cycle across three phases:

1. **Procedural Synthesis** — Constructs bounded arena, allocates interior rooms, distributes tactical cover archetypes using spatial exclusion constraints
2. **Analytical Validation** — Evaluates layout against 8 calibrated spatial metrics using raycasting, NavMesh analysis, and Monte Carlo traffic simulation
3. **Autonomous Curation** — Accepts and exports viable layouts as Unity Prefabs; reseeds generator on failure

### Generation Pipeline

- `LevelOrchestrator.cs` — Coordinates batch execution, performance profiling, and state management
- `MapBorderGenerator.cs` — Constructs outer map boundaries and containment walls
- `MultiRoomManager.cs` — Handles interior space allocation with rejection sampling
- `CoverFieldGenerator.cs` — Places tactical cover archetypes with geometric exclusion constraints

### Validation & Analysis Suite

- `CoverAnalyzer.cs` — Evaluates cover density, evenness, clustering, and effectiveness via radial raycasting
- `ConnectivityChecker.cs` — Validates NavMesh connectivity and navigable area ratio using A* pathfinding
- `SightlineAnalyzer.cs` — Computes isovist-based sightline exposure across 1.5m sampling grid
- `SymmetryEvaluator.cs` — Measures visibility balance and spatial asymmetry delta across team territories

---

## Getting Started

1. Clone the repository
2. Open the project in Unity 6.2 (6000.2.10f1)
3. Open `Working_Scene`
4. Enter Play Mode:
   - Press **[B]** to initiate a batch automation sequence
   - Press **[R]** to generate a single map
5. Validated maps are serialized as Prefabs in `Assets/Exports/PassedMaps/`
6. Batch results are exported as CSV to `Assets/ProceduralGeneration_BatchResults.csv`

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/          # LevelOrchestrator, batch automation
│   ├── Generation/       # MapBorderGenerator, MultiRoomManager, CoverFieldGenerator
│   └── Validation/       # CoverAnalyzer, ConnectivityChecker, SightlineAnalyzer, SymmetryEvaluator
├── Exports/
│   └── PassedMaps/       # Serialized validated layout Prefabs
└── ProceduralGeneration_BatchResults.csv
```

---

## Limitations

- **Static evaluation only** — operates pre-runtime; does not capture dynamic gameplay behaviour or real-time player interaction
- **Single-level topology** — does not support multi-floor environments, balconies, or elevation advantages
- **CQC-specific thresholds** — calibrated for close-quarters arena formats; may not generalise to large-scale tactical or asymmetric game modes
- **Geometric simplification** — cover archetypes are limited to three primitive shapes rather than complex architectural geometry
- **Small survey sample** — user perception validated with n=21 convenience sample from a single institution

### Future Directions

- Agent-based simulation integration for dynamic gameplay evaluation
- Multi-floor and volumetric spatial analysis
- Evolutionary or reinforcement learning optimization to replace generate-and-test filtering
- Expanded benchmark dataset across FPS subgenres and player counts
- Larger, demographically diverse user study with playable 3D environments

---

## Full Report

The complete project report including methodology, mathematical metric definitions, and full results is available here:

<!--[Final_Report.pdf](https://github.com/user-attachments/files/28582772/Final_Report.pdf)-->

> *Report link will be added post-graduation.*

---

## Citation

If you reference this work, please cite:

```bibtex
@misc{wong2026fps,
  author       = {Wong, Jin Xuan},
  title        = {A Framework for Automated Validation of Combat-Optimized Procedural Interiors in First-Person Shooter Games},
  year         = {2026},
  howpublished = {\url{https://github.com/Alone1101/A-Framework-for-Automated-Validation-of-Combat-Optimized-Procedural-Interiors-in-FPS}},
  note         = {BSc Computer Science Final Year Project, University of Southampton Malaysia. Manuscript in preparation.}
}
```

*Citation will be updated upon publication.*

---

## Acknowledgements

Developed under the supervision of Dr Marwan D. Saleh at the University of Southampton Malaysia (UoSM). Second examiner: Dr Syed Hamid Hussain Madni.

Benchmark maps sourced from GameBanana (`aim_map`, `aim_redline`) and Sketchfab (`Warehouse`, PUBG Mobile). All maps geometrically simplified for spatial metric analysis only.
