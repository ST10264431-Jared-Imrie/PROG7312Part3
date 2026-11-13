using System;
using System.Collections.Generic;
using System.Linq;
using SmartX.API.DataStructures;
using SmartX.API.Services;
using Xunit;

namespace SmartX.Tests
{
    public class PoEPart3TopologyTests
    {
        #region 1. AVL Tree Tests (Self-Balancing Binary Search Tree for Historical Telemetry)

        [Fact]
        public void AvlTree_BalancesProperly_AndMaintainsLogarithmicHeight()
        {
            // Arrange
            var avlTree = new AvlTree<DateTime, TelemetryRecord>();
            var baseTime = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

            // Act: Insert strictly sequential timestamps (would cause an unbalanced linked-list of height 31 in standard BST)
            for (int i = 0; i < 31; i++)
            {
                var dt = baseTime.AddMinutes(i * 10);
                var record = new TelemetryRecord("00:1A:2C:3F:4E:56", "Hydro Node", "Hydroponics", dt, 40.0 + i, "%");
                avlTree.Insert(dt, record);
            }

            // Assert: For 31 nodes, AVL height must be <= ceil(1.44 * log2(32)) = 6 (ideal complete binary tree height = 5)
            Assert.Equal(31, avlTree.Count);
            Assert.True(avlTree.Height <= 6, $"AVL Tree height was {avlTree.Height}, expected <= 6 due to balancing rotations.");
        }

        [Fact]
        public void AvlTree_QueryRange_EfficientlyRetrievesDataWindow_WithoutFullListScan()
        {
            // Arrange
            var avlTree = new AvlTree<DateTime, TelemetryRecord>();
            var baseTime = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);

            // Insert 100 historical readings at 15-minute intervals across 25 hours
            for (int i = 0; i < 100; i++)
            {
                var dt = baseTime.AddMinutes(i * 15);
                var record = new TelemetryRecord("00:1A:2C:3F:4E:56", "Hydro Sensor", "Hydroponics", dt, 20.0 + (i % 30), "%");
                avlTree.Insert(dt, record);
            }

            // Target window: between 14:00 (minute 840) and 16:00 (minute 960) -> 840, 855, 870, 885, 900, 915, 930, 945, 960 (9 records)
            var startRange = baseTime.AddHours(14);
            var endRange = baseTime.AddHours(16);

            // Act: Execute range query
            var results = avlTree.QueryRange(startRange, endRange, out int nodesEvaluated);

            // Assert
            Assert.Equal(9, results.Count);
            Assert.All(results, r => Assert.True(r.Timestamp >= startRange && r.Timestamp <= endRange));

            // Verify O(log n + k) complexity: nodes evaluated should be vastly less than 100 (total nodes in tree)
            Assert.True(nodesEvaluated < 40, $"Evaluated {nodesEvaluated} nodes; expected targeted search path rather than scanning all 100 nodes.");
        }

        [Fact]
        public void AvlTree_InOrderTraversal_ReturnsStrictChronologicalOrder()
        {
            // Arrange
            var avlTree = new AvlTree<DateTime, TelemetryRecord>();
            var now = DateTime.UtcNow;

            // Insert out of chronological order
            avlTree.Insert(now.AddHours(-3), new TelemetryRecord("00:01", "Sensor", "Temp", now.AddHours(-3), 20.0, "C"));
            avlTree.Insert(now.AddHours(-1), new TelemetryRecord("00:01", "Sensor", "Temp", now.AddHours(-1), 22.0, "C"));
            avlTree.Insert(now.AddHours(-4), new TelemetryRecord("00:01", "Sensor", "Temp", now.AddHours(-4), 19.0, "C"));
            avlTree.Insert(now.AddHours(-2), new TelemetryRecord("00:01", "Sensor", "Temp", now.AddHours(-2), 21.0, "C"));

            // Act
            var sortedList = avlTree.ToList();

            // Assert
            Assert.Equal(4, sortedList.Count);
            for (int i = 0; i < sortedList.Count - 1; i++)
            {
                Assert.True(sortedList[i].Timestamp <= sortedList[i + 1].Timestamp, "AVL in-order traversal was not sorted ascending!");
            }
        }

