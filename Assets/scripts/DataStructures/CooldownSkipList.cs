using System;
using System.Collections.Generic;

namespace VRArcaneArena.DataStructures
{
    /// <summary>
    /// Represents one active spell cooldown entry.
    /// </summary>
    public sealed class CooldownEntry
    {
        /// <summary>
        /// Unique spell id key.
        /// </summary>
        public string spellId;

        /// <summary>
        /// Absolute timestamp when cooldown expires.
        /// </summary>
        public float expiryTimestamp;

        /// <summary>
        /// User-facing spell name.
        /// </summary>
        public string spellName;

        /// <summary>
        /// Initializes a new cooldown entry.
        /// </summary>
        /// <param name="spellId">Unique spell id.</param>
        /// <param name="spellName">Display name.</param>
        /// <param name="expiryTimestamp">Expiry timestamp.</param>
        /// <exception cref="ArgumentException">Thrown when spellId is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when expiry timestamp is NaN.</exception>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public CooldownEntry(string spellId, string spellName, float expiryTimestamp)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                throw new ArgumentException("spellId cannot be null or whitespace.", nameof(spellId));
            }

            if (float.IsNaN(expiryTimestamp))
            {
                throw new ArgumentOutOfRangeException(nameof(expiryTimestamp), "expiryTimestamp cannot be NaN.");
            }

