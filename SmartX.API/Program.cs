using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartX.API.DataStructures;
using SmartX.API.Models;
using SmartX.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Core Advanced Data Structure Services (Singletons for in-memory live state)
builder.Services.AddSingleton<IDeviceStateService, DeviceStateService>();
builder.Services.AddSingleton<IPredictiveEngineService, PredictiveEngineService>();
builder.Services.AddSingleton<IMeshTopologyService, MeshTopologyService>(); // Part 3: Graph, Trees, MST, Traversal

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "SmartX-IoT-Mesh Gateway API (PoE Final)",
        Version = "v3",
        Description = "Industrial Edge Gateway API featuring Advanced Data Structures (AVL Trees, Graphs, BFS/DFS Traversal, Prim's/Kruskal's MST, Priority Queues, Stacks, Dictionaries, Sets)"
    });
});

// Configure CORS for desktop & web client integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure HTTP pipeline
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartX API v3");
    c.RoutePrefix = "swagger";
});

// ==================================================================
// PILLAR 1: SENSOR DATA INGESTION & TELEMETRY
// ==================================================================

// POST /api/telemetry: Ingests packet via FIFO queue or Priority Queue + records to AVL Tree
app.MapPost("/api/telemetry", (TelemetryPacket<double> packet, IDeviceStateService deviceService, IMeshTopologyService topologyService) =>
{
    if (packet == null)
    {
        return Results.BadRequest(new { Status = "ERROR", Message = "Telemetry packet cannot be null." });
    }

    // Ingest into Advanced Data Structures (FIFO Queue or Priority Alert Queue bypass)
    TelemetryResponse response = deviceService.EnqueueTelemetry(packet);

    // Also record into AVL Tree for chronological range querying
    topologyService.RecordTelemetry(new TelemetryRecord(
        packet.MacAddress,
        packet.Location,
        packet.Category,
        packet.Timestamp,
        packet.PayloadValue,
        packet.Category == "Power" ? "W" : packet.Category == "Hydroponics" ? "%" : "°C",
        response.VisualState
    ));

    return Results.Ok(response);
})
.WithName("SubmitTelemetry");

// POST /api/sensors/{mac}/upload-log: Multipart file/photo upload
app.MapPost("/api/sensors/{mac}/upload-log", async (string mac, IFormFile file, IWebHostEnvironment env) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { Status = "ERROR", Message = "No file attached or file is empty." });
    }

    string uploadDir = Path.Combine(env.ContentRootPath, "UploadedLogs");
    if (!Directory.Exists(uploadDir))
    {
        Directory.CreateDirectory(uploadDir);
    }

    string safeMac = mac.Replace(":", "-").Replace("/", "_");
    string timestampPrefix = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    string safeFileName = $"{safeMac}_{timestampPrefix}_{Path.GetFileName(file.FileName)}";
    string destinationPath = Path.Combine(uploadDir, safeFileName);

    using (var stream = new FileStream(destinationPath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    return Results.Ok(new
    {
        Status = "SUCCESS",
        MacAddress = mac,
        OriginalFileName = file.FileName,
        SavedFileName = safeFileName,
        FileSizeBytes = file.Length,
        UploadedAt = DateTime.UtcNow,
        FilePath = destinationPath
    });
})
.WithName("UploadSensorLog")
.DisableAntiforgery();

// ==================================================================
// PILLAR 2: REAL-TIME COMMAND STREAM & HISTORY
// ==================================================================

// GET /api/devices: Fetch live registered devices from Dictionary<string, LiveDevice> with search/filter
app.MapGet("/api/devices", (string? category, string? status, string? search, IDeviceStateService deviceService) =>
{
    var devices = deviceService.GetAllDevices(category, status, search);
    return Results.Ok(devices);
})
.WithName("GetLiveDevices");

