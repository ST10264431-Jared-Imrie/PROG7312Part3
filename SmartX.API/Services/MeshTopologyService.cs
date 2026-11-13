using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SmartX.API.DataStructures;

namespace SmartX.API.Services
{
    public class MeshTopologyService : IMeshTopologyService
    {
        private readonly MeshGraph _graph = new();
        private readonly ConcurrentDictionary<string, AvlTree<DateTime, TelemetryRecord>> _historyTrees = new();
        private readonly string _gatewayRootId = "GATEWAY-ROOT";

        public MeshGraph Graph => _graph;
        public string GatewayRootId => _gatewayRootId;

        public MeshTopologyService()
        {
            InitializeTopology();
            SeedHistoricalTelemetry();
        }

        private void InitializeTopology()
        {
            // -------------------------------------------------------------
            // 1. VERTICES (ESP32 IoT Mesh Nodes & Central Gateway)
            // -------------------------------------------------------------
            // Gateway Coordinator Hub (Central Server)
            _graph.AddVertex(new MeshVertex("GATEWAY-ROOT", "00:00:00:00:00:01", "Central Gateway Hub", "Sector-7 / Central HQ", MeshNodeType.GatewayCoordinator, 450, 40, 100.0, -35));

            // Tier 1 Mesh Routers / Primary Nodes
            _graph.AddVertex(new MeshVertex("ESP32-HYDRO-01", "00:1A:2C:3F:4E:56", "ESP32 Hydroponics Sensor", "Greenhouse Sub-Zone A", MeshNodeType.MeshRouter, 180, 150, 92.5, -55));
            _graph.AddVertex(new MeshVertex("ESP32-ROUTER-01", "12:34:56:78:9A:BC", "Substation Transformer Router", "Substation Alpha", MeshNodeType.MeshRouter, 450, 170, 98.0, -48));
            _graph.AddVertex(new MeshVertex("ESP32-CLIMATE-01", "1A:2B:3C:4D:5E:6F", "Greenhouse Climate Node", "Greenhouse Sub-Zone B", MeshNodeType.MeshRouter, 720, 150, 88.0, -58));

            // Tier 2 Intermediate & Sensor Nodes
            _graph.AddVertex(new MeshVertex("ESP32-SOIL-02", "22:33:44:55:66:77", "Farmland Soil Moisture Sensor", "North Quad Orchard", MeshNodeType.SensorLeaf, 100, 290, 84.0, -68));
            _graph.AddVertex(new MeshVertex("ESP32-PUMP-01", "00:1A:2C:3F:4E:57", "Irrigation Water Pump Alpha", "Pump House Station 1", MeshNodeType.ActuatorEndpoint, 300, 300, 95.0, -62));
            _graph.AddVertex(new MeshVertex("ESP32-SOLAR-01", "33:44:55:66:77:88", "Solar Photovoltaic Inverter", "Solar Array West", MeshNodeType.SensorLeaf, 600, 300, 99.0, -60));
            _graph.AddVertex(new MeshVertex("ESP32-WEATHER-01", "44:55:66:77:88:99", "Edge Weather Station", "Weather Mast 01", MeshNodeType.SensorLeaf, 800, 290, 76.0, -72));

            // Tier 3 Redundant Leaf / Actuator
            _graph.AddVertex(new MeshVertex("ESP32-RELAY-02", "55:66:77:88:99:AA", "Auxiliary Safety Breaker Relay", "Auxiliary Substation", MeshNodeType.ActuatorEndpoint, 450, 420, 91.0, -64));

            // -------------------------------------------------------------
            // 2. EDGES (Wireless Mesh Radio Interconnections with Power Weights)
            // Weight = Power transmission cost in milliwatts (mW)
            // Lower weight = more power-efficient direct transmission link
            // -------------------------------------------------------------
            // Direct Links to Central Gateway Hub
            _graph.AddEdge("GATEWAY-ROOT", "ESP32-HYDRO-01", 18.5, -55, 8.0);
            _graph.AddEdge("GATEWAY-ROOT", "ESP32-ROUTER-01", 12.0, -48, 5.0);
            _graph.AddEdge("GATEWAY-ROOT", "ESP32-CLIMATE-01", 21.0, -58, 9.5);

            // Tier 1 Inter-Router Mesh Cross-Links (Provides self-healing redundancy!)
            _graph.AddEdge("ESP32-HYDRO-01", "ESP32-ROUTER-01", 16.0, -52, 6.5);
            _graph.AddEdge("ESP32-ROUTER-01", "ESP32-CLIMATE-01", 17.5, -54, 7.0);

            // Links to Tier 2 Nodes
            _graph.AddEdge("ESP32-HYDRO-01", "ESP32-SOIL-02", 14.0, -60, 6.0);
            _graph.AddEdge("ESP32-HYDRO-01", "ESP32-PUMP-01", 15.5, -58, 7.0);
            _graph.AddEdge("ESP32-ROUTER-01", "ESP32-PUMP-01", 22.0, -65, 11.0); // Alternative redundant link
            _graph.AddEdge("ESP32-ROUTER-01", "ESP32-SOLAR-01", 15.0, -56, 6.5);
            _graph.AddEdge("ESP32-CLIMATE-01", "ESP32-SOLAR-01", 24.0, -67, 12.5); // Alternative redundant link
            _graph.AddEdge("ESP32-CLIMATE-01", "ESP32-WEATHER-01", 16.5, -61, 7.5);

            // Cross-Cluster Redundant Edges (Self-healing failover pathways)
            _graph.AddEdge("ESP32-SOIL-02", "ESP32-PUMP-01", 28.0, -75, 15.0);
            _graph.AddEdge("ESP32-SOLAR-01", "ESP32-WEATHER-01", 26.0, -73, 14.0);

            // Links to Tier 3 Relay
            _graph.AddEdge("ESP32-PUMP-01", "ESP32-RELAY-02", 19.0, -63, 8.5);
            _graph.AddEdge("ESP32-SOLAR-01", "ESP32-RELAY-02", 18.0, -62, 8.0);
        }

