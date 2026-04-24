# Combat-Optimized Procedural Arena Generator

![Unity](https://img.shields.io/badge/Unity-6.x-black?style=flat&logo=unity)
![C#](https://img.shields.io/badge/C%23-Unity-blue?style=flat&logo=c-sharp)
![Status](https://img.shields.io/badge/Status-Final_Year_Project-orange)

---

## Overview

This repository contains a procedural generation framework for First-Person Shooter (FPS) environments. The system integrates a rule-based generator with a **post-generation validation pipeline** to filter layouts using spatial and gameplay-oriented metrics.

Unlike purely random generation approaches, this framework evaluates generated maps based on properties such as sightline exposure, visibility balance, and navigability to improve structural consistency.

This project was developed as a BSc Computer Science final year project and evaluated through large-scale batch simulations and comparison with human-authored maps.

<img width="841" height="801" alt="System Architecture drawio" src="https://github.com/user-attachments/assets/bee6b2ce-dc8e-45a6-a162-588a9ff5c79f" />

---

## Core Features

- **Procedural Geometry Generation**  
  Generates multi-room layouts within bounded environments.

- **Cover Field Generator**  
  Distributes tactical cover elements using spatial constraints to maintain consistent coverage.

- **Tactical Analysis Suite**  
  Evaluates generated layouts using quantitative metrics:
  - Sightline Exposure  
  - Visibility Balance (Symmetry Delta)  
  - Chokepoint Centrality  
  - NavMesh Connectivity  

- **Batch Simulation Pipeline**  
  Automates large-scale generation and validation for analysis and export.

---

## Performance & Evaluation

The framework was evaluated using a 1,000-iteration batch simulation and compared against selected human-authored FPS maps (e.g., `aim_map`, `Warehouse`).

- **Execution Time:** ~75.11 ms per map (CPU time, including generation and validation)  
- **Acceptance Rate:** 46.7% of generated layouts passed all validation criteria  
- **Filtering Effect:** Validation reduces extreme spatial imbalance (e.g., high visibility asymmetry) and constrains metric distributions  

These results indicate that the framework functions as a filtering mechanism, improving consistency across generated layouts.

---

## System Architecture

The system is structured into modular components:

### 1. Generation Pipeline
- `LevelOrchestrator.cs` — Coordinates the batch execution process, including performance profiling and state management.  
- `MapBorderGenerator.cs` — Constructs the outer map boundaries and containment walls.  
- `MultiRoomManager.cs` — Handles interior space allocation by subdividing the arena into multiple room-based zones.  
- `CoverFieldGenerator.cs` — Places tactical cover elements using spatial constraints to maintain a target navigable area ratio (15%–25%).  

### 2. Validation & Analysis Suite
- `CoverAnalyzer.cs` — Evaluates cover distribution, including density, evenness, and clustering characteristics.  
- `ConnectivityChecker.cs` — Validates NavMesh connectivity and ensures all regions are reachable.  
- `SightlineAnalyzer.cs` — Computes sightline exposure and flow-related visibility metrics.  
- `SymmetryEvaluator.cs` — Measures visibility balance and spatial asymmetry across the layout.  

---

##  Getting Started

1. Clone the repository.
2. Open the project in Unity 6.2 (6000.2.10f1)
3. Open the `Working_Scene`.
4. Enter Play Mode and press **[B]** to initiate a batch automation sequence, or **[R]** to generate a single map.
5. Passed maps are automatically serialized as Prefabs in the `Assets/Exports/` directory.