// GET /api/devices/{mac}: Instantaneous O(1) Dictionary lookup
app.MapGet("/api/devices/{mac}", (string mac, IDeviceStateService deviceService) =>
{
    var device = deviceService.GetDevice(mac);
    return device != null ? Results.Ok(device) : Results.NotFound(new { Message = $"Device with MAC {mac} not found in registry." });
})
.WithName("GetDeviceByMac");

// POST /api/devices: Register new device into Dictionary
app.MapPost("/api/devices", (LiveDevice device, IDeviceStateService deviceService) =>
{
    if (device == null || string.IsNullOrWhiteSpace(device.MacAddress))
    {
        return Results.BadRequest(new { Message = "Device and MAC address are required." });
    }
    deviceService.RegisterDevice(device);
    return Results.Created($"/api/devices/{device.MacAddress}", device);
})
.WithName("RegisterDevice");

// POST /api/commands: Manual override command (Pushes to Stack<DeviceCommand>)
app.MapPost("/api/commands", (DeviceCommand command, IDeviceStateService deviceService, IPredictiveEngineService predictiveService) =>
{
    if (command == null || string.IsNullOrWhiteSpace(command.MacAddress) || string.IsNullOrWhiteSpace(command.ActionType))
    {
        return Results.BadRequest(new { Message = "Command requires valid MAC Address and ActionType." });
    }

    var executed = deviceService.ExecuteCommand(command);
    predictiveService.RecordCommandExecution(executed);

    return Results.Ok(new
    {
        Status = "SUCCESS",
        Message = $"Command '{executed.ActionType}' ({executed.Parameter}) executed successfully on {executed.TargetDeviceName}.",
        Command = executed
    });
})
.WithName("ExecuteCommand");

// POST /api/commands/undo: Reverts the last command from Stack<DeviceCommand>
app.MapPost("/api/commands/undo", (IDeviceStateService deviceService) =>
{
    var undone = deviceService.UndoLastCommand();
    if (undone == null)
    {
        return Results.NotFound(new { Status = "EMPTY_STACK", Message = "No previous commands available on the undo stack to revert." });
    }

    return Results.Ok(new
    {
        Status = "SUCCESS",
        Message = $"Reverted last command on {undone.TargetDeviceName}. New state restored to {undone.Parameter}.",
        UndoneCommand = undone
    });
})
.WithName("UndoCommand");

// GET /api/commands/history: Inspect command stack
app.MapGet("/api/commands/history", (IDeviceStateService deviceService) =>
{
    return Results.Ok(deviceService.GetCommandHistory());
})
.WithName("GetCommandHistory");

// GET /api/telemetry/queue-status: Real-time status of all data structures
app.MapGet("/api/telemetry/queue-status", (IDeviceStateService deviceService) =>
{
    return Results.Ok(deviceService.GetQueueStatus());
})
.WithName("GetQueueStatus");

// POST /api/telemetry/simulate-alert: Priority Queue Bypass simulation
app.MapPost("/api/telemetry/simulate-alert", (SimulateAlertRequest req, IDeviceStateService deviceService) =>
{
    deviceService.SimulateHighPriorityAlert(
        req.MacAddress ?? "12:34:56:78:9A:BC",
        req.AlertType ?? "POWER_SPIKE",
        req.Value > 0 ? req.Value : 4850.0,
        req.Message ?? "CRITICAL VOLTAGE SPIKE: Substation transformer surge detected. Immediate priority queue bypass triggered!"
    );

    return Results.Ok(new
    {
        Status = "PRIORITY_ALERT_QUEUED",
        Message = "High-priority alert successfully bypassed standard FIFO queue and updated device state.",
        QueueStatus = deviceService.GetQueueStatus()
    });
})
.WithName("SimulateAlert");

// GET /api/telemetry/timeline: Fetch chronologically sorted logs from SortedDictionary
app.MapGet("/api/telemetry/timeline", (int? limit, IDeviceStateService deviceService) =>
{
    var timeline = deviceService.GetTimelineLogs(limit ?? 50);
    return Results.Ok(timeline);
})
.WithName("GetTimeline");

