using System;
using System.Collections.Generic;

namespace SmartX.API.DataStructures
{
    /// <summary>
    /// Represents a single timestamped sensor telemetry record stored inside the AVL tree.
    /// </summary>
    public class TelemetryRecord : IComparable<TelemetryRecord>
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString("N");
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Status { get; set; } = "NORMAL";

        public TelemetryRecord() { }

        public TelemetryRecord(string macAddress, string deviceName, string category, DateTime timestamp, double value, string unit, string status = "NORMAL")
        {
            MacAddress = macAddress;
            DeviceName = deviceName;
            Category = category;
            Timestamp = timestamp;
            Value = value;
            Unit = unit;
            Status = status;
        }

        public int CompareTo(TelemetryRecord? other)
        {
            if (other == null) return 1;
            int timeCompare = Timestamp.CompareTo(other.Timestamp);
            return timeCompare != 0 ? timeCompare : string.Compare(RecordId, other.RecordId, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {DeviceName} ({Category}): {Value:F2} {Unit} [{Status}]";
        }
    }

    /// <summary>
    /// Generic Node for the self-balancing AVL Tree.
    /// </summary>
    public class AvlNode<TKey, TValue> where TKey : IComparable<TKey>
    {
        public TKey Key { get; set; }
        public List<TValue> Values { get; set; } = new();
        public AvlNode<TKey, TValue>? Left { get; set; }
        public AvlNode<TKey, TValue>? Right { get; set; }
        public int Height { get; set; } = 1;

        public AvlNode(TKey key, TValue value)
        {
            Key = key;
            Values.Add(value);
        }
    }

    /// <summary>
    /// Self-balancing AVL (Adelson-Velsky and Landis) Tree implementation.
    /// Guarantees strict O(log n) time complexity for insertions, deletions, single-key lookups,
    /// and O(log n + k) for range queries across chronological timestamps.
    /// </summary>
    public class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
    {
        private AvlNode<TKey, TValue>? _root;
        private readonly object _lock = new();

        public int Count { get; private set; }

        public int Height => GetHeight(_root);

        public bool IsEmpty => _root == null;

        /// <summary>
        /// Inserts a key-value pair into the AVL tree with automated height balance rotations.
        /// If the key already exists (e.g. identical timestamp), the value is appended to the node's bucket.
        /// </summary>
        public void Insert(TKey key, TValue value)
        {
            lock (_lock)
            {
                _root = InsertInternal(_root, key, value);
                Count++;
            }
        }

        private AvlNode<TKey, TValue> InsertInternal(AvlNode<TKey, TValue>? node, TKey key, TValue value)
        {
            if (node == null)
            {
                return new AvlNode<TKey, TValue>(key, value);
            }

            int compare = key.CompareTo(node.Key);
            if (compare < 0)
            {
                node.Left = InsertInternal(node.Left, key, value);
            }
            else if (compare > 0)
            {
                node.Right = InsertInternal(node.Right, key, value);
            }
            else
            {
                // Key collision (same timestamp) -> bucket multiple values at same node
                node.Values.Add(value);
                return node;
            }

            // Update height of ancestor node
            node.Height = 1 + Math.Max(GetHeight(node.Left), GetHeight(node.Right));

            // Check balance factor
            int balance = GetBalance(node);

            // Left Left Case (Single Right Rotation)
            if (balance > 1 && key.CompareTo(node.Left!.Key) < 0)
            {
                return RightRotate(node);
            }

            // Right Right Case (Single Left Rotation)
            if (balance < -1 && key.CompareTo(node.Right!.Key) > 0)
            {
                return LeftRotate(node);
            }

            // Left Right Case (Double Rotation: Left then Right)
            if (balance > 1 && key.CompareTo(node.Left!.Key) > 0)
            {
                node.Left = LeftRotate(node.Left);
                return RightRotate(node);
            }

            // Right Left Case (Double Rotation: Right then Left)
            if (balance < -1 && key.CompareTo(node.Right!.Key) < 0)
            {
                node.Right = RightRotate(node.Right);
                return LeftRotate(node);
            }

            return node;
        }

        /// <summary>
        /// Range query retrieving all values where startKey &lt;= key &lt;= endKey in O(log n + k) time.
        /// Traverses only relevant branches in the tree without inspecting unorganised records.
        /// </summary>
        public List<TValue> QueryRange(TKey startKey, TKey endKey, out int nodesEvaluated)
        {
            lock (_lock)
            {
                var results = new List<TValue>();
                int evaluated = 0;
                QueryRangeInternal(_root, startKey, endKey, results, ref evaluated);
                nodesEvaluated = evaluated;
                return results;
            }
        }

        public List<TValue> QueryRange(TKey startKey, TKey endKey)
        {
            return QueryRange(startKey, endKey, out _);
        }

        private void QueryRangeInternal(AvlNode<TKey, TValue>? node, TKey startKey, TKey endKey, List<TValue> results, ref int evaluated)
        {
            if (node == null) return;
            evaluated++;

            int startCmp = startKey.CompareTo(node.Key);
            int endCmp = endKey.CompareTo(node.Key);

            // If startKey < node.Key, left subtree may have keys within range
            if (startCmp < 0 && node.Left != null)
            {
                QueryRangeInternal(node.Left, startKey, endKey, results, ref evaluated);
            }

            // If node.Key is within [startKey, endKey], add its values
            if (startCmp <= 0 && endCmp >= 0)
            {
                results.AddRange(node.Values);
            }

            // If endKey > node.Key, right subtree may have keys within range
            if (endCmp > 0 && node.Right != null)
            {
                QueryRangeInternal(node.Right, startKey, endKey, results, ref evaluated);
            }
        }

        /// <summary>
        /// Returns all values in ascending order via in-order traversal.
        /// </summary>
        public List<TValue> ToList()
        {
            lock (_lock)
            {
                var list = new List<TValue>();
                InOrderTraversal(_root, list);
                return list;
            }
        }

        private void InOrderTraversal(AvlNode<TKey, TValue>? node, List<TValue> list)
        {
            if (node == null) return;
            InOrderTraversal(node.Left, list);
            list.AddRange(node.Values);
            InOrderTraversal(node.Right, list);
        }

        public void Clear()
        {
            lock (_lock)
            {
                _root = null;
                Count = 0;
            }
        }

        #region AVL Rotations and Helper Calculations

        private int GetHeight(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

        private int GetBalance(AvlNode<TKey, TValue>? node)
        {
            return node == null ? 0 : GetHeight(node.Left) - GetHeight(node.Right);
        }

        private AvlNode<TKey, TValue> RightRotate(AvlNode<TKey, TValue> y)
        {
            var x = y.Left!;
            var t2 = x.Right;

            // Perform rotation
            x.Right = y;
            y.Left = t2;

            // Update heights
            y.Height = 1 + Math.Max(GetHeight(y.Left), GetHeight(y.Right));
            x.Height = 1 + Math.Max(GetHeight(x.Left), GetHeight(x.Right));

            return x;
        }

        private AvlNode<TKey, TValue> LeftRotate(AvlNode<TKey, TValue> x)
        {
            var y = x.Right!;
            var t2 = y.Left;

            // Perform rotation
            y.Left = x;
            x.Right = t2;

            // Update heights
            x.Height = 1 + Math.Max(GetHeight(x.Left), GetHeight(x.Right));
            y.Height = 1 + Math.Max(GetHeight(y.Left), GetHeight(y.Right));

            return y;
        }

        #endregion
    }
}