            this.spellId = spellId;
            this.spellName = spellName ?? string.Empty;
            this.expiryTimestamp = expiryTimestamp;
        }
    }

    /// <summary>
    /// Skip list specialized for cooldowns ordered by expiry timestamp.
    /// </summary>
    public sealed class CooldownSkipList
    {
        /// <summary>
        /// Node in the cooldown skip list.
        /// </summary>
        public sealed class SkipListNode
        {
            /// <summary>
            /// Stored entry for this node. Sentinel nodes use null.
            /// </summary>
            public CooldownEntry entry;

            /// <summary>
            /// Forward pointers by level.
            /// </summary>
            public SkipListNode[] forward;

            /// <summary>
            /// Initializes a skip list node.
            /// </summary>
            /// <param name="level">Number of levels for this node.</param>
            /// <param name="entry">Payload entry.</param>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when level is less than 1.</exception>
            /// <remarks>
            /// Complexity: O(1)
            /// </remarks>
            public SkipListNode(int level, CooldownEntry entry)
            {
                if (level < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(level), "level must be at least 1.");
                }

                this.entry = entry;
                forward = new SkipListNode[level];
            }
        }

        /// <summary>
        /// Maximum number of levels in this skip list.
        /// </summary>
        public int maxLevels = 16;

        /// <summary>
        /// Promotion probability for level generation.
        /// </summary>
        public float probability = 0.5f;

        /// <summary>
        /// Random number generator used for level selection.
        /// </summary>
        public Random rng;

        /// <summary>
        /// Number of entries currently stored.
        /// </summary>
        public int Count { get; private set; }

        private readonly SkipListNode _head;
        private readonly SortedDictionary<string, CooldownEntry> _entriesBySpellId;
        private int _currentLevel;

        /// <summary>
        /// Initializes a new cooldown skip list.
        /// </summary>
        /// <param name="seed">Optional deterministic random seed.</param>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public CooldownSkipList(int? seed = null)
        {
            rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _head = new SkipListNode(16, null);
            _entriesBySpellId = new SortedDictionary<string, CooldownEntry>(StringComparer.Ordinal);
            _currentLevel = 1;
            Count = 0;
        }

        /// <summary>
        /// Inserts or updates a cooldown entry.
        /// </summary>
        /// <param name="spellId">Unique spell id.</param>
        /// <param name="spellName">Display spell name.</param>
        /// <param name="expiryTimestamp">Absolute expiry timestamp.</param>
        /// <exception cref="ArgumentException">Thrown when spellId is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when expiryTimestamp is NaN.</exception>
        /// <remarks>
        /// Complexity: O(log n) expected.
        /// </remarks>
        public void Insert(string spellId, string spellName, float expiryTimestamp)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                throw new ArgumentException("spellId cannot be null or whitespace.", nameof(spellId));
            }

            if (float.IsNaN(expiryTimestamp))
            {
                throw new ArgumentOutOfRangeException(nameof(expiryTimestamp), "expiryTimestamp cannot be NaN.");
            }

            if (_entriesBySpellId.TryGetValue(spellId, out var existing))
            {
                RemoveNodeByKey(existing.expiryTimestamp, spellId);
                _entriesBySpellId.Remove(spellId);
                Count--;
            }

            var newEntry = new CooldownEntry(spellId, spellName, expiryTimestamp);
            var update = FindUpdateArray(expiryTimestamp);
            var level = GenerateRandomLevel();

            if (level > _currentLevel)
            {
                for (var i = _currentLevel; i < level; i++)
                {
                    update[i] = _head;
                }

                _currentLevel = level;
            }

            var insertionPredecessor = update[0];
            while (insertionPredecessor.forward[0] != null &&
                   insertionPredecessor.forward[0].entry.expiryTimestamp == expiryTimestamp &&
                   string.CompareOrdinal(insertionPredecessor.forward[0].entry.spellId, spellId) < 0)
            {
                insertionPredecessor = insertionPredecessor.forward[0];
            }

            update[0] = insertionPredecessor;

            for (var i = 1; i < level; i++)
            {
                var predecessor = update[i];
                while (predecessor.forward[i] != null &&
                       predecessor.forward[i].entry.expiryTimestamp == expiryTimestamp &&
                       string.CompareOrdinal(predecessor.forward[i].entry.spellId, spellId) < 0)
                {
                    predecessor = predecessor.forward[i];
                }

                update[i] = predecessor;
            }

            var newNode = new SkipListNode(level, newEntry);
            for (var i = 0; i < level; i++)
            {
                newNode.forward[i] = update[i].forward[i];
                update[i].forward[i] = newNode;
            }

            _entriesBySpellId[spellId] = newEntry;
            Count++;
        }

        /// <summary>
        /// Removes all expired entries from the list front where expiry is less than or equal to current time.
        /// </summary>
        /// <param name="currentTime">Current absolute time.</param>
        /// <remarks>
        /// Complexity: O(k log n), where k is the number of removed entries.
        /// </remarks>
        public void RemoveExpired(float currentTime)
        {
            if (float.IsNaN(currentTime))
            {
                throw new ArgumentOutOfRangeException(nameof(currentTime), "currentTime cannot be NaN.");
            }

            var node = _head.forward[0];
            while (node != null && node.entry.expiryTimestamp <= currentTime)
            {
                RemoveNodeByKey(node.entry.expiryTimestamp, node.entry.spellId);
                _entriesBySpellId.Remove(node.entry.spellId);
                Count--;
                node = _head.forward[0];
            }
        }

        /// <summary>
        /// Returns the soonest expiring cooldown entry.
        /// </summary>
        /// <returns>The front entry, or <see langword="null"/> when empty.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public CooldownEntry PeekNext()
        {
            var first = _head.forward[0];
            return first == null ? null : first.entry;
        }

        /// <summary>
        /// Checks whether a spell currently exists in the cooldown list.
        /// </summary>
        /// <param name="spellId">Spell id to search.</param>
        /// <returns><see langword="true"/> when the spell is present; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// Complexity: O(log n) expected via balanced key lookup.
        /// </remarks>
        public bool IsOnCooldown(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return false;
            }

            return _entriesBySpellId.ContainsKey(spellId);
        }

        /// <summary>
        /// Returns all cooldown entries in ascending expiry order.
        /// </summary>
        /// <returns>Sorted list of cooldown entries.</returns>
        /// <remarks>
        /// Complexity: O(n)
        /// </remarks>
        public List<CooldownEntry> GetAllCooldowns()
        {
            var result = new List<CooldownEntry>(Count);
            var node = _head.forward[0];
            while (node != null)
            {
                result.Add(node.entry);
                node = node.forward[0];
            }

            return result;
        }

        /// <summary>
        /// Removes all cooldown entries and resets list state.
        /// </summary>
        /// <remarks>
        /// Complexity: O(maxLevels)
        /// </remarks>
        public void Clear()
        {
            for (var i = 0; i < maxLevels; i++)
            {
                _head.forward[i] = null;
            }

            _entriesBySpellId.Clear();
            _currentLevel = 1;
            Count = 0;
        }

        // Complexity: O(1) expected.
        private int GenerateRandomLevel()
        {
            var level = 1;
            while (level < maxLevels && rng.NextDouble() < probability)
            {
                level++;
            }

            return level;
        }

        // Complexity: O(log n) expected.
        private SkipListNode[] FindUpdateArray(float expiryTimestamp)
        {
            var update = new SkipListNode[maxLevels];
            var current = _head;

            for (var level = _currentLevel - 1; level >= 0; level--)
            {
                while (current.forward[level] != null &&
                       current.forward[level].entry.expiryTimestamp < expiryTimestamp)
                {
                    current = current.forward[level];
                }

                update[level] = current;
            }

            for (var level = _currentLevel; level < maxLevels; level++)
            {
                update[level] = _head;
            }

            return update;
        }

        // Complexity: O(log n) expected.
        private void RemoveNodeByKey(float expiryTimestamp, string spellId)
        {
            var update = FindUpdateArray(expiryTimestamp);
            var candidate = update[0].forward[0];

            while (candidate != null && candidate.entry.expiryTimestamp == expiryTimestamp)
            {
                if (string.Equals(candidate.entry.spellId, spellId, StringComparison.Ordinal))
                {
                    break;
                }

                update[0] = candidate;
                candidate = candidate.forward[0];
            }

            if (candidate == null || candidate.entry.expiryTimestamp != expiryTimestamp)
            {
                return;
            }

            if (!string.Equals(candidate.entry.spellId, spellId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeLevelCount = candidate.forward.Length;
            for (var level = 0; level < nodeLevelCount; level++)
            {
                var predecessor = update[level];
                while (predecessor.forward[level] != null && predecessor.forward[level] != candidate)
                {
                    predecessor = predecessor.forward[level];
                }

                if (predecessor.forward[level] == candidate)
                {
                    predecessor.forward[level] = candidate.forward[level];
                }
            }

            while (_currentLevel > 1 && _head.forward[_currentLevel - 1] == null)
            {
                _currentLevel--;
            }
        }
    }
}
