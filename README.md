# Smart-X IoT Mesh System — Network Topology & Mesh Routing (PoE Final)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF Desktop](https://img.shields.io/badge/UI-WPF%20XAML-purple.svg)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%26%20Modular-green.svg)]()
[![Build & Tests](https://img.shields.io/badge/Tests-19%20Passed-brightgreen.svg)]()
[![License](https://img.shields.io/badge/License-MIT-lightgrey.svg)]()

> **Module:** Advanced Object-Oriented Programming (PROG7312)  
> **Milestone:** Portfolio of Evidence (PoE) Final Delivery — Full Functioning Application  
> **Student Number:** ST10264431  
> **Target Framework:** .NET 10.0 (C# 14 / .NET 10 SDK)  
> **Repository:** [https://github.com/ST10264431/ST10264431PROG7312POEPART1.git](https://github.com/ST10264431/ST10264431PROG7312POEPART1.git)

---

## 📖 System Overview & Executive Summary

The **Smart-X IoT Mesh System** is an enterprise-grade industrial edge computing platform engineered for modern decentralized hardware deployments (e.g. agricultural microclimates, industrial substations, and smart grid automation). In large-scale deployments consisting of thousands of dispersed edge microcontrollers (such as ESP32 chips), standard star-topology Wi-Fi networks suffer from severe transmission range limits, single-point gateway bottlenecks, and radio frequency interference.

To solve these challenges, Smart-X implements a **Decentralized Multi-Hop Mesh Network** where sensor nodes establish peer-to-peer radio interconnections. Data packets dynamically "hop" from sensor to router across the mesh until reaching the central gateway. When intermediate devices suffer power failure or physical obstruction, the network automatically **self-heals** by recalculating power-optimal alternative transmission pathways.

This final PoE release integrates advanced non-linear data structures:
1. **Self-Balancing AVL Trees:** Guarantees strict $O(\log n)$ historical telemetry storage and $O(\log n + k)$ time-window range querying without scanning unorganized lists.
2. **Mesh Graph Topology & Traversal (BFS & DFS):** Models physical ESP32 wireless links and discovers subnet devices, hop levels, and isolated nodes.
3. **Minimum Spanning Tree (MST - Prim's & Kruskal's):** Calculates the global power-optimized wireless routing backbone, achieving over **60% transmission energy reduction** compared to redundant mesh broadcasts.
4. **Interactive 3-Pillar WPF Dashboard:** Fully interconnected desktop client featuring live data ingestion, priority queue stream monitoring, interactive visual graph canvas, and dynamic self-healing simulation.

---

## 🏛️ Architectural Pillars (All 3 Modules Interconnected)

The client-facing dashboard provides an integrated navigation bar hosting all three operational pillars:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        SMART-X IOT MESH GATEWAY CONTROL CONSOLE                        │
├───────────────────────────────┬────────────────────────────────┬───────────────────────┤
│  Pillar 1: Sensor Data        │  Pillar 2: Real-Time Command   │  Pillar 3: Network    │
│  Ingestion & Telemetry        │  Stream & History              │  Topology & Routing   │
│  (Part 1 - Active)            │  (Part 2 - Active)             │  (PoE Final - Active) │
└───────────────────────────────┴────────────────────────────────┴───────────────────────┘
```

1. **Pillar 1: Sensor Data Ingestion & Telemetry (Part 1)**:
   * Low-latency generic packet reception (`TelemetryPacket<T> where T : struct`).
   * Operator overloaded power aggregation (`SmartMeter`).
   * Jagged array stream ingestion (`IngestionBuffer`).
   * Recursive zone deployment validation (`DeploymentNode`).
   * Dynamic visual engagement heatmap indicators (`GREEN_NORMAL` vs `RED_ALERT`).
   * Multipart diagnostic log and deployment photo attachment upload.

2. **Pillar 2: Real-Time Command Stream & History (Part 2)**:
   * Real-Time FIFO Queue ingestion pipeline (`Queue<T>`).
   * High-Priority Alert Queue bypass mechanism (`PriorityQueue<TElement, TPriority>`).
   * Stack-based manual command execution and single-click state reversion (`Stack<T>`).
   * Instantaneous $O(1)$ live device registry and search (`Dictionary<string, LiveDevice>`).
   * Unique error deduplication state tracker (`HashSet<string>`).
   * Predictive recommendation engine generating proactive automation suggestions.

3. **Pillar 3: Network Topology & Mesh Routing Engine (PoE Final)**:
   * **Graph Representation:** Complete mesh topology model with ESP32 vertices and radio link edges weighted by transmission power (mW) and signal quality (RSSI).
   * **Subnet Traversal:** Breadth-First Search (BFS) and Depth-First Search (DFS) for hop-distance mapping and connectivity validation.
   * **Power-Optimized Backbone:** Prim's algorithm (Min-Heap) and Kruskal's algorithm (Disjoint Set Union) computing the Minimum Spanning Tree (MST).
   * **Self-Healing Simulation:** Live toggle of hardware outages causing the mesh to instantly reroute telemetry around failed nodes without data loss.
   * **Historical Data Querying:** Self-balancing AVL Tree enabling instantaneous range lookups (e.g. all moisture readings between 14:00 and 16:00) with diagnostic performance metrics.

---

## 🔬 In-Depth Data Structure Explanations & Code Examples

### 1. Self-Balancing AVL Tree (`AvlTree<TKey, TValue>`)
* **Role in System:** Stores continuous timestamped sensor telemetry (`DateTime` -> `TelemetryRecord`). In standard Binary Search Trees, chronological insertions degrade into a degenerate linked list ($O(n)$ search time). The AVL Tree enforces a balance factor $BF \in \{-1, 0, 1\}$ using single rotations (`LeftRotate`, `RightRotate`) and double rotations (`LeftRightRotate`, `RightLeftRotate`), ensuring strict $O(\log n)$ height.
* **Range Query Efficiency:** To retrieve readings for a specific time window (e.g. past 2 hours), `QueryRange(start, end)` recursively inspects only the branches that can contain matching timestamps, terminating search early on non-overlapping subtrees in $O(\log n + k)$ time where $k$ is the number of matching records.

```csharp
// Example: AVL Tree Range Query Implementation
public List<TValue> QueryRange(TKey startKey, TKey endKey, out int nodesEvaluated)
{
    var results = new List<TValue>();
    int evaluated = 0;
    QueryRangeInternal(_root, startKey, endKey, results, ref evaluated);
    nodesEvaluated = evaluated;
    return results;
}

private void QueryRangeInternal(AvlNode<TKey, TValue>? node, TKey startKey, TKey endKey, List<TValue> results, ref int evaluated)
{
    if (node == null) return;
    evaluated++;

    int startCmp = startKey.CompareTo(node.Key);
    int endCmp = endKey.CompareTo(node.Key);

    // Prune right subtree if startKey is less than current node key
    if (startCmp < 0 && node.Left != null)
        QueryRangeInternal(node.Left, startKey, endKey, results, ref evaluated);

    // Capture matching node values
    if (startCmp <= 0 && endCmp >= 0)
        results.AddRange(node.Values);

    // Prune left subtree if endKey is greater than current node key
    if (endCmp > 0 && node.Right != null)
        QueryRangeInternal(node.Right, startKey, endKey, results, ref evaluated);
}
```

### 2. Mesh Graph Modeling (`MeshGraph`) & Traversal (BFS & DFS)
* **Role in System:** The mesh is represented as an undirected weighted graph $G = (V, E)$, where $V$ represents sensor nodes (hydroponics, weather, climate, substation) and $E$ represents radio links weighted by power consumption (mW).
* **Breadth-First Search (BFS):** Traverses the network level-by-level starting from the Central Gateway Hub (`GATEWAY-ROOT`) to compute the exact minimum hop count for every node and detect isolated devices.

```csharp
// Example: BFS Hop Distance and Subnet Reachability
public TraversalResult BreadthFirstSearch(string startNodeId)
{
    var visited = new HashSet<string> { startNodeId };
    var queue = new Queue<string>();
    var hopDistances = new Dictionary<string, int> { [startNodeId] = 0 };
    queue.Enqueue(startNodeId);

    while (queue.Count > 0)
    {
        string current = queue.Dequeue();
        int currentHop = hopDistances[current];

        foreach (var edge in _adjacencyList[current])
        {
            string neighbor = edge.TargetId;
            if (_vertices[neighbor].IsOnline && !visited.Contains(neighbor))
            {
                visited.Add(neighbor);
                hopDistances[neighbor] = currentHop + 1;
                queue.Enqueue(neighbor);
            }
        }
    }
    // Nodes not in visited are unreachable/isolated
}
```

### 3. Minimum Spanning Tree (MST) — Prim's & Kruskal's Algorithms
* **Role in System:** Broadcast floods across all mesh edges cause packet collisions and exhaust battery reserves on remote edge nodes. The MST determines a subgraph that connects all $V$ active nodes using exactly $V - 1$ edges with minimal total transmission power.
* **Prim's Algorithm (Min-Heap Priority Queue):** Grows the tree outwards from the Gateway by greedily selecting the lowest power edge in $O(E \log V)$ time using `PriorityQueue<MeshEdge, double>`.
* **Kruskal's Algorithm (Disjoint Set Union):** Sorts all candidate edges ascending and adds them if endpoints belong to different disjoint sets, using path compression and union-by-rank to avoid cycles in $O(E \log E)$ time.

```csharp
// Example: Kruskal's MST with Disjoint Set Union
public MstResult CalculateKruskalMst(string rootGatewayId)
{
    var onlineNodes = _vertices.Values.Where(v => v.IsOnline).Select(v => v.Id).ToHashSet();
    var dsu = new DisjointSetUnion(onlineNodes);
    var candidateEdges = _allEdges
        .Where(e => onlineNodes.Contains(e.SourceId) && onlineNodes.Contains(e.TargetId))
        .OrderBy(e => e.Weight)
        .ToList();

    var mstEdges = new List<MeshEdge>();
    foreach (var edge in candidateEdges)
    {
        // Union returns true if no cycle is created
        if (dsu.Union(edge.SourceId, edge.TargetId))
        {
            edge.IsMstEdge = true;
            mstEdges.Add(edge);
            if (mstEdges.Count == onlineNodes.Count - 1) break;
        }
    }
    return new MstResult { TreeEdges = mstEdges, TotalMstPowerCost = mstEdges.Sum(e => e.Weight) };
}
```

---

## 🛠️ Prerequisites & System Requirements

Ensure the following SDKs and developer tools are installed:
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or .NET 8.0 SDK)
* [Visual Studio 2022](https://visualstudio.microsoft.com/) (Version 17.10+ with *.NET Desktop Development* & *ASP.NET and Web Development* workloads)
* [Git](https://git-scm.com/)
* [Docker Desktop](https://www.docker.com/) (Optional, for containerized API execution)

---

## 🚀 Setup, Build, and Run Instructions

### 1. Clone the Solution
```bash
git clone https://github.com/ST10264431/ST10264431PROG7312POEPART1.git
cd SmartX-IoT-Mesh
```

### 2. Restore Project Dependencies
```bash
dotnet restore SmartX-IoT-Mesh.sln
```

### 3. Build the Solution
```bash
dotnet build SmartX-IoT-Mesh.sln --configuration Release
```

### 4. Run Automated Test Suite (19 xUnit Tests)
```bash
dotnet test SmartX.Tests/SmartX.Tests.csproj
```
*All 19 tests verify AVL tree balancing, O(log n + k) range queries, Graph modeling, BFS/DFS traversal, Prim's/Kruskal's MST, and Self-Healing dynamic rerouting.*

---

## 💻 Running the Application Suite Concurrently

### Terminal 1: Launch Backend API Server
```bash
dotnet run --project SmartX.API/SmartX.API.csproj
```
* **API Host**: `http://localhost:5000`
* **Interactive Swagger Documentation**: `http://localhost:5000/swagger`

### Terminal 2: Launch WPF Desktop Control Console
```bash
dotnet run --project SmartX.Dashboard/SmartX.Dashboard.csproj
```

### Quick Run Batch Scripts (Windows)
* Double-click `run_all.bat` to automatically build and launch both the backend API and frontend WPF client in parallel.

---

## 🐳 Docker Container Orchestration

To run the backend API service inside a Docker container:
```bash
# Build and start container in detached mode
docker-compose up --build -d

# Verify container status
docker ps
```
The API is mapped to `http://localhost:5000` with volumes mounting `SmartX.API/UploadedLogs`.

---

## 📡 Complete REST API Endpoint Directory

| Endpoint | Method | Pillar | Description |
| :--- | :---: | :---: | :--- |
| `/api/telemetry` | `POST` | 1 | Ingests generic telemetry packet, evaluates alert state, and records to AVL tree |
| `/api/sensors/{mac}/upload-log` | `POST` | 1 | Multipart file upload for sensor diagnostic logs and photos |
| `/api/devices` | `GET` | 2 | Returns all live devices from Dictionary with category/status filtering |
| `/api/devices/{mac}` | `GET` | 2 | Instantaneous $O(1)$ device lookup |
| `/api/commands` | `POST` | 2 | Issues manual actuator command, pushing to Undo Stack |
| `/api/commands/undo` | `POST` | 2 | Pops and reverses the last issued manual command |
| `/api/telemetry/queue-status` | `GET` | 2 | Real-time queue counters (FIFO, Priority, Stack, Dictionary, HashSet) |
| `/api/telemetry/simulate-alert`| `POST`| 2 | Injects high-priority alert testing priority queue bypass |
| `/api/telemetry/timeline` | `GET` | 2 | Fetches chronologically sorted logs from `SortedDictionary` |
| `/api/topology/graph` | `GET` | 3 | Returns complete mesh graph vertices, edges, and power weights |
| `/api/topology/mst` | `GET` | 3 | Calculates power-optimized Minimum Spanning Tree (Prim's or Kruskal's) |
| `/api/topology/traversal` | `GET` | 3 | Executes BFS or DFS traversal returning reached devices and hop distances |
| `/api/topology/route/{id}` | `GET` | 3 | Computes hop-by-hop transmission route from sensor node to gateway hub |
| `/api/topology/toggle-outage` | `POST` | 3 | Toggles hardware outage on a node and triggers mesh self-healing |
| `/api/history/query-range` | `GET` | 3 | Executes $O(\log n + k)$ range query on AVL Tree across timestamp window |
| `/api/history/all` | `GET` | 3 | Returns all historical records for a sensor node |
| `/api/history/record` | `POST` | 3 | Inserts a new timestamped telemetry record into the node's AVL Tree |
| `/api/history/stats` | `GET` | 3 | Returns record count statistics across all sensor AVL trees |

---

## 🧪 Demonstration Guide for Lecturers

1. **Authentication**:
   * Login using demo credentials: `admin@smartx.io` / `AdminPassword123!` (or click **Auto-Fill**).
2. **Pillar 1 (Telemetry Ingestion)**:
   * Input reading `45.0` -> Heatmap turns Green (`GREEN_NORMAL`).
   * Input reading `95.0` -> Heatmap turns Red (`RED_ALERT`).
   * Attach log/photo and submit -> Persisted in `UploadedLogs`.
3. **Pillar 2 (Real-Time Streams & Priority Bypasses)**:
   * Click **Inject Power Spike Alert** -> Bypasses FIFO queue, priority routed to Priority Alert Queue, recorded in Unique Error HashSet.
   * Trigger water pump on/off -> Pushed to command stack; click **Revert Last Action** to undo.
4. **Pillar 3 (Mesh Topology & AVL Tree Engine)**:
   * Switch between **Full Mesh**, **Power-Optimized MST**, and **Active Hop Path** visual modes.
   * Click **⚡ Prim's MST** or **🌲 Kruskal's MST** -> Observe 60.1% power reduction.
   * Select `ESP32-PUMP-01` -> Observe hop route `ESP32-PUMP-01 ➔ ESP32-HYDRO-01 ➔ GATEWAY-ROOT`.
   * Click **⚡ Simulate Hardware Outage** on `ESP32-HYDRO-01` -> Observe self-healing: route reroutes through `ESP32-ROUTER-01`!
   * In the right panel, select sensor and click **Past 6h** or **🔍 Execute AVL Tree Range Query** -> View diagnostic metrics proving $O(\log n + k)$ search performance.

---

## 📄 License & Attribution
Developed for educational submission in accordance with Independent Institute of Education (IIE) Varsity College standards for the PROG7312 module. All rights reserved © 2026 ST10264431.
