using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartX.API.DataStructures
{
    public enum MeshNodeType
    {
        GatewayCoordinator, // Central Gateway Root
        MeshRouter,         // Intermediate forwarding node
        SensorLeaf,         // Sensor data producer
        ActuatorEndpoint    // Relays / pumps
    }

    /// <summary>
    /// Vertex representing a physical or logical IoT mesh sensor node in the topology graph.
    /// </summary>
    public class MeshVertex
    {
        public string Id { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public MeshNodeType NodeType { get; set; } = MeshNodeType.SensorLeaf;
        public bool IsOnline { get; set; } = true;
        public double PositionX { get; set; } // Canvas X coordinate
        public double PositionY { get; set; } // Canvas Y coordinate
        public double BatteryPercentage { get; set; } = 100.0;
        public int SignalDbm { get; set; } = -60;

        public MeshVertex() { }

        public MeshVertex(string id, string macAddress, string deviceName, string zone, MeshNodeType nodeType, double x, double y, double battery = 100.0, int signalDbm = -60)
        {
            Id = id;
            MacAddress = macAddress;
            DeviceName = deviceName;
            Zone = zone;
            NodeType = nodeType;
            PositionX = x;
            PositionY = y;
            BatteryPercentage = battery;
            SignalDbm = signalDbm;
            IsOnline = true;
        }
    }

    /// <summary>
    /// Edge representing a wireless radio link (ESP-NOW, Zigbee, Thread) between two mesh nodes.
    /// Weight represents transmission power consumption in milliwatts (mW) or link latency.
    /// </summary>
    public class MeshEdge : IComparable<MeshEdge>
    {
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public double Weight { get; set; } // Power cost in milliwatts (mW)
        public int Rssi { get; set; } = -65; // Signal strength in dBm
        public double LatencyMs { get; set; } = 12.0;
        public bool IsMstEdge { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public MeshEdge() { }

        public MeshEdge(string sourceId, string targetId, double weight, int rssi = -65, double latencyMs = 12.0)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Weight = weight;
            Rssi = rssi;
            LatencyMs = latencyMs;
            IsMstEdge = false;
            IsActive = true;
        }

        public int CompareTo(MeshEdge? other)
        {
            if (other == null) return 1;
            return Weight.CompareTo(other.Weight);
        }

        public override string ToString()
        {
            return $"{SourceId} <--> {TargetId} ({Weight:F1} mW, {Rssi} dBm)";
        }
    }

    /// <summary>
    /// Disjoint Set Union (DSU) / Union-Find data structure with Path Compression and Union by Rank
    /// for Kruskal's Minimum Spanning Tree algorithm.
    /// </summary>
    public class DisjointSetUnion
    {
        private readonly Dictionary<string, string> _parent = new();
        private readonly Dictionary<string, int> _rank = new();

        public DisjointSetUnion(IEnumerable<string> elements)
        {
            foreach (var item in elements)
            {
                _parent[item] = item;
                _rank[item] = 0;
            }
        }

        public string Find(string node)
        {
            if (!_parent.ContainsKey(node))
            {
                _parent[node] = node;
                _rank[node] = 0;
                return node;
            }

            if (_parent[node] != node)
            {
                // Path compression: flatten tree directly to representative root
                _parent[node] = Find(_parent[node]);
            }
            return _parent[node];
        }

        public bool Union(string a, string b)
        {
            string rootA = Find(a);
            string rootB = Find(b);

            if (rootA == rootB)
            {
                return false; // Cycle detected
            }

            // Union by rank: attach smaller depth tree under root of higher rank tree
            if (_rank[rootA] < _rank[rootB])
            {
                _parent[rootA] = rootB;
            }
            else if (_rank[rootA] > _rank[rootB])
            {
                _parent[rootB] = rootA;
            }
            else
            {
                _parent[rootB] = rootA;
                _rank[rootA]++;
            }

            return true;
        }
    }

    /// <summary>
    /// Result payload for Minimum Spanning Tree computations.
    /// </summary>
    public class MstResult
    {
        public List<MeshEdge> TreeEdges { get; set; } = new();
        public double TotalMstPowerCost { get; set; }
        public double TotalMeshPowerCost { get; set; }
        public double PowerSavingsPercent { get; set; }
        public Dictionary<string, List<string>> HopRoutes { get; set; } = new();
        public List<string> ConnectedNodeIds { get; set; } = new();
        public List<string> UnreachableNodeIds { get; set; } = new();
        public string AlgorithmUsed { get; set; } = "Prim's (Min-Heap Priority Queue)";
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result payload for Graph Traversal operations (BFS/DFS).
    /// </summary>
    public class TraversalResult
    {
        public string Algorithm { get; set; } = "BFS";
        public string StartNodeId { get; set; } = string.Empty;
        public List<string> VisitedNodeIds { get; set; } = new();
        public Dictionary<string, int> HopDistances { get; set; } = new();
        public List<string> UnreachableNodeIds { get; set; } = new();
        public int TotalNodesReached => VisitedNodeIds.Count;
        public bool IsFullyConnected { get; set; }
    }

    /// <summary>
    /// Graph data structure modeling the physical and logical multi-hop IoT mesh network.
    /// Vertices represent ESP32 edge sensors, actuators, and routers.
    /// Edges represent wireless links with power transmission costs.
    /// Supports BFS, DFS, Kruskal's MST, Prim's MST, and dynamic self-healing rerouting.
    /// </summary>
    public class MeshGraph
    {
        private readonly Dictionary<string, MeshVertex> _vertices = new();
        private readonly Dictionary<string, List<MeshEdge>> _adjacencyList = new();
        private readonly List<MeshEdge> _allEdges = new();
        private readonly object _lock = new();

        public IReadOnlyDictionary<string, MeshVertex> Vertices => _vertices;
        public IReadOnlyList<MeshEdge> Edges => _allEdges.AsReadOnly();

        public int VertexCount => _vertices.Count;
        public int EdgeCount => _allEdges.Count;

        public void AddVertex(MeshVertex vertex)
        {
            lock (_lock)
            {
                if (!_vertices.ContainsKey(vertex.Id))
                {
                    _vertices[vertex.Id] = vertex;
                    _adjacencyList[vertex.Id] = new List<MeshEdge>();
                }
            }
        }

        public void AddEdge(string sourceId, string targetId, double weight, int rssi = -65, double latencyMs = 12.0)
        {
            lock (_lock)
            {
                if (!_vertices.ContainsKey(sourceId) || !_vertices.ContainsKey(targetId))
                {
                    return;
                }

                // Avoid duplicate bidirectional edge additions
                var existing = _allEdges.FirstOrDefault(e =>
                    (e.SourceId == sourceId && e.TargetId == targetId) ||
                    (e.SourceId == targetId && e.TargetId == sourceId));

                if (existing != null) return;

                var edge = new MeshEdge(sourceId, targetId, weight, rssi, latencyMs);
                _allEdges.Add(edge);

                _adjacencyList[sourceId].Add(edge);
                _adjacencyList[targetId].Add(new MeshEdge(targetId, sourceId, weight, rssi, latencyMs));
            }
        }

        /// <summary>
        /// Updates the online/offline operational status of a node (simulates hardware failure or self-healing restoration).
        /// </summary>
        public bool SetNodeStatus(string nodeId, bool isOnline)
        {
            lock (_lock)
            {
                if (_vertices.TryGetValue(nodeId, out var vertex))
                {
                    vertex.IsOnline = isOnline;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Performs Breadth-First Search (BFS) to identify all active devices connected to a subnet/gateway,
        /// compute hop levels, and identify unreachable/down nodes.
        /// </summary>
        public TraversalResult BreadthFirstSearch(string startNodeId)
        {
            lock (_lock)
            {
                var result = new TraversalResult
                {
                    Algorithm = "BFS",
                    StartNodeId = startNodeId
                };

                if (!_vertices.ContainsKey(startNodeId) || !_vertices[startNodeId].IsOnline)
                {
                    result.UnreachableNodeIds = _vertices.Keys.ToList();
                    result.IsFullyConnected = false;
                    return result;
                }

                var visited = new HashSet<string>();
                var queue = new Queue<string>();

                visited.Add(startNodeId);
                queue.Enqueue(startNodeId);
                result.HopDistances[startNodeId] = 0;

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    result.VisitedNodeIds.Add(current);
                    int currentHop = result.HopDistances[current];

                    if (_adjacencyList.TryGetValue(current, out var neighbors))
                    {
                        foreach (var edge in neighbors)
                        {
                            string neighborId = edge.TargetId;
                            // Traverse only if neighbor is online and not yet visited
                            if (_vertices.TryGetValue(neighborId, out var neighborVertex) &&
                                neighborVertex.IsOnline &&
                                !visited.Contains(neighborId))
                            {
                                visited.Add(neighborId);
                                result.HopDistances[neighborId] = currentHop + 1;
                                queue.Enqueue(neighborId);
                            }
                        }
                    }
                }

                // Identify isolated or offline nodes
                foreach (var kvp in _vertices)
                {
                    if (!visited.Contains(kvp.Key))
                    {
                        result.UnreachableNodeIds.Add(kvp.Key);
                    }
                }

                result.IsFullyConnected = result.UnreachableNodeIds.Count == 0;
                return result;
            }
        }

        /// <summary>
        /// Performs Depth-First Search (DFS) traversal to detect network topology paths and cycle presence.
        /// </summary>
        public TraversalResult DepthFirstSearch(string startNodeId)
        {
            lock (_lock)
            {
                var result = new TraversalResult
                {
                    Algorithm = "DFS",
                    StartNodeId = startNodeId
                };

                if (!_vertices.ContainsKey(startNodeId) || !_vertices[startNodeId].IsOnline)
                {
                    result.UnreachableNodeIds = _vertices.Keys.ToList();
                    result.IsFullyConnected = false;
                    return result;
                }

                var visited = new HashSet<string>();
                DfsRecursive(startNodeId, visited, result.VisitedNodeIds, result.HopDistances, 0);

                foreach (var kvp in _vertices)
                {
                    if (!visited.Contains(kvp.Key))
                    {
                        result.UnreachableNodeIds.Add(kvp.Key);
                    }
                }

                result.IsFullyConnected = result.UnreachableNodeIds.Count == 0;
                return result;
            }
        }

        private void DfsRecursive(string current, HashSet<string> visited, List<string> order, Dictionary<string, int> hops, int depth)
        {
            visited.Add(current);
            order.Add(current);
            hops[current] = depth;

            if (_adjacencyList.TryGetValue(current, out var neighbors))
            {
                foreach (var edge in neighbors)
                {
                    string neighborId = edge.TargetId;
                    if (_vertices.TryGetValue(neighborId, out var neighborVertex) &&
                        neighborVertex.IsOnline &&
                        !visited.Contains(neighborId))
                    {
                        DfsRecursive(neighborId, visited, order, hops, depth + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the Minimum Spanning Tree (MST) using Prim's Algorithm with a Min-Heap (PriorityQueue).
        /// Replicates power-efficient routing protocols (Zigbee/Thread) ensuring optimal energy consumption.
        /// </summary>
        public MstResult CalculatePrimMst(string rootGatewayId)
        {
            lock (_lock)
            {
                var result = new MstResult
                {
                    AlgorithmUsed = "Prim's Algorithm (Min-Heap Priority Queue)"
                };

                var onlineNodes = _vertices.Values.Where(v => v.IsOnline).Select(v => v.Id).ToHashSet();
                if (!onlineNodes.Contains(rootGatewayId))
                {
                    // If root is offline, return empty MST
                    result.UnreachableNodeIds = _vertices.Keys.ToList();
                    return result;
                }

                var inMst = new HashSet<string>();
                var priorityQueue = new PriorityQueue<MeshEdge, double>();
                var mstEdges = new List<MeshEdge>();

                // Total mesh power consumption without MST optimisation
                double totalMeshPower = _allEdges.Where(e =>
                    _vertices[e.SourceId].IsOnline && _vertices[e.TargetId].IsOnline
                ).Sum(e => e.Weight);

                inMst.Add(rootGatewayId);

                // Add all incident edges of root into Min-Heap
                if (_adjacencyList.TryGetValue(rootGatewayId, out var rootEdges))
                {
                    foreach (var edge in rootEdges)
                    {
                        if (onlineNodes.Contains(edge.TargetId))
                        {
                            priorityQueue.Enqueue(edge, edge.Weight);
                        }
                    }
                }

                while (priorityQueue.Count > 0 && inMst.Count < onlineNodes.Count)
                {
                    var minEdge = priorityQueue.Dequeue();
                    string nextNode = minEdge.TargetId;

                    if (inMst.Contains(nextNode)) continue;

                    inMst.Add(nextNode);
                    minEdge.IsMstEdge = true;
                    mstEdges.Add(minEdge);

                    // Add adjacent edges of newly incorporated node into priority queue
                    if (_adjacencyList.TryGetValue(nextNode, out var incidentEdges))
                    {
                        foreach (var edge in incidentEdges)
                        {
                            if (onlineNodes.Contains(edge.TargetId) && !inMst.Contains(edge.TargetId))
                            {
                                priorityQueue.Enqueue(edge, edge.Weight);
                            }
                        }
                    }
                }

                result.TreeEdges = mstEdges;
                result.TotalMstPowerCost = mstEdges.Sum(e => e.Weight);
                result.TotalMeshPowerCost = totalMeshPower > 0 ? totalMeshPower : result.TotalMstPowerCost;
                result.PowerSavingsPercent = result.TotalMeshPowerCost > 0
                    ? Math.Round((1.0 - (result.TotalMstPowerCost / result.TotalMeshPowerCost)) * 100.0, 1)
                    : 0.0;

                result.ConnectedNodeIds = inMst.ToList();
                result.UnreachableNodeIds = _vertices.Keys.Where(k => !inMst.Contains(k)).ToList();

                // Build hop routing table back to gateway along MST
                result.HopRoutes = BuildRoutingTableFromMst(rootGatewayId, mstEdges);

                return result;
            }
        }

        /// <summary>
        /// Calculates the Minimum Spanning Tree (MST) using Kruskal's Algorithm with Disjoint Set Union (DSU).
        /// </summary>
        public MstResult CalculateKruskalMst(string rootGatewayId)
        {
            lock (_lock)
            {
                var result = new MstResult
                {
                    AlgorithmUsed = "Kruskal's Algorithm (Disjoint Set Union)"
                };

                var onlineNodes = _vertices.Values.Where(v => v.IsOnline).Select(v => v.Id).ToHashSet();
                var dsu = new DisjointSetUnion(onlineNodes);

                // Sort all active edges by weight ascending (Greedy Choice)
                var candidateEdges = _allEdges
                    .Where(e => onlineNodes.Contains(e.SourceId) && onlineNodes.Contains(e.TargetId))
                    .OrderBy(e => e.Weight)
                    .ToList();

                double totalMeshPower = candidateEdges.Sum(e => e.Weight);
                var mstEdges = new List<MeshEdge>();

                foreach (var edge in candidateEdges)
                {
                    // If endpoints are in different sets, union them (no cycle created)
                    if (dsu.Union(edge.SourceId, edge.TargetId))
                    {
                        edge.IsMstEdge = true;
                        mstEdges.Add(edge);
                        if (mstEdges.Count == onlineNodes.Count - 1)
                        {
                            break; // Spanning tree complete
                        }
                    }
                }

                result.TreeEdges = mstEdges;
                result.TotalMstPowerCost = mstEdges.Sum(e => e.Weight);
                result.TotalMeshPowerCost = totalMeshPower > 0 ? totalMeshPower : result.TotalMstPowerCost;
                result.PowerSavingsPercent = result.TotalMeshPowerCost > 0
                    ? Math.Round((1.0 - (result.TotalMstPowerCost / result.TotalMeshPowerCost)) * 100.0, 1)
                    : 0.0;

                var connected = new HashSet<string>();
                foreach (var edge in mstEdges)
                {
                    connected.Add(edge.SourceId);
                    connected.Add(edge.TargetId);
                }
                if (onlineNodes.Contains(rootGatewayId)) connected.Add(rootGatewayId);

                result.ConnectedNodeIds = connected.ToList();
                result.UnreachableNodeIds = _vertices.Keys.Where(k => !connected.Contains(k)).ToList();

                result.HopRoutes = BuildRoutingTableFromMst(rootGatewayId, mstEdges);
                return result;
            }
        }

        /// <summary>
        /// Constructs a lookup dictionary mapping each node to its ordered multi-hop path back to the Gateway.
        /// </summary>
        private Dictionary<string, List<string>> BuildRoutingTableFromMst(string rootGatewayId, List<MeshEdge> mstEdges)
        {
            var routes = new Dictionary<string, List<string>>();
            var mstAdj = new Dictionary<string, List<string>>();

            foreach (var edge in mstEdges)
            {
                if (!mstAdj.ContainsKey(edge.SourceId)) mstAdj[edge.SourceId] = new List<string>();
                if (!mstAdj.ContainsKey(edge.TargetId)) mstAdj[edge.TargetId] = new List<string>();

                mstAdj[edge.SourceId].Add(edge.TargetId);
                mstAdj[edge.TargetId].Add(edge.SourceId);
            }

            // For each node, find path to gateway using BFS on the MST tree
            foreach (var node in _vertices.Keys)
            {
                if (node == rootGatewayId)
                {
                    routes[node] = new List<string> { rootGatewayId };
                    continue;
                }

                var path = FindTreePath(node, rootGatewayId, mstAdj);
                if (path != null)
                {
                    routes[node] = path;
                }
            }

            return routes;
        }

        private List<string>? FindTreePath(string start, string target, Dictionary<string, List<string>> treeAdj)
        {
            if (!treeAdj.ContainsKey(start) || !treeAdj.ContainsKey(target)) return null;

            var queue = new Queue<string>();
            var parent = new Dictionary<string, string>();
            var visited = new HashSet<string>();

            queue.Enqueue(start);
            visited.Add(start);

            bool found = false;
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (curr == target)
                {
                    found = true;
                    break;
                }

                if (treeAdj.TryGetValue(curr, out var neighbors))
                {
                    foreach (var next in neighbors)
                    {
                        if (!visited.Contains(next))
                        {
                            visited.Add(next);
                            parent[next] = curr;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            if (!found) return null;

            // Reconstruct path from start to target
            var path = new List<string>();
            string step = target;
            while (step != start)
            {
                path.Add(step);
                step = parent[step];
            }
            path.Add(start);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Finds the active shortest routing path (hop-by-hop) from a specific sensor node to the Gateway.
        /// If intermediate routers are down, self-heals by routing around failed nodes.
        /// </summary>
        public List<string> GetShortestPathToGateway(string sensorNodeId, string gatewayNodeId)
        {
            lock (_lock)
            {
                var path = new List<string>();
                if (!_vertices.ContainsKey(sensorNodeId) || !_vertices[sensorNodeId].IsOnline)
                {
                    return path;
                }

                // Dijkstra's algorithm for minimum power path
                var distances = new Dictionary<string, double>();
                var previous = new Dictionary<string, string>();
                var priorityQueue = new PriorityQueue<string, double>();

                foreach (var v in _vertices.Keys)
                {
                    distances[v] = double.PositiveInfinity;
                }
                distances[sensorNodeId] = 0;
                priorityQueue.Enqueue(sensorNodeId, 0);

                while (priorityQueue.Count > 0)
                {
                    string current = priorityQueue.Dequeue();
                    if (current == gatewayNodeId) break;

                    if (_adjacencyList.TryGetValue(current, out var edges))
                    {
                        foreach (var edge in edges)
                        {
                            string neighbor = edge.TargetId;
                            if (!_vertices[neighbor].IsOnline) continue; // Skip offline nodes (Self-Healing)

                            double newDist = distances[current] + edge.Weight;
                            if (newDist < distances[neighbor])
                            {
                                distances[neighbor] = newDist;
                                previous[neighbor] = current;
                                priorityQueue.Enqueue(neighbor, newDist);
                            }
                        }
                    }
                }

                if (!previous.ContainsKey(gatewayNodeId) && sensorNodeId != gatewayNodeId)
                {
                    return path; // No route available (isolated)
                }

                string currStep = gatewayNodeId;
                while (currStep != sensorNodeId)
                {
                    path.Add(currStep);
                    currStep = previous[currStep];
                }
                path.Add(sensorNodeId);
                path.Reverse();
                return path;
            }
        }
    }
}