// GET /api/errors/active: Fetch unique active error states from HashSet
app.MapGet("/api/errors/active", (IDeviceStateService deviceService) =>
{
    return Results.Ok(deviceService.GetActiveErrors());
})
.WithName("GetActiveErrors");

// POST /api/errors/resolve: Resolve an error from HashSet
app.MapPost("/api/errors/resolve", (ResolveErrorRequest req, IDeviceStateService deviceService) =>
{
    bool removed = deviceService.ResolveError(req.ErrorKey);
    return Results.Ok(new { Success = removed, ErrorKey = req.ErrorKey });
})
.WithName("ResolveError");

// ==================================================================
// PILLAR 3: NETWORK TOPOLOGY & MESH ROUTING ENGINE (POE PART 3)
// ==================================================================

// GET /api/topology/graph: Full mesh network topology graph (Vertices, Edges, Weights, Statuses)
app.MapGet("/api/topology/graph", (IMeshTopologyService topologyService) =>
{
    var graph = topologyService.GetGraph();
    return Results.Ok(new
    {
        VertexCount = graph.VertexCount,
        EdgeCount = graph.EdgeCount,
        GatewayRootId = topologyService.GatewayRootId,
        Vertices = graph.Vertices.Values.ToList(),
        Edges = graph.Edges.ToList()
    });
})
.WithName("GetTopologyGraph");

// GET /api/topology/mst: Compute Minimum Spanning Tree (Prim's Min-Heap or Kruskal's DSU)
app.MapGet("/api/topology/mst", (bool? kruskal, IMeshTopologyService topologyService) =>
{
    var result = topologyService.CalculateMst(kruskal ?? false);
    return Results.Ok(result);
})
.WithName("GetMinimumSpanningTree");

// GET /api/topology/traversal: Graph Traversal (BFS / DFS)
app.MapGet("/api/topology/traversal", (string? algorithm, string? startNode, IMeshTopologyService topologyService) =>
{
    var result = topologyService.RunTraversal(algorithm ?? "BFS", startNode);
    return Results.Ok(result);
})
.WithName("RunGraphTraversal");

// GET /api/topology/route/{id}: Shortest hop-by-hop path from sensor node to gateway
app.MapGet("/api/topology/route/{id}", (string id, IMeshTopologyService topologyService) =>
{
    var path = topologyService.GetRoutingPath(id);
    return Results.Ok(new
    {
        NodeId = id,
        Gateway = topologyService.GatewayRootId,
        HopCount = Math.Max(0, path.Count - 1),
        RoutePath = path
    });
})
.WithName("GetRoutingPath");

// POST /api/topology/toggle-outage: Simulates node failure / self-healing network restoration
app.MapPost("/api/topology/toggle-outage", (ToggleOutageRequest req, IMeshTopologyService topologyService) =>
{
    if (string.IsNullOrWhiteSpace(req.NodeId))
    {
        return Results.BadRequest(new { Message = "NodeId is required." });
    }

    bool success = topologyService.ToggleNodeStatus(req.NodeId, out bool newStatus, out string message);
    if (!success)
    {
        return Results.NotFound(new { Message = message });
    }

    // Recompute MST and routes after state change to show dynamic self-healing
    var updatedMst = topologyService.CalculateMst();
    return Results.Ok(new
    {
        Success = true,
        NodeId = req.NodeId,
        IsOnline = newStatus,
        Message = message,
        UpdatedMst = updatedMst
    });
})
.WithName("ToggleNodeOutage");

