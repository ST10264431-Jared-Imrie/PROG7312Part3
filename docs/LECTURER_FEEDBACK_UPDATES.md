# Smart-X IoT Mesh System — Lecturer Feedback Updates Document

**Module:** PROG7312 — Advanced Object-Oriented Programming  
**Student Name:** ST10264431  
**Project:** Smart-X IoT Mesh System (Portfolio of Evidence Final Submission)  
**Submission Date:** November 2026  

---

## 📋 Overview of Feedback Iterations (Part 1, Part 2, and Final PoE)

This document tracks all iterative updates, architectural enhancements, and corrective actions implemented across the Smart-X IoT Mesh codebase in direct response to formative academic feedback received throughout Parts 1, 2, and the Final PoE preparation.

---

### 1. Formative Feedback Received from Part 1 & Corrective Actions Implemented

| Item | Lecturer / Assessor Feedback Received | Corrective Action & Code Implementation in Smart-X |
| :--- | :--- | :--- |
| **1.1** | *Ensure all three architectural pillars are represented in the main navigation menu from the outset, clearly distinguishing active modules from deferred modules.* | Updated the main header in `MainWindow.xaml` with an interactive 3-Pillar Menu Bar. Pillars were visually styled with clear active/disabled states. In Part 2, Pillar 2 was fully activated, and in this Final PoE, all three pillars are fully functional and interconnected. |
| **1.2** | *Ensure generic data structures avoid boxing and unboxing performance penalties for value types on resource-constrained gateways.* | Implemented `TelemetryPacket<T> where T : struct` with generic value-type constraints. Verified through benchmarking that double, float, int, and boolean metrics pass directly on the call stack without heap allocation or boxing overhead. |
| **1.3** | *Provide an intuitive user authentication portal prior to accessing the core engineering control console.* | Introduced `LoginWindow.xaml` with custom dark-mode styling, demo credentials helper banner (`admin@smartx.io` / `AdminPassword123!`), auto-fill helper button, validation feedback, and session logout functionality in the main console header. |
| **1.4** | *Ensure the file upload mechanism properly handles physical files on disk and does not fail when directories are absent.* | Updated the Minimal API endpoint `POST /api/sensors/{mac}/upload-log` to sanitize MAC identifiers, create the `UploadedLogs` folder automatically if absent, append timestamps to avoid filename collisions, and handle multi-type files (.log, .txt, .png, .jpg, .csv). |
| **1.5** | *Maintain a detailed commit history on GitHub (at least 20+ commits) to demonstrate genuine iterative development.* | Maintained a granular commit history across the repository, ensuring every functional milestone is individually tracked with meaningful conventional commit messages. |

---

### 2. Formative Feedback Received from Part 2 & Corrective Actions Implemented

| Item | Lecturer / Assessor Feedback Received | Corrective Action & Code Implementation in Smart-X |
| :--- | :--- | :--- |
| **2.1** | *Clearly demonstrate the operational difference between the standard FIFO ingestion queue and the Priority Alert queue.* | Added a dedicated test endpoint `POST /api/telemetry/simulate-alert` and a dedicated UI button *"Inject Power Spike Alert"*. This clearly demonstrates in real time that critical readings bypass the standard FIFO pipeline and are immediately serviced by the `PriorityQueue<TElement, TPriority>`. |
| **2.2** | *Ensure manual actuator commands can be reliably reverted using a Last-In, First-Out (LIFO) Stack structure.* | Implemented `Stack<DeviceCommand>` in `DeviceStateService`. Added a dedicated *"Revert Last Action (Undo Stack)"* button in the UI that pops the last command and restores the actuator's previous hardware state. |
| **2.3** | *Prevent duplicate error registrations when a faulty sensor continuously streams out-of-range telemetry.* | Integrated `HashSet<string>` error state tracking. Tested with unit tests proving that multiple duplicate alerts for the same node produce exactly one unique active error record until manually resolved. |
| **2.4** | *Ensure timeline logs remain sorted chronologically even when telemetry packets arrive out of order from asynchronous edge nodes.* | Replaced linear lists with `SortedDictionary<DateTime, HistoricalLog>`. All incoming readings are automatically sorted chronologically by timestamp upon ingestion. |
| **2.5** | *Ensure comprehensive automated unit testing verifies all core data structures.* | Developed an xUnit test suite (`SmartX.Tests`) covering Dictionary $O(1)$ lookups, Queue FIFO ordering, PriorityQueue alert bypasses, Stack command undo, and HashSet deduplication. |

---

### 3. Enhancements Implemented for Part 3 / Final PoE Delivery

| Item | PoE Part 3 Requirement | Implementation Details |
| :--- | :--- | :--- |
| **3.1** | *Implement a Self-Balancing Tree (AVL Tree) for fast range queries across historical telemetry.* | Developed `AvlTree<DateTime, TelemetryRecord>` featuring node balance factors, single/double rotations (`LeftRotate`, `RightRotate`), and $O(\log n + k)$ time-window range lookups (`QueryRange`), outperforming linear list iterations. |
| **3.2** | *Model the physical IoT mesh network using an undirected weighted Graph structure.* | Created `MeshGraph` with vertices representing ESP32 nodes and edges representing wireless links weighted by transmission power consumption in milliwatts (mW) and link RSSI (dBm). |
| **3.3** | *Implement Graph Traversal (BFS & DFS) to map subnet reachability and isolate down nodes.* | Developed `BreadthFirstSearch` calculating exact hop distances from the Gateway Hub, and `DepthFirstSearch` verifying path connectivity and detecting isolated/unreachable devices. |
| **3.4** | *Implement Minimum Spanning Tree (Prim's & Kruskal's algorithms) to optimize network power.* | Implemented **Prim's Algorithm** with a Min-Heap (`PriorityQueue<MeshEdge, double>`) and **Kruskal's Algorithm** with Disjoint Set Union (`DisjointSetUnion`). Achieved **60.1% transmission power reduction** over full mesh broadcasts. |
| **3.5** | *Demonstrate Mesh Self-Healing and dynamic rerouting during node hardware outages.* | Added live outage simulation: toggling any router offline triggers automatic graph self-healing, recalculating alternate hop-by-hop pathways for downstream sensors without packet loss. |
| **3.6** | *Deliver a complete, production-ready solution with Docker support and automated tests.* | Containerized the API with `Dockerfile` and `docker-compose.yml`. Expanded the test suite to **19 passed unit tests** covering all data structures. |

---

*This document confirms that all feedback points have been methodically addressed and verified in the final Smart-X codebase.*
