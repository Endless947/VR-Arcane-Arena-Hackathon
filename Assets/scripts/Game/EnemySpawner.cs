using UnityEngine;

namespace VRArcaneArena.Game
{
    /// <summary>
    /// Spawns enemy waves in formation - Boss at back, Archers flanking, Goblins in front.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject goblinPrefab;
        public GameObject archerPrefab;
        public GameObject bossPrefab;

        [Header("Spawn Settings")]
        public float spawnRadius = 18f;
        public float timeBetweenWaves = 25f;
        public int waveNumber = 0;

        [Header("Demo Mode")]
        public bool demoMode = false;
        public float demoFocusAngle = 0f;

        private float _nextWaveTime;

        /// <summary>
        /// Schedules first wave after short delay.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public void Start()
        {
            _nextWaveTime = Time.time + 3f;
        }

        /// <summary>
        /// Triggers wave spawn when timer elapses.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1) when no spawn, O(k) on spawn where k is enemies instantiated in the wave.
        /// </remarks>
        public void Update()
        {
        }

        /// <summary>
        /// Spawns exactly enemyCount enemies using the same formation layout.
        /// 1 boss, up to 2 archers, and remaining as goblins.
        /// </summary>
        /// <param name="enemyCount">Exact number of enemies to spawn.</param>
        /// <remarks>
        /// Complexity: O(k), where k is total enemies spawned in the wave.
        /// </remarks>
        public void SpawnWave(int enemyCount)
        {
            if (enemyCount <= 0)
            {
                return;
            }

            var player = GameObject.FindWithTag("Player");
            var playerForward = player != null ? player.transform.forward : Vector3.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.0001f)
            {
                playerForward = Vector3.forward;
            }

            playerForward.Normalize();
            var centerAngleDeg = Mathf.Atan2(playerForward.z, playerForward.x) * Mathf.Rad2Deg;
            var centerAngleRad = centerAngleDeg * Mathf.Deg2Rad;

            var forward = new Vector3(Mathf.Cos(centerAngleRad), 0f, Mathf.Sin(centerAngleRad));
            var right = new Vector3(-forward.z, 0f, forward.x);

            // Formation spawns INSIDE arena at half spawn radius
            var center = forward * (spawnRadius * 0.4f);

            // Drop height - enemies fall from sky
            var dropHeight = 12f;

            var remaining = enemyCount;

            // Boss at back center of formation
            if (remaining > 0)
            {
                var bossPos = center + forward * 2f;
                SpawnEnemy(bossPrefab, bossPos + Vector3.up * dropHeight, EnemyType.GoblinBoss);
                remaining--;

                // Archers directly beside the boss
                if (remaining > 0)
                {
                    SpawnEnemy(archerPrefab, bossPos + right * 2f + Vector3.up * dropHeight, EnemyType.GoblinArcher);
                    remaining--;
                }

                if (remaining > 0)
                {
                    SpawnEnemy(archerPrefab, bossPos + right * -2f + Vector3.up * dropHeight, EnemyType.GoblinArcher);
                    remaining--;
                }
            }

            // Goblins in FRONT of boss (between boss and player)
            var goblinCount = remaining;
            for (var i = 0; i < goblinCount; i++)
            {
                var spread = (i - goblinCount * 0.5f) * 1.8f;
                var goblinPos = center - forward * 2f + right * spread;
                SpawnEnemy(goblinPrefab, goblinPos + Vector3.up * dropHeight, EnemyType.Goblin);
            }

            _nextWaveTime = Time.time + Mathf.Max(1f, timeBetweenWaves);
        }

        /// <summary>
        /// Spawns a formation wave - 1 boss at back center,
        /// 2 archers flanking the boss, 3-5 goblins spread in front.
        /// Formation is oriented toward the player (positive Z in demo mode).
        /// </summary>
        /// <remarks>
        /// Complexity: O(k), where k is total enemies spawned in the wave.
        /// </remarks>
        public void SpawnWave()
        {
            waveNumber++;

            var player = GameObject.FindWithTag("Player");
            var playerForward = player != null ? player.transform.forward : Vector3.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.0001f)
            {
                playerForward = Vector3.forward;
            }

            playerForward.Normalize();
            var centerAngleDeg = Mathf.Atan2(playerForward.z, playerForward.x) * Mathf.Rad2Deg;
            var centerAngleRad = centerAngleDeg * Mathf.Deg2Rad;

            var forward = new Vector3(Mathf.Cos(centerAngleRad), 0f, Mathf.Sin(centerAngleRad));
            var right = new Vector3(-forward.z, 0f, forward.x);
            
            // Formation spawns INSIDE arena at half spawn radius
            var center = forward * (spawnRadius * 0.4f);

            // Drop height - enemies fall from sky
            var dropHeight = 12f;

            // Boss at back center of formation
            var bossPos = center + forward * 2f;
            SpawnEnemy(bossPrefab, bossPos + Vector3.up * dropHeight, EnemyType.GoblinBoss);

            // Archers directly beside the boss
            SpawnEnemy(archerPrefab, bossPos + right * 2f + Vector3.up * dropHeight, EnemyType.GoblinArcher);
            SpawnEnemy(archerPrefab, bossPos + right * -2f + Vector3.up * dropHeight, EnemyType.GoblinArcher);

            // Goblins in FRONT of boss (between boss and player)
            var goblinCount = Mathf.Min(3 + waveNumber, 8);
            for (var i = 0; i < goblinCount; i++)
            {
                var spread = (i - goblinCount * 0.5f) * 1.8f;
                var goblinPos = center - forward * 2f + right * spread;
                SpawnEnemy(goblinPrefab, goblinPos + Vector3.up * dropHeight, EnemyType.Goblin);
            }

            _nextWaveTime = Time.time + Mathf.Max(1f, timeBetweenWaves);
        }

        /// <summary>
        /// Instantiates an enemy prefab and sets its type.
        /// </summary>
        /// <param name="prefab">Prefab to instantiate.</param>
        /// <param name="position">World spawn position.</param>
        /// <param name="type">Enemy archetype to assign.</param>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        private void SpawnEnemy(GameObject prefab, Vector3 position, EnemyType type)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, position, Quaternion.identity);
            var stats = go.GetComponent<EnemyStats>();
            if (stats != null)
            {
                stats.enemyType = type;
                stats.Awake(); // re-apply stats for assigned type
            }
        }
    }
}
