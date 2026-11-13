using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using SmartX.Dashboard.Models;

namespace SmartX.Dashboard
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000"),
            Timeout = TimeSpan.FromSeconds(5)
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // UI Observable Collections
        public ObservableCollection<LiveDeviceDto> DevicesList { get; } = new();
        public ObservableCollection<DeviceCommandDto> CommandHistoryList { get; } = new();
        public ObservableCollection<HistoricalLogDto> TimelineLogsList { get; } = new();
        public ObservableCollection<TelemetryRecordDto> HistoricalRecordsList { get; } = new();

        // Active State
        private string? _selectedFilePath;
        private PredictiveInsightDto? _currentInsight;
        private readonly DispatcherTimer _autoRefreshTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Bind UI collections
            DevicesItemsControl.ItemsSource = DevicesList;
            CommandHistoryListBox.ItemsSource = CommandHistoryList;
            TimelineListBox.ItemsSource = TimelineLogsList;
            HistoryDataGrid.ItemsSource = HistoricalRecordsList;

            // Setup auto-refresh polling timer (every 4 seconds for live data sync)
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _autoRefreshTimer.Tick += async (s, e) =>
            {
                if (Pillar2CommandStreamGrid.Visibility == Visibility.Visible)
                {
                    await RefreshStreamAsync(silent: true);
                }
            };
            _autoRefreshTimer.Start();

            // Initial load
            Loaded += async (s, e) =>
            {
                await RefreshStreamAsync();
            };
        }

        #region 3-Pillar Navigation Logic (Preserves Application State)
        private void NavPillar1_Click(object sender, RoutedEventArgs e)
        {
            Pillar1IngestionGrid.Visibility = Visibility.Visible;
            Pillar2CommandStreamGrid.Visibility = Visibility.Collapsed;
            Pillar3TopologyGrid.Visibility = Visibility.Collapsed;

            NavPillar1Button.Style = (Style)FindResource("ActivePillarButtonStyle");
            NavPillar2Button.Style = (Style)FindResource("PillarNavButtonStyle");
            NavPillar3Button.Style = (Style)FindResource("PillarNavButtonStyle");
        }

        private async void NavPillar2_Click(object sender, RoutedEventArgs e)
        {
            Pillar1IngestionGrid.Visibility = Visibility.Collapsed;
            Pillar2CommandStreamGrid.Visibility = Visibility.Visible;
            Pillar3TopologyGrid.Visibility = Visibility.Collapsed;

            NavPillar1Button.Style = (Style)FindResource("PillarNavButtonStyle");
            NavPillar2Button.Style = (Style)FindResource("ActivePillarButtonStyle");
            NavPillar3Button.Style = (Style)FindResource("PillarNavButtonStyle");

            await RefreshStreamAsync();
        }

        private async void NavPillar3_Click(object sender, RoutedEventArgs e)
        {
            Pillar1IngestionGrid.Visibility = Visibility.Collapsed;
            Pillar2CommandStreamGrid.Visibility = Visibility.Collapsed;
            Pillar3TopologyGrid.Visibility = Visibility.Visible;

            NavPillar1Button.Style = (Style)FindResource("PillarNavButtonStyle");
            NavPillar2Button.Style = (Style)FindResource("PillarNavButtonStyle");
            NavPillar3Button.Style = (Style)FindResource("ActivePillarButtonStyle");

            await LoadTopologyDataAsync();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer.Stop();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
        #endregion

        #region Pillar 1: Telemetry Dispatch & Heatmap (Part 1 Preserved)
        private void AttachLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Title = "Select Diagnostic Log or Sensor Image",
                    Filter = "All Supported Files (*.log;*.txt;*.png;*.jpg;*.jpeg;*.csv)|*.log;*.txt;*.png;*.jpg;*.jpeg;*.csv|Log Files (*.log;*.txt)|*.log;*.txt|Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    _selectedFilePath = openFileDialog.FileName;
                    AttachedFileTextBlock.Text = System.IO.Path.GetFileName(_selectedFilePath);
                    AttachedFileTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"));
                    AppendConsoleLog($"[FILE ATTACHED] Selected file: {_selectedFilePath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting file: {ex.Message}", "File Attachment Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string macAddress = MacAddressTextBox.Text.Trim();
            string location = LocationTextBox.Text.Trim();
            string category = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Environmental";
            string rawValueText = TelemetryValueTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(macAddress))
            {
                MessageBox.Show("Please enter a valid MAC Address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(rawValueText, out double telemetryValue))
            {
                MessageBox.Show("Please enter a valid numeric Telemetry Value.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SubmitButton.IsEnabled = false;
            SubmitButton.Content = "⏳ Transmitting Payload...";

            try
            {
                var packetPayload = new
                {
                    MacAddress = macAddress,
                    Location = location,
                    Category = category,
                    PayloadValue = telemetryValue,
                    Timestamp = DateTime.UtcNow
                };

                string jsonContent = JsonSerializer.Serialize(packetPayload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                AppendConsoleLog($"[DISPATCH] Sending TelemetryPacket to /api/telemetry | MAC: {macAddress} | Val: {telemetryValue}");

                HttpResponseMessage response = await HttpClient.PostAsync("/api/telemetry", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    JsonElement root = doc.RootElement;

                    string status = root.GetProperty("status").GetString() ?? "SUCCESS";
                    string visualState = root.GetProperty("visualState").GetString() ?? "GREEN_NORMAL";
                    string message = root.GetProperty("message").GetString() ?? string.Empty;

                    AppendConsoleLog($"[API RESPONSE] Status: {status} | VisualState: {visualState}");
                    AppendConsoleLog($"[QUEUE ROUTING] {message}");

                    UpdateHeatmapVisualState(visualState, telemetryValue, macAddress);
                }
                else
                {
                    AppendConsoleLog($"[API ERROR] HTTP {(int)response.StatusCode}: {responseBody}");
                    UpdateHeatmapVisualState("RED_ALERT", telemetryValue, macAddress, $"API Call Failed: {(int)response.StatusCode}");
                }

                if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
                {
                    await UploadAttachedLogFileAsync(macAddress, _selectedFilePath);
                }
            }
            catch (HttpRequestException httpEx)
            {
                AppendConsoleLog($"[CONNECTION ERROR] Could not connect to API at {HttpClient.BaseAddress}: {httpEx.Message}");
                UpdateHeatmapVisualState("RED_ALERT", telemetryValue, macAddress, "Backend API Connection Refused");
            }
            catch (Exception ex)
            {
                AppendConsoleLog($"[ERROR] Unexpected exception: {ex.Message}");
            }
            finally
            {
                SubmitButton.IsEnabled = true;
                SubmitButton.Content = "🚀 Submit Telemetry Data";
            }
        }

        private async Task UploadAttachedLogFileAsync(string macAddress, string filePath)
        {
            try
            {
                string safeMac = Uri.EscapeDataString(macAddress);
                string uploadEndpoint = $"/api/sensors/{safeMac}/upload-log";

                using var formContent = new MultipartFormDataContent();
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                string fileName = System.IO.Path.GetFileName(filePath);
                formContent.Add(fileContent, "file", fileName);

                AppendConsoleLog($"[FILE UPLOAD] Uploading {fileName} ({fileBytes.Length} bytes) to {uploadEndpoint}");

                HttpResponseMessage uploadResponse = await HttpClient.PostAsync(uploadEndpoint, formContent);
                string responseBody = await uploadResponse.Content.ReadAsStringAsync();

                if (uploadResponse.IsSuccessStatusCode)
                {
                    AppendConsoleLog($"[FILE SUCCESS] Log uploaded and persisted on server. Details: {responseBody}");
                }
                else
                {
                    AppendConsoleLog($"[FILE ERROR] Server rejected upload with HTTP {(int)uploadResponse.StatusCode}: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                AppendConsoleLog($"[FILE EXCEPTION] Could not upload file: {ex.Message}");
            }
        }

        private void UpdateHeatmapVisualState(string visualState, double value, string macAddress, string? customMessage = null)
        {
            if (visualState == "RED_ALERT")
            {
                StatusHeatmapBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                StatusIconTextBlock.Text = "⚠️";
                StatusHeaderTextBlock.Text = "RED_ALERT (CRITICAL THRESHOLD EXCEEDED)";
                StatusDetailTextBlock.Text = customMessage ?? $"Node {macAddress} reported value of {value:F2}, exceeding safe threshold limit!";
            }
            else
            {
                StatusHeatmapBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                StatusIconTextBlock.Text = "✓";
                StatusHeaderTextBlock.Text = "GREEN_NORMAL (NOMINAL OPERATIONAL RANGE)";
                StatusDetailTextBlock.Text = customMessage ?? $"Node {macAddress} nominal at {value:F2}. Normal boundary verified.";
            }
        }

        private void AppendConsoleLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            ConsoleLogTextBox.AppendText($"[{timestamp}] {message}\n");
            ConsoleLogTextBox.ScrollToEnd();
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            ConsoleLogTextBox.Clear();
        }
        #endregion

        #region Pillar 2: Real-Time Command Stream, Data Structures & Predictive Engine
        /// <summary>
        /// Asynchronously fetches live devices, data structures metrics, timeline, and suggestions from API.
        /// </summary>
        private async Task RefreshStreamAsync(bool silent = false)
        {
            try
            {
                // 1. Fetch Live Registered Devices from Dictionary<string, LiveDevice> (O(1))
                string category = (CategoryFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Categories";
                string status = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All States";
                string search = DeviceSearchTextBox.Text.Trim();

                string devicesUrl = $"/api/devices?category={Uri.EscapeDataString(category)}&status={Uri.EscapeDataString(status)}&search={Uri.EscapeDataString(search)}";
                var devicesResponse = await HttpClient.GetAsync(devicesUrl);

                if (devicesResponse.IsSuccessStatusCode)
                {
                    string json = await devicesResponse.Content.ReadAsStringAsync();
                    var devices = JsonSerializer.Deserialize<List<LiveDeviceDto>>(json, JsonOptions);

                    DevicesList.Clear();
                    if (devices != null)
                    {
                        foreach (var d in devices) DevicesList.Add(d);
                    }
                    DeviceCountBadgeTextBlock.Text = $"Showing {DevicesList.Count} Devices";
                }

                // 2. Fetch Data Structures Metrics (Queue<T>, PriorityQueue, Stack<T>, HashSet<T>, Dictionary)
                var queueResponse = await HttpClient.GetAsync("/api/telemetry/queue-status");
                if (queueResponse.IsSuccessStatusCode)
                {
                    string json = await queueResponse.Content.ReadAsStringAsync();
                    var qStatus = JsonSerializer.Deserialize<QueueStatusDto>(json, JsonOptions);

                    if (qStatus != null)
                    {
                        MetricFifoQueueTextBlock.Text = $"{qStatus.StandardFifoQueueCount} pkts";
                        MetricPriorityQueueTextBlock.Text = $"{qStatus.TotalPriorityBypasses} bypasses";
                        MetricUndoStackTextBlock.Text = $"{qStatus.CommandUndoStackCount} cmds";
                        UndoStackDepthBadge.Text = $"Stack: {qStatus.CommandUndoStackCount}";
                        MetricActiveErrorsTextBlock.Text = $"{qStatus.ActiveErrorCount} unique";
                        MetricRegisteredDevicesTextBlock.Text = $"{qStatus.RegisteredDevicesCount} nodes";
                    }
                }

                // 3. Fetch Command History (Stack<DeviceCommand>)
                var historyResponse = await HttpClient.GetAsync("/api/commands/history");
                if (historyResponse.IsSuccessStatusCode)
                {
                    string json = await historyResponse.Content.ReadAsStringAsync();
                    var history = JsonSerializer.Deserialize<List<DeviceCommandDto>>(json, JsonOptions);

                    CommandHistoryList.Clear();
                    if (history != null)
                    {
                        foreach (var cmd in history) CommandHistoryList.Add(cmd);
                    }
                }

                // 4. Fetch Chronological Timeline Logs (SortedDictionary<DateTime, HistoricalLog>)
                var timelineResponse = await HttpClient.GetAsync("/api/telemetry/timeline?limit=25");
                if (timelineResponse.IsSuccessStatusCode)
                {
                    string json = await timelineResponse.Content.ReadAsStringAsync();
                    var timeline = JsonSerializer.Deserialize<List<HistoricalLogDto>>(json, JsonOptions);

                    TimelineLogsList.Clear();
                    if (timeline != null)
                    {
                        foreach (var log in timeline) TimelineLogsList.Add(log);
                    }
                }

                // 5. Fetch Predictive Insights & Automated Recommendations
                var insightsResponse = await HttpClient.GetAsync("/api/predictive/insights");
                if (insightsResponse.IsSuccessStatusCode)
                {
                    string json = await insightsResponse.Content.ReadAsStringAsync();
                    var insights = JsonSerializer.Deserialize<List<PredictiveInsightDto>>(json, JsonOptions);

                    if (insights != null && insights.Count > 0)
                    {
                        _currentInsight = insights[0];
                        InsightTitleTextBlock.Text = $"Pattern Match: {_currentInsight.Reason}";
                        InsightReasonTextBlock.Text = $"{_currentInsight.PatternCorrelation} | Target: {_currentInsight.TargetDeviceName}";
                        InsightConfidenceTextBlock.Text = $"Confidence: {_currentInsight.ConfidencePercentage}";
                        ExecuteInsightButton.IsEnabled = true;
                        ExecuteInsightButton.Content = $"⚡ Execute {_currentInsight.SuggestedAction}";
                    }
                    else
                    {
                        _currentInsight = null;
                        InsightTitleTextBlock.Text = "System operating optimally. Predictive engine monitoring historical patterns.";
                        InsightReasonTextBlock.Text = "No critical anomalies or frequent query triggers detected.";
                        InsightConfidenceTextBlock.Text = "Nominal State";
                        ExecuteInsightButton.IsEnabled = false;
                        ExecuteInsightButton.Content = "All Nominal";
                    }
                }

                // API Status Indicator
                ApiStatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                ApiStatusTextBlock.Text = "API: Connected (http://localhost:5000)";
            }
            catch (Exception)
            {
                if (!silent)
                {
                    ApiStatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    ApiStatusTextBlock.Text = "API: Offline / Connection Failed";
                }
            }
        }

        private async void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                await RefreshStreamAsync();
            }
        }

        private async void DeviceSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;

            string query = DeviceSearchTextBox.Text.Trim();
            await RefreshStreamAsync();

            // Record search query on API for pattern correlation learning
            if (query.Length >= 3)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var req = new { SearchTerm = query, Category = "All", AlertState = "All" };
                        var content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");
                        await HttpClient.PostAsync("/api/predictive/record-search", content);
                    }
                    catch { }
                });
            }
        }

        private async void RefreshStreamButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStreamAsync();
        }

        #region Manual Override Commands (Stack Implementation)
        private async void DeviceToggleRelay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mac)
            {
                // Find device in current list
                var device = DevicesList.FirstOrDefault(d => d.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase));
                string newParam = (device?.RelayState == "CLOSED") ? "OPEN" : "CLOSED";

                await SendOverrideCommandAsync(mac, "TOGGLE_RELAY", newParam, $"Toggling Relay to {newParam}");
            }
        }

        private async void DeviceTogglePump_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mac)
            {
                var device = DevicesList.FirstOrDefault(d => d.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase));
                string newParam = (device?.PumpState == "ON") ? "OFF" : "ON";

                await SendOverrideCommandAsync(mac, "TRIGGER_PUMP", newParam, $"Triggering Pump to {newParam}");
            }
        }

        private async void DeviceEmergencyStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mac)
            {
                await SendOverrideCommandAsync(mac, "EMERGENCY_SHUTDOWN", "ISOLATE", "Emergency Isolation Triggered");
            }
        }

        private async Task SendOverrideCommandAsync(string mac, string actionType, string param, string description)
        {
            try
            {
                var cmd = new
                {
                    MacAddress = mac,
                    ActionType = actionType,
                    Parameter = param,
                    Operator = "admin@smartx.io"
                };

                string json = JsonSerializer.Serialize(cmd);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync("/api/commands", content);
                if (response.IsSuccessStatusCode)
                {
                    LastActionStatusTextBlock.Text = $"Command Sent: {actionType} -> {param} (Pushed to Undo Stack)";
                    await RefreshStreamAsync();
                }
                else
                {
                    MessageBox.Show("Could not dispatch override command to device.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Command dispatch failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// "Undo" Feature Implementation: Pops the last command from Stack<DeviceCommand>
        /// and applies the reverse command to revert the device state.
        /// </summary>
        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UndoCommandButton.IsEnabled = false;

                var response = await HttpClient.PostAsync("/api/commands/undo", null);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    string msg = doc.RootElement.GetProperty("message").GetString() ?? "Command successfully undone.";

                    LastActionStatusTextBlock.Text = $"↺ {msg}";
                    MessageBox.Show(msg, "Command Undone (Stack Pop)", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshStreamAsync();
                }
                else
                {
                    MessageBox.Show("No previous commands on the undo stack to revert.", "Undo Stack Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Undo operation failed: {ex.Message}", "Undo Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UndoCommandButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Simulates a Critical High-Priority Alert (Power Surge / Moisture Drop)
        /// that bypasses standard FIFO Queue via PriorityQueue<TelemetryAlert, int>.
        /// </summary>
        private async void SimulateAlertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var req = new
                {
                    MacAddress = "12:34:56:78:9A:BC",
                    AlertType = "POWER_SPIKE",
                    Value = 4850.0,
                    Message = "SIMULATED CRITICAL POWER SURGE: 4850.0 W detected. Priority Queue bypass activated!"
                };

                string json = JsonSerializer.Serialize(req);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync("/api/telemetry/simulate-alert", content);
                if (response.IsSuccessStatusCode)
                {
                    LastActionStatusTextBlock.Text = "🚨 Critical Power Surge Alert Injected via Priority Queue!";
                    MessageBox.Show("High-Priority Alert injected!\n\nNotice that this packet bypassed the standard FIFO Queue and was immediately priority-routed into the Priority Alert Queue and registered in the Unique Error HashSet.", "Priority Queue Test Successful", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    await RefreshStreamAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Simulation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Executes the suggested predictive action with one click, automatically pushing to the Undo stack.
        /// </summary>
        private async void ExecuteInsightButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentInsight == null) return;

            try
            {
                ExecuteInsightButton.IsEnabled = false;

                string url = $"/api/predictive/execute-suggestion/{_currentInsight.InsightId}";
                var response = await HttpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    LastActionStatusTextBlock.Text = $"⚡ Executed Automated Insight: {_currentInsight.SuggestedAction} on {_currentInsight.TargetDeviceName}";
                    MessageBox.Show($"Executed Automated Recommendation:\n\n{_currentInsight.SuggestedAction} on {_currentInsight.TargetDeviceName}\n\nThis action was automatically pushed onto the Undo Stack and can be reverted at any time.", "Predictive Action Executed", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshStreamAsync();
                }
                else
                {
                    MessageBox.Show("Suggestion has expired or already been executed.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshStreamAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to execute suggestion: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExecuteInsightButton.IsEnabled = true;
            }
        }
        #endregion
        #endregion

        #region Pillar 3: Mesh Topology, Graph Algorithms & AVL Tree Engine

        private TopologyGraphDto? _topologyGraph;
        private MstResultDto? _mstResult;
        private MeshVertexDto? _selectedVertex;
        private List<string> _currentHopPath = new();
        private string _currentVisualMode = "FullMesh"; // FullMesh, MST, HopRoute

        private async Task LoadTopologyDataAsync()
        {
            try
            {
                // 1. Fetch Topology Graph
                var graphResp = await HttpClient.GetAsync("/api/topology/graph");
                if (graphResp.IsSuccessStatusCode)
                {
                    string json = await graphResp.Content.ReadAsStringAsync();
                    _topologyGraph = JsonSerializer.Deserialize<TopologyGraphDto>(json, JsonOptions);
                }

                // 2. Fetch MST
                var mstResp = await HttpClient.GetAsync("/api/topology/mst");
                if (mstResp.IsSuccessStatusCode)
                {
                    string json = await mstResp.Content.ReadAsStringAsync();
                    _mstResult = JsonSerializer.Deserialize<MstResultDto>(json, JsonOptions);
                }

                // 3. Update Metrics Banner
                if (_mstResult != null && _topologyGraph != null)
                {
                    MstPowerTextBlock.Text = $"{_mstResult.TotalMstPowerCost:F1} mW";
                    MeshPowerTextBlock.Text = $"{_mstResult.TotalMeshPowerCost:F1} mW";
                    PowerSavingsTextBlock.Text = $"{_mstResult.PowerSavingsPercent:F1}% SAVED";

                    int onlineCount = _topologyGraph.Vertices.Count(v => v.IsOnline);
                    int totalCount = _topologyGraph.Vertices.Count;
                    double onlinePct = totalCount > 0 ? (double)onlineCount / totalCount * 100.0 : 0.0;
                    SubnetStatusTextBlock.Text = $"{onlineCount} / {totalCount} Online ({onlinePct:F0}%)";
                    SubnetStatusTextBlock.Foreground = new SolidColorBrush(onlineCount == totalCount
                        ? (Color)ColorConverter.ConvertFromString("#10B981")
                        : (Color)ColorConverter.ConvertFromString("#F59E0B"));
                }

                // 4. Default Select first sensor if none selected
                if (_selectedVertex == null && _topologyGraph?.Vertices.Count > 0)
                {
                    _selectedVertex = _topologyGraph.Vertices.FirstOrDefault(v => v.NodeType == 1 || v.NodeType == 2)
                                      ?? _topologyGraph.Vertices[0];
                }

                if (_selectedVertex != null)
                {
                    await SelectVertexAsync(_selectedVertex, updateHistory: true);
                }

                RenderTopologyCanvas();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading topology data: {ex.Message}", "Topology Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SelectVertexAsync(MeshVertexDto vertex, bool updateHistory = true)
        {
            _selectedVertex = vertex;

            SelectedNodeTitleTextBlock.Text = $"{GetNodeIcon(vertex.NodeType)} {vertex.DeviceName} ({vertex.Id})";
            SelectedNodeStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(vertex.StatusBadgeColor));
            SelectedNodeStatusBadgeTextBlock.Text = vertex.StatusText;

            SelectedNodeMetaTextBlock.Text = $"MAC: {vertex.MacAddress} | Zone: {vertex.Zone} | Type: {vertex.NodeTypeName} | Battery: {vertex.BatteryPercentage:F1}% | RSSI: {vertex.SignalDbm} dBm";

            ToggleOutageButton.Content = vertex.IsOnline ? "⚡ Simulate Hardware Outage" : "🔄 Restore Node to Online";
            ToggleOutageButton.Background = new SolidColorBrush(vertex.IsOnline
                ? (Color)ColorConverter.ConvertFromString("#EF4444")
                : (Color)ColorConverter.ConvertFromString("#10B981"));

            // Fetch hop-by-hop route
            try
            {
                var routeResp = await HttpClient.GetAsync($"/api/topology/route/{vertex.Id}");
                if (routeResp.IsSuccessStatusCode)
                {
                    string json = await routeResp.Content.ReadAsStringAsync();
                    var routeDto = JsonSerializer.Deserialize<RoutingPathDto>(json, JsonOptions);
                    if (routeDto != null)
                    {
                        _currentHopPath = routeDto.RoutePath;
                        SelectedNodeRouteTextBlock.Text = $"{routeDto.PathDisplay} ({routeDto.HopCount} Hops)";
                    }
                }
            }
            catch
            {
                SelectedNodeRouteTextBlock.Text = $"{vertex.Id} (Direct Route)";
            }

            // Sync with Historical ComboBox
            if (updateHistory)
            {
                foreach (ComboBoxItem item in HistorySensorComboBox.Items)
                {
                    if (item.Tag?.ToString() == vertex.MacAddress)
                    {
                        HistorySensorComboBox.SelectedItem = item;
                        break;
                    }
                }
                await ExecuteAvlRangeQueryAsync(vertex.MacAddress, DateTime.UtcNow.AddHours(-12), DateTime.UtcNow.AddMinutes(5));
            }

            RenderTopologyCanvas();
        }

        private void RenderTopologyCanvas()
        {
            MeshTopologyCanvas.Children.Clear();
            if (_topologyGraph == null) return;

            double canvasWidth = MeshTopologyCanvas.ActualWidth > 0 ? MeshTopologyCanvas.ActualWidth : 760;
            double canvasHeight = MeshTopologyCanvas.ActualHeight > 0 ? MeshTopologyCanvas.ActualHeight : 450;

            // Coordinate normalization dictionary
            var nodePos = new Dictionary<string, Point>();
            foreach (var v in _topologyGraph.Vertices)
            {
                // Normalize positions into canvas dimensions
                double x = Math.Clamp(v.PositionX * (canvasWidth / 920.0), 20, canvasWidth - 160);
                double y = Math.Clamp(v.PositionY * (canvasHeight / 500.0), 15, canvasHeight - 75);
                nodePos[v.Id] = new Point(x, y);
            }

            // 1. DRAW EDGES
            var mstEdgeSet = new HashSet<string>();
            if (_mstResult != null)
            {
                foreach (var e in _mstResult.TreeEdges)
                {
                    mstEdgeSet.Add($"{e.SourceId}|{e.TargetId}");
                    mstEdgeSet.Add($"{e.TargetId}|{e.SourceId}");
                }
            }

            var hopEdgeSet = new HashSet<string>();
            if (_currentHopPath.Count > 1)
            {
                for (int i = 0; i < _currentHopPath.Count - 1; i++)
                {
                    hopEdgeSet.Add($"{_currentHopPath[i]}|{_currentHopPath[i + 1]}");
                    hopEdgeSet.Add($"{_currentHopPath[i + 1]}|{_currentHopPath[i]}");
                }
            }

            foreach (var edge in _topologyGraph.Edges)
            {
                if (!nodePos.ContainsKey(edge.SourceId) || !nodePos.ContainsKey(edge.TargetId)) continue;

                var p1 = nodePos[edge.SourceId];
                var p2 = nodePos[edge.TargetId];

                // Center points on node cards (card size: ~140 x 56)
                double x1 = p1.X + 70;
                double y1 = p1.Y + 28;
                double x2 = p2.X + 70;
                double y2 = p2.Y + 28;

                bool isMst = mstEdgeSet.Contains($"{edge.SourceId}|{edge.TargetId}");
                bool isHop = hopEdgeSet.Contains($"{edge.SourceId}|{edge.TargetId}");

                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2
                };

                // Styling based on visual mode
                if (_currentVisualMode == "MST")
                {
                    if (isMst)
                    {
                        line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                        line.StrokeThickness = 3.0;
                    }
                    else
                    {
                        line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                        line.StrokeThickness = 1.0;
                        line.StrokeDashArray = new DoubleCollection { 4, 4 };
                    }
                }
                else if (_currentVisualMode == "HopRoute")
                {
                    if (isHop)
                    {
                        line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                        line.StrokeThickness = 3.5;
                    }
                    else if (isMst)
                    {
                        line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                        line.StrokeThickness = 2.0;
                        line.Opacity = 0.5;
                    }
                    else
                    {
                        line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                        line.StrokeThickness = 1.0;
                        line.Opacity = 0.3;
                    }
                }
                else // FullMesh
                {
                    if (isMst)
                    {
                        line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                        line.StrokeThickness = 2.0;
                    }
                    else
                    {
                        line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
                        line.StrokeThickness = 1.2;
                    }
                }

                MeshTopologyCanvas.Children.Add(line);

                // Add link weight badge near midpoint if MST or HopRoute or FullMesh
                double midX = (x1 + x2) / 2.0;
                double midY = (y1 + y2) / 2.0;

                var badge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isHop ? "#F59E0B" : isMst ? "#10B981" : "#334155")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(3, 1, 3, 1),
                    Child = new TextBlock
                    {
                        Text = $"{edge.Weight:F0}mW",
                        FontSize = 8,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isHop ? "#F59E0B" : isMst ? "#10B981" : "#94A3B8"))
                    }
                };

                Canvas.SetLeft(badge, midX - 16);
                Canvas.SetTop(badge, midY - 8);
                MeshTopologyCanvas.Children.Add(badge);
            }

            // 2. DRAW NODES
            foreach (var vertex in _topologyGraph.Vertices)
            {
                var pos = nodePos[vertex.Id];
                bool isSelected = _selectedVertex?.Id == vertex.Id;
                bool isGateway = vertex.NodeType == 0;

                var card = new Border
                {
                    Width = 140,
                    Height = 56,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isGateway ? "#1E40AF" : isSelected ? "#1E3A8A" : "#1E293B")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        !vertex.IsOnline ? "#EF4444" :
                        isSelected ? "#F59E0B" :
                        isGateway ? "#60A5FA" :
                        vertex.NodeType == 1 ? "#3B82F6" :
                        vertex.NodeType == 3 ? "#8B5CF6" : "#10B981")),
                    BorderThickness = new Thickness(isSelected ? 2.5 : 1.5),
                    Padding = new Thickness(8, 6, 8, 6),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = vertex
                };

                // Node content
                var sp = new StackPanel();

                // Title row
                var titleDock = new DockPanel();
                var titleText = new TextBlock
                {
                    Text = $"{GetNodeIcon(vertex.NodeType)} {vertex.DeviceName}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.White),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 100
                };
                var statusDot = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(vertex.StatusBadgeColor)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(statusDot, Dock.Right);
                titleDock.Children.Add(statusDot);
                titleDock.Children.Add(titleText);
                sp.Children.Add(titleDock);

                // Subtitle: Zone / RSSI
                var subText = new TextBlock
                {
                    Text = $"{vertex.Zone} | {vertex.SignalDbm} dBm",
                    FontSize = 8,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                sp.Children.Add(subText);

                // Bottom row: Node Id + Battery
                var bottomDock = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
                var idText = new TextBlock
                {
                    Text = vertex.Id,
                    FontSize = 8,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? "#F59E0B" : "#38BDF8"))
                };
                var batteryText = new TextBlock
                {
                    Text = $"⚡ {vertex.BatteryPercentage:F0}%",
                    FontSize = 8,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(batteryText, Dock.Right);
                bottomDock.Children.Add(batteryText);
                bottomDock.Children.Add(idText);
                sp.Children.Add(bottomDock);

                card.Child = sp;

                // Click event
                card.MouseLeftButtonDown += async (s, e) =>
                {
                    e.Handled = true;
                    if (s is Border b && b.Tag is MeshVertexDto v)
                    {
                        await SelectVertexAsync(v, updateHistory: true);
                    }
                };

                Canvas.SetLeft(card, pos.X);
                Canvas.SetTop(card, pos.Y);
                MeshTopologyCanvas.Children.Add(card);
            }
        }

        private static string GetNodeIcon(int nodeType)
        {
            return nodeType switch
            {
                0 => "📡",
                1 => "⚡",
                2 => "🌿",
                3 => "💧",
                _ => "📟"
            };
        }

        private void Canvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Empty click handler
        }

        private void ViewMode_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewFullMeshRadio?.IsChecked == true) _currentVisualMode = "FullMesh";
            else if (ViewMstRadio?.IsChecked == true) _currentVisualMode = "MST";
            else if (ViewHopRouteRadio?.IsChecked == true) _currentVisualMode = "HopRoute";

            RenderTopologyCanvas();
        }

        private async void RunPrimMst_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var resp = await HttpClient.GetAsync("/api/topology/mst?kruskal=false");
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    _mstResult = JsonSerializer.Deserialize<MstResultDto>(json, JsonOptions);
                    if (_mstResult != null)
                    {
                        MstPowerTextBlock.Text = $"{_mstResult.TotalMstPowerCost:F1} mW";
                        MeshPowerTextBlock.Text = $"{_mstResult.TotalMeshPowerCost:F1} mW";
                        PowerSavingsTextBlock.Text = $"{_mstResult.PowerSavingsPercent:F1}% SAVED";

                        ViewMstRadio.IsChecked = true;
                        _currentVisualMode = "MST";
                        RenderTopologyCanvas();

                        MessageBox.Show($"Prim's Algorithm (Min-Heap Priority Queue) Executed Successfully!\n\n" +
                                        $"Calculated Minimum Spanning Tree across {_mstResult.TreeEdges.Count} tree edges.\n" +
                                        $"Total Power Cost: {_mstResult.TotalMstPowerCost:F1} mW vs Broadcast Cost: {_mstResult.TotalMeshPowerCost:F1} mW\n" +
                                        $"Power Saved: {_mstResult.PowerSavingsPercent:F1}%\n\n" +
                                        $"This replicates Zigbee and Thread protocol behavior by selecting the most energy-efficient wireless backbone.",
                                        "Prim's MST Calculation Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to run Prim's MST: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunKruskalMst_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var resp = await HttpClient.GetAsync("/api/topology/mst?kruskal=true");
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    _mstResult = JsonSerializer.Deserialize<MstResultDto>(json, JsonOptions);
                    if (_mstResult != null)
                    {
                        MstPowerTextBlock.Text = $"{_mstResult.TotalMstPowerCost:F1} mW";
                        MeshPowerTextBlock.Text = $"{_mstResult.TotalMeshPowerCost:F1} mW";
                        PowerSavingsTextBlock.Text = $"{_mstResult.PowerSavingsPercent:F1}% SAVED";

                        ViewMstRadio.IsChecked = true;
                        _currentVisualMode = "MST";
                        RenderTopologyCanvas();

                        MessageBox.Show($"Kruskal's Algorithm (Disjoint Set Union / Union-Find) Executed Successfully!\n\n" +
                                        $"Calculated Minimum Spanning Tree across {_mstResult.TreeEdges.Count} tree edges without cycles.\n" +
                                        $"Total Power Cost: {_mstResult.TotalMstPowerCost:F1} mW\n" +
                                        $"Power Saved: {_mstResult.PowerSavingsPercent:F1}%\n\n" +
                                        $"Demonstrates greedy edge weight sorting and cycle avoidance via path compression and union-by-rank.",
                                        "Kruskal's MST Calculation Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to run Kruskal's MST: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunBfs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var resp = await HttpClient.GetAsync("/api/topology/traversal?algorithm=BFS");
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    var trav = JsonSerializer.Deserialize<TraversalResultDto>(json, JsonOptions);
                    if (trav != null)
                    {
                        string hopsDetail = string.Join("\n", trav.HopDistances.Select(kvp => $"  • {kvp.Key}: Hop Distance {kvp.Value}"));
                        string unreachableDetail = trav.UnreachableNodeIds.Count > 0
                            ? $"\n\nIsolated / Down Nodes:\n  • {string.Join(", ", trav.UnreachableNodeIds)}"
                            : "\n\nAll mesh nodes are reachable from the Central Gateway.";

                        MessageBox.Show($"Breadth-First Search (BFS) Subnet Traversal:\n\n" +
                                        $"Reached {trav.TotalNodesReached} devices layer-by-layer from Gateway Root.\n\n" +
                                        $"Hop Distances from Gateway:\n{hopsDetail}{unreachableDetail}",
                                        "BFS Traversal Results", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to run BFS: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshTopology_Click(object sender, RoutedEventArgs e)
        {
            await LoadTopologyDataAsync();
        }

        private async void ToggleOutage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVertex == null) return;

            try
            {
                var payload = new { NodeId = _selectedVertex.Id };
                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await HttpClient.PostAsync("/api/topology/toggle-outage", content);
                if (resp.IsSuccessStatusCode)
                {
                    string respJson = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(respJson);
                    string msg = doc.RootElement.GetProperty("message").GetString() ?? "Status toggled";
                    bool isOnline = doc.RootElement.GetProperty("isOnline").GetBoolean();

                    MessageBox.Show($"{msg}\n\n" +
                                    (isOnline
                                        ? "The network re-incorporated the node and updated the minimum power routing paths."
                                        : "Self-Healing Triggered: Telemetry packets from downstream nodes are now dynamically rerouted through alternative mesh routers to reach the central gateway without data loss."),
                                    "Mesh Self-Healing Event", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    await LoadTopologyDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Outage toggle failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region AVL Tree Historical Query Handlers

        private async void HistorySensorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistorySensorComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string mac = item.Tag.ToString()!;
                await ExecuteAvlRangeQueryAsync(mac, DateTime.UtcNow.AddHours(-12), DateTime.UtcNow.AddMinutes(5));
            }
        }

        private async void HistoryPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null && double.TryParse(btn.Tag.ToString(), out double hours))
            {
                string mac = GetSelectedHistoryMac();
                DateTime start = DateTime.UtcNow.AddHours(-hours);
                DateTime end = DateTime.UtcNow.AddMinutes(5);
                await ExecuteAvlRangeQueryAsync(mac, start, end);
            }
        }

        private async void ExecuteAvlQuery_Click(object sender, RoutedEventArgs e)
        {
            string mac = GetSelectedHistoryMac();
            await ExecuteAvlRangeQueryAsync(mac, DateTime.UtcNow.AddHours(-12), DateTime.UtcNow.AddMinutes(5));
        }

        private string GetSelectedHistoryMac()
        {
            if (HistorySensorComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                return item.Tag.ToString()!;
            }
            return "00:1A:2C:3F:4E:56";
        }

        private async Task ExecuteAvlRangeQueryAsync(string macAddress, DateTime start, DateTime end)
        {
            try
            {
                string url = $"/api/history/query-range?mac={Uri.EscapeDataString(macAddress)}&start={start:O}&end={end:O}";
                var resp = await HttpClient.GetAsync(url);

                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<HistoricalRangeResultDto>(json, JsonOptions);

                    if (result != null)
                    {
                        HistoricalRecordsList.Clear();
                        foreach (var rec in result.Records)
                        {
                            HistoricalRecordsList.Add(rec);
                        }

                        AvlTreeHeightTextBlock.Text = $"{result.AvlTreeHeight} Levels";
                        AvlEvaluatedTextBlock.Text = $"{result.NodesEvaluated} Nodes";
                        AvlMatchedTextBlock.Text = $"{result.RecordCount} Records";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AVL query error: {ex.Message}");
            }
        }

        #endregion

        #endregion
    }
}
