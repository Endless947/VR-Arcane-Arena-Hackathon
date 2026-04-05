using System;
using System.Collections.Generic;

namespace VRArcaneArena.DataStructures
{
    /// <summary>
    /// Maximum-priority Fibonacci heap implementation.
    /// </summary>
    /// <typeparam name="T">Payload type stored in each heap node.</typeparam>
    public sealed class FibonacciHeap<T>
    {
        /// <summary>
        /// Node stored in the Fibonacci heap.
        /// </summary>
        /// <typeparam name="TValue">Payload type.</typeparam>
        public sealed class FibHeapNode<TValue>
        {
            /// <summary>
            /// User payload.
            /// </summary>
            public TValue data;

            /// <summary>
            /// Priority key used by the max-heap.
            /// </summary>
            public float key;

            /// <summary>
            /// Number of direct children.
            /// </summary>
            public int degree;

            /// <summary>
            /// Mark used for cascading cuts.
            /// </summary>
            public bool marked;

            /// <summary>
            /// Parent pointer, or null when node is in root list.
            /// </summary>
            public FibHeapNode<TValue> parent;

            /// <summary>
            /// One child pointer (siblings are reached via circular list), or null if none.
            /// </summary>
            public FibHeapNode<TValue> child;

            /// <summary>
            /// Left sibling in circular doubly linked list.
            /// </summary>
            public FibHeapNode<TValue> left;

            /// <summary>
            /// Right sibling in circular doubly linked list.
            /// </summary>
            public FibHeapNode<TValue> right;

            /// <summary>
            /// Initializes a new heap node.
            /// </summary>
            /// <param name="data">Payload value.</param>
            /// <param name="key">Priority key.</param>
            /// <exception cref="ArgumentException">Thrown when key is NaN.</exception>
            /// <remarks>
            /// Complexity: O(1)
            /// </remarks>
            public FibHeapNode(TValue data, float key)
            {
                if (float.IsNaN(key))
                {
                    throw new ArgumentException("Key cannot be NaN.", nameof(key));
                }

                this.data = data;
                this.key = key;
                degree = 0;
                marked = false;
                parent = null;
                child = null;
                left = this;
                right = this;
            }
        }

        private FibHeapNode<T> _max;

        /// <summary>
        /// Gets the total number of nodes currently in the heap.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Initializes an empty Fibonacci max heap.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public FibonacciHeap()
        {
            _max = null;
            Count = 0;
        }

        /// <summary>
        /// Inserts an item with the specified key into the heap.
        /// </summary>
        /// <param name="data">Payload to store.</param>
        /// <param name="key">Priority key.</param>
        /// <returns>The created node handle.</returns>
        /// <exception cref="ArgumentException">Thrown when key is NaN.</exception>
        /// <remarks>
        /// Complexity: O(1) amortized.
        /// </remarks>
        public FibHeapNode<T> Insert(T data, float key)
        {
            var node = new FibHeapNode<T>(data, key);
            AddToRootList(node);

            if (_max == null || node.key > _max.key)
            {
                _max = node;
            }

            Count++;
            return node;
        }

        /// <summary>
        /// Returns the current maximum node without removing it.
        /// </summary>
        /// <returns>The max node, or <see langword="null"/> if heap is empty.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public FibHeapNode<T> FindMax()
        {
            return _max;
        }

        /// <summary>
        /// Removes and returns the current maximum node.
        /// </summary>
        /// <returns>The extracted max node, or <see langword="null"/> if heap is empty.</returns>
        /// <remarks>
        /// Complexity: O(log n) amortized.
        /// </remarks>
        public FibHeapNode<T> ExtractMax()
        {
            var z = _max;
            if (z == null)
            {
                return null;
            }

            if (z.child != null)
            {
                var children = EnumerateCircularList(z.child);
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    child.parent = null;
                    child.marked = false;
                    AddToRootList(child);
                }

                z.child = null;
                z.degree = 0;
            }

            if (z.right == z)
            {
                _max = null;
            }
            else
            {
                var next = z.right;
                RemoveFromRootList(z);
                _max = next;
                Consolidate();
            }

            z.left = z;
            z.right = z;
            z.parent = null;
            Count--;
            return z;
        }

        /// <summary>
        /// Increases the key of a node and restores heap invariants.
        /// </summary>
        /// <param name="node">Node to update.</param>
        /// <param name="newKey">New key value. Must be greater than or equal to current key.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when new key is NaN.</exception>
        /// <exception cref="InvalidOperationException">Thrown when new key is smaller than current key.</exception>
        /// <remarks>
        /// Complexity: O(1) amortized.
        /// </remarks>
        public void IncreaseKey(FibHeapNode<T> node, float newKey)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (float.IsNaN(newKey))
            {
                throw new ArgumentException("Key cannot be NaN.", nameof(newKey));
            }

