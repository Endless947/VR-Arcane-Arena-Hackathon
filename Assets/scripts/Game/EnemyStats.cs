using System;
using UnityEngine;

namespace VRArcaneArena.Game
{
    /// <summary>
    /// Defines enemy archetype which determines base stats and threat level.
    /// </summary>
    public enum EnemyType
    {
        Goblin = 0,
        GoblinArcher = 1,
        GoblinBoss = 2
    }

    /// <summary>
    /// Simple enemy stat container used by gameplay and manager systems.
    /// </summary>
    public sealed class EnemyStats : MonoBehaviour
    {
        /// <summary>
        /// Enemy movement speed in world units per second.
        /// </summary>
        public float speed = 3f;

        /// <summary>
        /// Damage dealt by this enemy.
        /// </summary>
        public float damage = 10f;

        /// <summary>
        /// Current health value.
        /// </summary>
        public float health = 100f;

        /// <summary>
        /// Maximum health value.
        /// </summary>
        public float maxHealth = 100f;

        /// <summary>
        /// Unique identifier for this enemy instance.
        /// </summary>
        public string enemyId;

        /// <summary>
        /// Archetype of this enemy. Determines base stats and threat contribution.
        /// </summary>
        public EnemyType enemyType = EnemyType.Goblin;

        /// <summary>
        /// Generates a fresh GUID for this enemy instance.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public void Awake()
        {
            enemyId = Guid.NewGuid().ToString();
            ApplyTypeStats();
        }

        /// <summary>
        /// Applies base stats based on enemy type.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        private void ApplyTypeStats()
        {
            switch (enemyType)
            {
                case EnemyType.Goblin:
                    speed = 0.8f;
                    damage = 5f;
                    health = 50f;
                    maxHealth = 50f;
                    break;
                case EnemyType.GoblinArcher:
                    speed = 1.0f;
                    damage = 15f;
                    health = 100f;
                    maxHealth = 100f;
                    break;
                case EnemyType.GoblinBoss:
                    speed = 0.6f;
                    damage = 50f;
                    health = 150f;
                    maxHealth = 150f;
                    break;
            }
        }

        /// <summary>
        /// Applies incoming damage and clamps health to a minimum of zero.
        /// </summary>
        /// <param name="amount">Damage amount to apply.</param>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public void TakeDamage(float amount)
        {
            health -= Mathf.Max(0f, amount);
            health = Mathf.Max(0f, health);
        }

        /// <summary>
        /// Determines whether this enemy has no remaining health.
        /// </summary>
        /// <returns>True when health is less than or equal to zero; otherwise false.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public bool IsDead()
        {
            return health <= 0f;
        }

        /// <summary>
        /// Returns base threat value for this enemy type.
        /// Boss = 100, Archer = 10, Goblin = 1.
        /// Used as base key in Fibonacci Heap.
        /// </summary>
        /// <returns>Base threat value for the current enemy archetype.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public float GetBaseThreat()
        {
            switch (enemyType)
            {
                case EnemyType.GoblinBoss:   return 100f;
                case EnemyType.GoblinArcher: return 10f;
                default:                     return 1f;
            }
        }
    }
}