        private void SeedHistoricalTelemetry()
        {
            var now = DateTime.UtcNow;
            var random = new Random(10264431); // Seeded for consistent realistic PoE demonstration data

            var sensorConfigs = new[]
            {
                new { Mac = "00:1A:2C:3F:4E:56", Name = "ESP32 Hydroponics Sensor", Cat = "Hydroponics", BaseVal = 42.0, Unit = "%", Range = 8.0 },
                new { Mac = "12:34:56:78:9A:BC", Name = "Substation Transformer Router", Cat = "Power", BaseVal = 2200.0, Unit = "W", Range = 450.0 },
                new { Mac = "1A:2B:3C:4D:5E:6F", Name = "Greenhouse Climate Node", Cat = "Environmental", BaseVal = 24.5, Unit = "°C", Range = 3.5 },
                new { Mac = "22:33:44:55:66:77", Name = "Farmland Soil Moisture Sensor", Cat = "Environmental", BaseVal = 38.0, Unit = "%", Range = 12.0 },
                new { Mac = "33:44:55:66:77:88", Name = "Solar Photovoltaic Inverter", Cat = "Power", BaseVal = 4800.0, Unit = "W", Range = 1200.0 },
                new { Mac = "44:55:66:77:88:99", Name = "Edge Weather Station", Cat = "Environmental", BaseVal = 18.0, Unit = "km/h", Range = 6.0 }
            };

            foreach (var cfg in sensorConfigs)
            {
                var tree = _historyTrees.GetOrAdd(cfg.Mac, _ => new AvlTree<DateTime, TelemetryRecord>());

                // Seed 50 historical points over the last 24 hours at 30-minute intervals
                for (int i = 48; i >= 0; i--)
                {
                    var timestamp = now.AddMinutes(-30 * i);
                    double jitter = (random.NextDouble() * 2.0 - 1.0) * cfg.Range;
                    double val = Math.Round(cfg.BaseVal + jitter, 2);

                    string status = "NORMAL";
                    if (cfg.Cat == "Power" && val > 3000.0) status = "RED_ALERT";
                    if (cfg.Cat == "Hydroponics" && val < 20.0) status = "RED_ALERT";

                    var record = new TelemetryRecord(cfg.Mac, cfg.Name, cfg.Cat, timestamp, val, cfg.Unit, status);
                    tree.Insert(timestamp, record);
                }
            }
        }