        #endregion

        #region 2. Graph & Traversal Tests (Mesh Modeling, BFS, DFS)

        [Fact]
        public void MeshGraph_ModelsVerticesAndEdges_AccuratelyInAdjacencyList()
        {
            // Arrange
            var graph = new MeshGraph();
            graph.AddVertex(new MeshVertex("GW", "00:00", "Gateway", "HQ", MeshNodeType.GatewayCoordinator, 0, 0));
            graph.AddVertex(new MeshVertex("NODE_A", "00:01", "Node A", "Zone 1", MeshNodeType.MeshRouter, 10, 10));
            graph.AddVertex(new MeshVertex("NODE_B", "00:02", "Node B", "Zone 2", MeshNodeType.SensorLeaf, 20, 20));

            // Act
            graph.AddEdge("GW", "NODE_A", 15.0, -50, 5.0);
            graph.AddEdge("NODE_A", "NODE_B", 25.0, -65, 10.0);

            // Assert
            Assert.Equal(3, graph.VertexCount);
            Assert.Equal(2, graph.EdgeCount);
            Assert.True(graph.Vertices.ContainsKey("GW"));
            Assert.True(graph.Vertices.ContainsKey("NODE_A"));
            Assert.True(graph.Vertices.ContainsKey("NODE_B"));
        }

        [Fact]
        public void BreadthFirstSearch_DiscoversSubnetDevices_AndComputesHopDistances()
        {
            // Arrange
            var service = new MeshTopologyService();
            var graph = service.Graph;

            // Act: BFS from Central Gateway
            var bfsResult = graph.BreadthFirstSearch("GATEWAY-ROOT");

            // Assert
            Assert.Equal("BFS", bfsResult.Algorithm);
            Assert.Equal("GATEWAY-ROOT", bfsResult.StartNodeId);
            Assert.True(bfsResult.IsFullyConnected);
            Assert.Equal(graph.VertexCount, bfsResult.TotalNodesReached);

            // Verify Hop Distances
            Assert.Equal(0, bfsResult.HopDistances["GATEWAY-ROOT"]);
            Assert.Equal(1, bfsResult.HopDistances["ESP32-HYDRO-01"]);
            Assert.Equal(1, bfsResult.HopDistances["ESP32-ROUTER-01"]);
            Assert.True(bfsResult.HopDistances["ESP32-SOIL-02"] >= 2);
        }

        [Fact]
        public void BreadthFirstSearch_IdentifiesIsolatedAndDownNodes_WhenOutageOccurs()
        {
            // Arrange
            var graph = new MeshGraph();
            graph.AddVertex(new MeshVertex("GW", "00:00", "Gateway", "HQ", MeshNodeType.GatewayCoordinator, 0, 0));
            graph.AddVertex(new MeshVertex("ROUTER_1", "00:01", "Router", "Zone 1", MeshNodeType.MeshRouter, 10, 10));
            graph.AddVertex(new MeshVertex("LEAF_1", "00:02", "Leaf", "Zone 2", MeshNodeType.SensorLeaf, 20, 20));

            graph.AddEdge("GW", "ROUTER_1", 10.0);
            graph.AddEdge("ROUTER_1", "LEAF_1", 10.0);

            // Act: Simulate ROUTER_1 going down
            graph.SetNodeStatus("ROUTER_1", isOnline: false);
            var bfsResult = graph.BreadthFirstSearch("GW");

            // Assert
            Assert.False(bfsResult.IsFullyConnected);
            Assert.Contains("ROUTER_1", bfsResult.UnreachableNodeIds);
            Assert.Contains("LEAF_1", bfsResult.UnreachableNodeIds); // LEAF_1 was isolated
        }

        [Fact]
        public void DepthFirstSearch_ExploresDeepPaths_AndReturnsVisitedOrder()
        {
            // Arrange
            var service = new MeshTopologyService();
            var graph = service.Graph;

            // Act
            var dfsResult = graph.DepthFirstSearch("GATEWAY-ROOT");

            // Assert
            Assert.Equal("DFS", dfsResult.Algorithm);
            Assert.Equal(graph.VertexCount, dfsResult.TotalNodesReached);
            Assert.True(dfsResult.IsFullyConnected);
        }