            if (newKey < node.key)
            {
                throw new InvalidOperationException("New key must be greater than or equal to current key.");
            }

            node.key = newKey;
            var parent = node.parent;
            if (parent != null && node.key > parent.key)
            {
                Cut(node, parent);
                CascadingCut(parent);
            }

            if (_max == null || node.key > _max.key)
            {
                _max = node;
            }
        }

        /// <summary>
        /// Deletes a node from the heap.
        /// </summary>
        /// <param name="node">Node to delete.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        /// <remarks>
        /// Complexity: O(log n) amortized.
        /// </remarks>
        public void Delete(FibHeapNode<T> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            IncreaseKey(node, float.PositiveInfinity);
            ExtractMax();
        }

        /// <summary>
        /// Removes all nodes from the heap.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public void Clear()
        {
            _max = null;
            Count = 0;
        }

        // Complexity: O(1)
        private void AddToRootList(FibHeapNode<T> node)
        {
            if (_max == null)
            {
                node.left = node;
                node.right = node;
                _max = node;
                return;
            }

            node.left = _max;
            node.right = _max.right;
            _max.right.left = node;
            _max.right = node;
            node.parent = null;
        }

        // Complexity: O(1)
        private void RemoveFromRootList(FibHeapNode<T> node)
        {
            node.left.right = node.right;
            node.right.left = node.left;
            node.left = node;
            node.right = node;
        }

        // Complexity: O(log n) amortized.
        private void Consolidate()
        {
            var roots = EnumerateCircularList(_max);
            var degreeTable = new List<FibHeapNode<T>>();

            for (var i = 0; i < roots.Count; i++)
            {
                var w = roots[i];
                var x = w;
                var d = x.degree;

                while (true)
                {
                    EnsureCapacity(degreeTable, d);
                    var y = degreeTable[d];
                    if (y == null)
                    {
                        break;
                    }

                    if (x.key < y.key)
                    {
                        var temp = x;
                        x = y;
                        y = temp;
                    }

                    Link(y, x);
                    degreeTable[d] = null;
                    d++;
                }

                degreeTable[d] = x;
            }

            _max = null;
            for (var i = 0; i < degreeTable.Count; i++)
            {
                var node = degreeTable[i];
                if (node == null)
                {
                    continue;
                }

                node.left = node;
                node.right = node;
                if (_max == null)
                {
                    _max = node;
                }
                else
                {
                    AddToRootList(node);
                    if (node.key > _max.key)
                    {
                        _max = node;
                    }
                }
            }
        }

        // Complexity: O(1)
        private void Link(FibHeapNode<T> child, FibHeapNode<T> parent)
        {
            RemoveFromRootList(child);
            child.parent = parent;
            child.marked = false;

            if (parent.child == null)
            {
                parent.child = child;
                child.left = child;
                child.right = child;
            }
            else
            {
                child.left = parent.child;
                child.right = parent.child.right;
                parent.child.right.left = child;
                parent.child.right = child;
            }

            parent.degree++;
        }

        // Complexity: O(1)
        private void Cut(FibHeapNode<T> node, FibHeapNode<T> parent)
        {
            if (node.right == node)
            {
                parent.child = null;
            }
            else
            {
                if (parent.child == node)
                {
                    parent.child = node.right;
                }

                node.left.right = node.right;
                node.right.left = node.left;
            }

            parent.degree--;
            node.left = node;
            node.right = node;
            node.parent = null;
            node.marked = false;
            AddToRootList(node);
        }

        // Complexity: O(log n) amortized over a sequence of operations.
        private void CascadingCut(FibHeapNode<T> node)
        {
            var parent = node.parent;
            if (parent == null)
            {
                return;
            }

            if (!node.marked)
            {
                node.marked = true;
                return;
            }

            Cut(node, parent);
            CascadingCut(parent);
        }

        // Complexity: O(r), where r is the number of nodes in the circular list.
        private static List<FibHeapNode<T>> EnumerateCircularList(FibHeapNode<T> start)
        {
            var result = new List<FibHeapNode<T>>();
            if (start == null)
            {
                return result;
            }

            var current = start;
            do
            {
                result.Add(current);
                current = current.right;
            }
            while (current != start);

            return result;
        }

        // Complexity: O(log n) worst-case due to list growth strategy over all consolidations.
        private static void EnsureCapacity(List<FibHeapNode<T>> degreeTable, int index)
        {
            while (degreeTable.Count <= index)
            {
                degreeTable.Add(null);
            }
        }
    }
}
