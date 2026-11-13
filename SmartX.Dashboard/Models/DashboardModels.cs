using System;
using System.Collections.Generic;

namespace SmartX.Dashboard.Models
{
    public class LiveDeviceDto
    {
        public string MacAddress { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double LastReading { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }
        public string Status { get; set; } = "Normal";
        public string RelayState { get; set; } = "CLOSED";
        public string PumpState { get; set; } = "OFF";
        public string ValveState { get; set; } = "NORMAL";
        public int TelemetryCount { get; set; }
        public double PowerDrawWatts { get; set; }
        public string FirmwareVersion { get; set; } = string.Empty;

        // Display Helpers
        public string DisplayValue => $"{LastReading:F1} {Unit}";
        public string StatusColor => Status switch
        {
            "Critical" => "#EF4444",
            "Warning" => "#F59E0B",
            "Disconnected" => "#64748B",
            _ => "#10B981"
        };
        public string RelayColor => RelayState == "CLOSED" ? "#10B981" : "#EF4444";
        public string PumpColor => PumpState == "ON" ? "#3B82F6" : "#64748B";
    }

    public class DeviceCommandDto
    {
        public string CommandId { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string TargetDeviceName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Parameter { get; set; } = string.Empty;
        public string PreviousState { get; set; } = string.Empty;
        public string ResultState { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public string Operator { get; set; } = string.Empty;
        public bool IsUndone { get; set; }

        public string FormattedDescription => $"[{IssuedAt:HH:mm:ss}] {TargetDeviceName}: {ActionType} -> {Parameter} (Prev: {PreviousState})";
    }

    public class PredictiveInsightDto
    {
        public string InsightId { get; set; } = string.Empty;
        public string TargetMac { get; set; } = string.Empty;
        public string TargetDeviceName { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
        public string ActionParameter { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public string Severity { get; set; } = "HIGH";
        public string PatternCorrelation { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }

        public string ConfidencePercentage => $"{ConfidenceScore * 100:F0}%";
        public string SeverityColor => Severity switch
        {
            "CRITICAL" => "#EF4444",
            "HIGH" => "#F97316",
            "MEDIUM" => "#F59E0B",
            _ => "#3B82F6"
        };
    }

    public class QueueStatusDto
    {
        public int StandardFifoQueueCount { get; set; }
        public int PriorityAlertQueueCount { get; set; }
        public int CommandUndoStackCount { get; set; }
        public int RegisteredDevicesCount { get; set; }
        public int HistoricalTimelineCount { get; set; }
        public int ActiveErrorCount { get; set; }
        public List<string> ActiveErrorList { get; set; } = new();
        public List<string> RecentCommands { get; set; } = new();
        public long TotalPacketsProcessed { get; set; }
        public long TotalPriorityBypasses { get; set; }
        public DateTime LastProcessedTimestamp { get; set; }
    }

    public class HistoricalLogDto
    {
        public string LogId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ProcessingQueue { get; set; } = string.Empty;

        public string QueueBadgeColor => ProcessingQueue.Contains("Priority", StringComparison.OrdinalIgnoreCase) ? "#EF4444" : "#3B82F6";
        public string FormattedLog => $"[{Timestamp:HH:mm:ss.fff}] {DeviceName}: {Value:F1} {Unit} [{ProcessingQueue}]";
    }

    // ====================================================================
    // PART 3 (PoE Final): MESH TOPOLOGY & AVL TREE DATA STRUCTURE DTOs
    // ====================================================================

    public class MeshVertexDto
    {
        public string Id { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public int NodeType { get; set; } // 0=Gateway, 1=Router, 2=Sensor, 3=Actuator
        public bool IsOnline { get; set; } = true;
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double BatteryPercentage { get; set; } = 100.0;
        public int SignalDbm { get; set; } = -60;

        public string NodeTypeName => NodeType switch
        {
            0 => "Gateway Root",
            1 => "Mesh Router",
            2 => "Sensor Leaf",
            3 => "Actuator Endpoint",
            _ => "Mesh Node"
        };

        public string NodeColor => !IsOnline ? "#EF4444" : NodeType switch
        {
            0 => "#2563EB", // Royal Blue Gateway
            1 => "#3B82F6", // Router
            2 => "#10B981", // Green Sensor
            3 => "#8B5CF6", // Purple Actuator
            _ => "#0EA5E9"
        };

        public string StatusText => IsOnline ? "ONLINE" : "OFFLINE (DOWN)";
        public string StatusBadgeColor => IsOnline ? "#10B981" : "#EF4444";
    }

    public class MeshEdgeDto
    {
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public double Weight { get; set; } // Transmission power in mW
        public int Rssi { get; set; }
        public double LatencyMs { get; set; }
        public bool IsMstEdge { get; set; }
        public bool IsActive { get; set; } = true;

        public string Label => $"{Weight:F1} mW ({Rssi} dBm)";
    }

    public class TopologyGraphDto
    {
        public int VertexCount { get; set; }
        public int EdgeCount { get; set; }
        public string GatewayRootId { get; set; } = string.Empty;
        public List<MeshVertexDto> Vertices { get; set; } = new();
        public List<MeshEdgeDto> Edges { get; set; } = new();
    }

    public class MstResultDto
    {
        public List<MeshEdgeDto> TreeEdges { get; set; } = new();
        public double TotalMstPowerCost { get; set; }
        public double TotalMeshPowerCost { get; set; }
        public double PowerSavingsPercent { get; set; }
        public Dictionary<string, List<string>> HopRoutes { get; set; } = new();
        public List<string> ConnectedNodeIds { get; set; } = new();
        public List<string> UnreachableNodeIds { get; set; } = new();
        public string AlgorithmUsed { get; set; } = "Prim's (Min-Heap Priority Queue)";
        public DateTime CalculatedAt { get; set; }

        public string SummaryText => $"{AlgorithmUsed}: {TreeEdges.Count} tree edges | Power: {TotalMstPowerCost:F1} mW (Saved {PowerSavingsPercent:F1}%)";
    }

    public class TraversalResultDto
    {
        public string Algorithm { get; set; } = "BFS";
        public string StartNodeId { get; set; } = string.Empty;
        public List<string> VisitedNodeIds { get; set; } = new();
        public Dictionary<string, int> HopDistances { get; set; } = new();
        public List<string> UnreachableNodeIds { get; set; } = new();
        public int TotalNodesReached { get; set; }
        public bool IsFullyConnected { get; set; }

        public string FormattedSummary => $"{Algorithm} from [{StartNodeId}]: Reached {TotalNodesReached} devices. Status: {(IsFullyConnected ? "Full Subnet Connected" : $"{UnreachableNodeIds.Count} Isolated Devices")}";
    }

    public class RoutingPathDto
    {
        public string NodeId { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public int HopCount { get; set; }
        public List<string> RoutePath { get; set; } = new();

        public string PathDisplay => RoutePath.Count > 0 ? string.Join(" ➔ ", RoutePath) : "No active route to gateway (Node Isolated)";
    }

    public class TelemetryRecordDto
    {
        public string RecordId { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Status { get; set; } = "NORMAL";

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string FormattedValue => $"{Value:F2} {Unit}";
        public string StatusBadgeColor => Status == "RED_ALERT" ? "#EF4444" : "#10B981";
    }

    public class HistoricalRangeResultDto
    {
        public string MacAddress { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int RecordCount { get; set; }
        public int NodesEvaluated { get; set; }
        public int AvlTreeHeight { get; set; }
        public string Algorithm { get; set; } = "AVL Tree Range Search (O(log n + k))";
        public List<TelemetryRecordDto> Records { get; set; } = new();

        public string DiagnosticSummary => $"AVL Tree Height: {AvlTreeHeight} | Nodes Evaluated: {NodesEvaluated} | Matched: {RecordCount} records in range ({Algorithm})";
    }
}