        #endregion

        #region 3. Minimum Spanning Tree (MST - Prim's & Kruskal's Algorithms)

        [Fact]
        public void PrimMst_ConstructsPowerOptimizedBackbone_WithVMinusOneEdges()
        {
            // Arrange
            var service = new MeshTopologyService();
            var graph = service.Graph;
            int onlineVertexCount = graph.Vertices.Values.Count(v => v.IsOnline);

            // Act
            var mst = graph.CalculatePrimMst("GATEWAY-ROOT");

            // Assert: Spanning tree of V vertices must have exactly V - 1 edges
            Assert.Equal(onlineVertexCount - 1, mst.TreeEdges.Count);
            Assert.True(mst.TotalMstPowerCost < mst.TotalMeshPowerCost,
                $"MST power cost ({mst.TotalMstPowerCost} mW) should be significantly lower than full mesh broadcast cost ({mst.TotalMeshPowerCost} mW).");
            Assert.True(mst.PowerSavingsPercent > 40.0, $"Expected > 40% power reduction, but was {mst.PowerSavingsPercent}%.");
            Assert.Empty(mst.UnreachableNodeIds);
        }

        [Fact]
        public void KruskalMst_AvoidsCycles_AndAchievesOptimalPowerCost()
        {
            // Arrange
            var service = new MeshTopologyService();
            var graph = service.Graph;
            int onlineVertexCount = graph.Vertices.Values.Count(v => v.IsOnline);

            // Act
            var kruskalMst = graph.CalculateKruskalMst("GATEWAY-ROOT");
            var primMst = graph.CalculatePrimMst("GATEWAY-ROOT");

            // Assert
            Assert.Equal(onlineVertexCount - 1, kruskalMst.TreeEdges.Count);
            // Both Prim's and Kruskal's must produce identical minimal total weight on connected graphs
            Assert.Equal(primMst.TotalMstPowerCost, kruskalMst.TotalMstPowerCost, precision: 2);
        }

        [Fact]
        public void DisjointSetUnion_AccuratelyDetectsCycles_DuringKruskalUnion()
        {
            // Arrange
            var elements = new[] { "A", "B", "C" };
            var dsu = new DisjointSetUnion(elements);

            // Act & Assert
            Assert.True(dsu.Union("A", "B")); // A-B connected
            Assert.True(dsu.Union("B", "C")); // B-C connected (A-B-C connected)
            Assert.False(dsu.Union("A", "C")); // A-C would create a cycle -> must return false!
        }

        #endregion

        #region 4. Self-Healing & Dynamic Rerouting Tests

        [Fact]
        public void MeshGraph_SelfHealsAndReroutesAroundFailedNode()
        {
            // Arrange
            var service = new MeshTopologyService();
            var graph = service.Graph;

            // Target leaf node: ESP32-PUMP-01
            // Normal route typically hops through ESP32-HYDRO-01
            var initialRoute = graph.GetShortestPathToGateway("ESP32-PUMP-01", "GATEWAY-ROOT");
            Assert.NotEmpty(initialRoute);
            Assert.Equal("ESP32-PUMP-01", initialRoute.First());
            Assert.Equal("GATEWAY-ROOT", initialRoute.Last());

            // Act: Simulate failure of the primary intermediate router ESP32-HYDRO-01
            service.ToggleNodeStatus("ESP32-HYDRO-01", out bool isOnline, out _);
            Assert.False(isOnline);

            // Recalculate route for ESP32-PUMP-01 under self-healing
            var reroutedPath = graph.GetShortestPathToGateway("ESP32-PUMP-01", "GATEWAY-ROOT");

            // Assert
            Assert.NotEmpty(reroutedPath);
            Assert.DoesNotContain("ESP32-HYDRO-01", reroutedPath); // Must avoid failed router!
            Assert.Contains("ESP32-ROUTER-01", reroutedPath);      // Rerouted through alternate router!
            Assert.Equal("GATEWAY-ROOT", reroutedPath.Last());
        }

        #endregion
    }
}