        public MeshGraph GetGraph() => _graph;

        public MstResult CalculateMst(bool useKruskal = false)
        {
            return useKruskal
                ? _graph.CalculateKruskalMst(_gatewayRootId)
                : _graph.CalculatePrimMst(_gatewayRootId);
        }

        public TraversalResult RunTraversal(string algorithm = "BFS", string? startNodeId = null)
        {
            string start = string.IsNullOrWhiteSpace(startNodeId) ? _gatewayRootId : startNodeId;
            return algorithm.Equals("DFS", StringComparison.OrdinalIgnoreCase)
                ? _graph.DepthFirstSearch(start)
                : _graph.BreadthFirstSearch(start);
        }

        public List<string> GetRoutingPath(string macOrNodeId)
        {
            // Find vertex matching either NodeId or MacAddress
            var vertex = _graph.Vertices.Values.FirstOrDefault(v =>
                v.Id.Equals(macOrNodeId, StringComparison.OrdinalIgnoreCase) ||
                v.MacAddress.Equals(macOrNodeId, StringComparison.OrdinalIgnoreCase));

            if (vertex == null) return new List<string>();

            return _graph.GetShortestPathToGateway(vertex.Id, _gatewayRootId);
        }

        public bool ToggleNodeStatus(string nodeId, out bool newStatus, out string message)
        {
            var vertex = _graph.Vertices.Values.FirstOrDefault(v =>
                v.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase) ||
                v.MacAddress.Equals(nodeId, StringComparison.OrdinalIgnoreCase));

            if (vertex == null)
            {
                newStatus = false;
                message = $"Node '{nodeId}' not found in mesh graph.";
                return false;
            }

            bool targetStatus = !vertex.IsOnline;
            _graph.SetNodeStatus(vertex.Id, targetStatus);
            newStatus = targetStatus;

            message = targetStatus
                ? $"Node '{vertex.DeviceName}' ({vertex.Id}) restored to ONLINE status. Active mesh topology updated."
                : $"SIMULATED HARDWARE OUTAGE: Node '{vertex.DeviceName}' ({vertex.Id}) is now OFFLINE. Mesh self-healing triggered!";

            return true;
        }

        public void RecordTelemetry(TelemetryRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.MacAddress)) return;

            var tree = _historyTrees.GetOrAdd(record.MacAddress, _ => new AvlTree<DateTime, TelemetryRecord>());
            tree.Insert(record.Timestamp, record);
        }

        public List<TelemetryRecord> QueryHistoricalRange(string macAddress, DateTime startTime, DateTime endTime, out int nodesEvaluated, out int treeHeight)
        {
            if (_historyTrees.TryGetValue(macAddress, out var tree))
            {
                treeHeight = tree.Height;
                return tree.QueryRange(startTime, endTime, out nodesEvaluated);
            }

            nodesEvaluated = 0;
            treeHeight = 0;
            return new List<TelemetryRecord>();
        }

        public List<TelemetryRecord> GetAllHistory(string macAddress)
        {
            if (_historyTrees.TryGetValue(macAddress, out var tree))
            {
                return tree.ToList();
            }
            return new List<TelemetryRecord>();
        }

        public Dictionary<string, int> GetHistoryStats()
        {
            return _historyTrees.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }
    }
}
