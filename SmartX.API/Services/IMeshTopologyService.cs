using System;
using System.Collections.Generic;
using SmartX.API.DataStructures;

namespace SmartX.API.Services
{
    public interface IMeshTopologyService
    {
        MeshGraph Graph { get; }
        string GatewayRootId { get; }

        MeshGraph GetGraph();
        MstResult CalculateMst(bool useKruskal = false);
        TraversalResult RunTraversal(string algorithm = "BFS", string? startNodeId = null);
        List<string> GetRoutingPath(string macOrNodeId);
        bool ToggleNodeStatus(string nodeId, out bool newStatus, out string message);
        void RecordTelemetry(TelemetryRecord record);
        List<TelemetryRecord> QueryHistoricalRange(string macAddress, DateTime startTime, DateTime endTime, out int nodesEvaluated, out int treeHeight);
        List<TelemetryRecord> GetAllHistory(string macAddress);
        Dictionary<string, int> GetHistoryStats();
    }
}