// GET /api/history/query-range: Fast AVL Tree range query O(log n + k)
app.MapGet("/api/history/query-range", (string mac, DateTime? start, DateTime? end, IMeshTopologyService topologyService) =>
{
    if (string.IsNullOrWhiteSpace(mac))
    {
        return Results.BadRequest(new { Message = "MAC Address is required." });
    }

    DateTime startTime = start ?? DateTime.UtcNow.AddHours(-12);
    DateTime endTime = end ?? DateTime.UtcNow.AddMinutes(5);

    var records = topologyService.QueryHistoricalRange(mac, startTime, endTime, out int evaluated, out int height);

    return Results.Ok(new
    {
        MacAddress = mac,
        StartTime = startTime,
        EndTime = endTime,
        RecordCount = records.Count,
        NodesEvaluated = evaluated,
        AvlTreeHeight = height,
        Algorithm = "AVL Tree Range Search (O(log n + k))",
        Records = records
    });
})
.WithName("QueryHistoricalRange");

// GET /api/history/all: Get all history for sensor
app.MapGet("/api/history/all", (string mac, IMeshTopologyService topologyService) =>
{
    var records = topologyService.GetAllHistory(mac);
    return Results.Ok(new
    {
        MacAddress = mac,
        TotalCount = records.Count,
        Records = records
    });
})
.WithName("GetAllHistoricalRecords");

// POST /api/history/record: Append telemetry to AVL Tree
app.MapPost("/api/history/record", (RecordTelemetryRequest req, IMeshTopologyService topologyService) =>
{
    var record = new TelemetryRecord(
        req.MacAddress,
        req.DeviceName ?? "Edge Sensor",
        req.Category ?? "Environmental",
        req.Timestamp ?? DateTime.UtcNow,
        req.Value,
        req.Unit ?? "%",
        req.Status ?? "NORMAL"
    );

    topologyService.RecordTelemetry(record);
    return Results.Ok(new { Status = "SUCCESS", Record = record });
})
.WithName("RecordHistoricalTelemetry");

// GET /api/history/stats: Summary count of AVL Tree records per node
app.MapGet("/api/history/stats", (IMeshTopologyService topologyService) =>
{
    return Results.Ok(topologyService.GetHistoryStats());
})
.WithName("GetHistoryStats");

// ==================================================================
// PREDICTIVE ACTION & RECOMMENDATION ENGINE
// ==================================================================

// GET /api/predictive/insights: Proactive suggestions
app.MapGet("/api/predictive/insights", (IDeviceStateService deviceService, IPredictiveEngineService predictiveService) =>
{
    var insights = predictiveService.GetCurrentInsights(deviceService);
    return Results.Ok(insights);
})
.WithName("GetPredictiveInsights");

// POST /api/predictive/record-search: Pattern learning from user queries
app.MapPost("/api/predictive/record-search", (RecordSearchRequest req, IPredictiveEngineService predictiveService) =>
{
    predictiveService.RecordUserSearch(req.SearchTerm ?? string.Empty, req.Category ?? "All", req.AlertState ?? "All");
    return Results.Ok(new { Status = "RECORDED" });
})
.WithName("RecordSearch");

// POST /api/predictive/execute-suggestion/{id}: One-click execution of automated recommendation
app.MapPost("/api/predictive/execute-suggestion/{id}", (string id, IDeviceStateService deviceService, IPredictiveEngineService predictiveService) =>
{
    var executed = predictiveService.ExecuteSuggestion(id, deviceService);
    if (executed == null)
    {
        return Results.NotFound(new { Message = $"Suggestion with ID {id} not found or expired." });
    }

    return Results.Ok(new
    {
        Status = "SUCCESS",
        Message = $"Executed automated recommendation: {executed.ActionType} ({executed.Parameter}) on {executed.TargetDeviceName}.",
        ExecutedCommand = executed
    });
})
.WithName("ExecuteSuggestion");

app.Run();

// DTO Helper classes for minimal API endpoints
public record SimulateAlertRequest(string? MacAddress, string? AlertType, double Value, string? Message);
public record ResolveErrorRequest(string ErrorKey);
public record RecordSearchRequest(string? SearchTerm, string? Category, string? AlertState);
public record ToggleOutageRequest(string NodeId);
public record RecordTelemetryRequest(string MacAddress, string? DeviceName, string? Category, DateTime? Timestamp, double Value, string? Unit, string? Status);
