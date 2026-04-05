using System;
using System.Collections.Generic;
using UnityEngine;
using VRArcaneArena.DataStructures;

namespace VRArcaneArena.Managers
{
    /// <summary>
    /// Singleton MonoBehaviour that tracks enemy threat scores using a Fibonacci max heap.
    /// </summary>
    public sealed class ThreatManager : MonoBehaviour
    {
        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static ThreatManager Instance;

        /// <summary>
        /// Interval in seconds between global threat recomputations.
        /// </summary>
        public float updateInterval = 0.1f;

        private FibonacciHeap<GameObject> _heap;
        private Dictionary<string, FibonacciHeap<GameObject>.FibHeapNode<GameObject>> _nodeByEnemyId;
        private Transform _playerTransform;
        private float _nextUpdateTime;

        /// <summary>
        /// Initializes singleton state and heap containers.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _heap = new FibonacciHeap<GameObject>();
            _nodeByEnemyId = new Dictionary<string, FibonacciHeap<GameObject>.FibHeapNode<GameObject>>(StringComparer.Ordinal);
            _nextUpdateTime = 0f;
        }

        /// <summary>
        /// Caches player transform by looking up the object tagged as Player.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1) average for a tag lookup.
        /// </remarks>
        public void Start()
        {
            var playerObj = GameObject.FindWithTag("Player");
            _playerTransform = playerObj != null ? playerObj.transform : null;
        }

        /// <summary>
        /// Periodically recomputes all tracked enemy threat scores.
        /// </summary>
        /// <remarks>
        /// Complexity: O(n log n) worst-case per update pass, where n is tracked enemies.
        /// </remarks>
        public void Update()
        {
            if (Time.time < _nextUpdateTime)
            {
                return;
            }

            RecalculateAllScores();
            _nextUpdateTime = Time.time + Mathf.Max(0.01f, updateInterval);
        }

        /// <summary>
        /// Registers an enemy in the threat heap with an initial score of zero.
        /// </summary>
        /// <param name="id">Unique enemy id.</param>
        /// <param name="enemy">Enemy object reference.</param>
        /// <remarks>
        /// Complexity: O(1) amortized.
        /// </remarks>
        public void RegisterEnemy(string id, GameObject enemy)
        {
            if (string.IsNullOrWhiteSpace(id) || enemy == null)
            {
                return;
            }

            if (_nodeByEnemyId.TryGetValue(id, out var existingNode))
            {
                _heap.Delete(existingNode);
                _nodeByEnemyId.Remove(id);
            }

            var node = _heap.Insert(enemy, 0f);
            _nodeByEnemyId[id] = node;
        }

        /// <summary>
        /// Unregisters an enemy and removes it from threat tracking.
        /// </summary>
        /// <param name="id">Enemy id to remove.</param>
        /// <remarks>
        /// Complexity: O(log n) amortized.
        /// </remarks>
        public void UnregisterEnemy(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!_nodeByEnemyId.TryGetValue(id, out var node))
            {
                return;
            }

            _heap.Delete(node);
            _nodeByEnemyId.Remove(id);
        }

        /// <summary>
        /// Recomputes threat score for every tracked enemy and updates heap priorities.
        /// </summary>
        /// <remarks>
        /// Complexity: O(n log n) worst-case, O(n) average when scores only increase.
        /// </remarks>
        public void RecalculateAllScores()
        {
            if (_playerTransform == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                _playerTransform = playerObj != null ? playerObj.transform : null;
                if (_playerTransform == null)
                {
                    return;
                }
            }

            var ids = new List<string>(_nodeByEnemyId.Keys);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!_nodeByEnemyId.TryGetValue(id, out var node) || node == null)
                {
                    continue;
                }

                var enemy = node.data;
                if (enemy == null)
                {
                    _heap.Delete(node);
                    _nodeByEnemyId.Remove(id);
                    continue;
                }

                var score = CalculateThreatScore(enemy);
                if (score >= node.key)
                {
                    _heap.IncreaseKey(node, score);
                }
                else
                {
                    _heap.Delete(node);
                    var newNode = _heap.Insert(enemy, score);
                    _nodeByEnemyId[id] = newNode;
                }
            }

            var highestThreatNode = _heap.FindMax();
            var highestThreatObject = highestThreatNode?.data;

            foreach (var kvp in _nodeByEnemyId)
            {
                var heapNode = kvp.Value;
                if (heapNode == null || heapNode.data == null) continue;
                var enemyComponent = heapNode.data.GetComponent<VRArcaneArena.Game.Enemy>();
                if (enemyComponent != null)
                {
                    bool isHighest = heapNode.data == highestThreatObject;
                    enemyComponent.SetThreatColor(heapNode.key, isHighest);
                }
            }
        }

        /// <summary>
        /// Returns the currently highest-threat enemy.
        /// </summary>
        /// <returns>Enemy game object with maximum threat, or null if none.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public GameObject GetHighestThreat()
        {
            var max = _heap.FindMax();
            return max == null ? null : max.data;
        }

        /// <summary>
        /// Returns the current threat score for a given enemy id.
        /// </summary>
        /// <param name="id">Enemy id.</param>
        /// <returns>Current threat score, or 0 when unknown.</returns>
        /// <remarks>
        /// Complexity: O(1) average.
        /// </remarks>
        public float GetThreatScore(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return 0f;
            }

            return _nodeByEnemyId.TryGetValue(id, out var node) && node != null ? node.key : 0f;
        }

        /// <summary>
        /// Calculates threat score based on enemy type as primary factor,
        /// distance as secondary modifier. Boss always outranks archers,
        /// archers always outrank goblins regardless of distance.
        /// </summary>
        /// <param name="enemy">Enemy game object.</param>
        /// <returns>Threat score value.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        private float CalculateThreatScore(GameObject enemy)
        {
            if (enemy == null || _playerTransform == null) return 0f;

            var stats = enemy.GetComponent<VRArcaneArena.Game.EnemyStats>();
            if (stats == null) return 0f;

            var baseThreat = stats.GetBaseThreat();
            var distance = Vector3.Distance(enemy.transform.position, _playerTransform.position);
            var distanceModifier = 1f / Mathf.Max(0.5f, distance);

            // Base threat dominates - distance only acts as tiebreaker within same type
            return baseThreat + distanceModifier;
        }
    }
}
