using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VRArcaneArena.DataStructures;

namespace VRArcaneArena.Managers
{
    /// <summary>
    /// Singleton MonoBehaviour wrapper around cooldown skip-list operations and cooldown ready events.
    /// </summary>
    public sealed class CooldownTracker : MonoBehaviour
    {
        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static CooldownTracker Instance;

        private CooldownSkipList _skipList;

        /// <summary>
        /// Fired when a spell leaves cooldown and becomes available.
        /// Payload is the spell id.
        /// </summary>
        public UnityEvent<string> onSpellReady;

        /// <summary>
        /// Initializes singleton state and creates the cooldown skip list.
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

            _skipList = new CooldownSkipList();
            if (onSpellReady == null)
            {
                onSpellReady = new UnityEvent<string>();
            }
        }

        /// <summary>
        /// Removes expired cooldowns and emits spell-ready events for newly available spells.
        /// </summary>
        /// <remarks>
        /// Complexity: O(k + n + k log n), where k is expired count and n is active cooldown count.
        /// </remarks>
        public void Update()
        {
            var now = Time.time;
            var expiredSpellIds = new List<string>();

            var all = _skipList.GetAllCooldowns();
            for (var i = 0; i < all.Count; i++)
            {
                var entry = all[i];
                if (entry.expiryTimestamp <= now)
                {
                    expiredSpellIds.Add(entry.spellId);
                }
                else
                {
                    break;
                }
            }

            _skipList.RemoveExpired(now);

            for (var i = 0; i < expiredSpellIds.Count; i++)
            {
                onSpellReady.Invoke(expiredSpellIds[i]);
            }
        }

        /// <summary>
        /// Adds or updates a cooldown entry with the specified duration.
        /// </summary>
        /// <param name="spellId">Spell id key.</param>
        /// <param name="spellName">Display name.</param>
        /// <param name="duration">Cooldown duration in seconds.</param>
        /// <remarks>
        /// Complexity: O(log n) expected.
        /// </remarks>
        public void AddCooldown(string spellId, string spellName, float duration)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return;
            }

            var expiry = Time.time + Mathf.Max(0f, duration);
            _skipList.Insert(spellId, spellName, expiry);
        }

        /// <summary>
        /// Determines whether a spell is currently on cooldown.
        /// </summary>
        /// <param name="spellId">Spell id key.</param>
        /// <returns>True when cooldown entry exists; otherwise false.</returns>
        /// <remarks>
        /// Complexity: O(log n) expected.
        /// </remarks>
        public bool IsOnCooldown(string spellId)
        {
            return _skipList.IsOnCooldown(spellId);
        }

        /// <summary>
        /// Returns the soonest-expiring cooldown entry.
        /// </summary>
        /// <returns>Next cooldown entry, or null when empty.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public CooldownEntry GetNextReady()
        {
            return _skipList.PeekNext();
        }

        /// <summary>
        /// Returns all cooldown entries in sorted expiry order.
        /// </summary>
        /// <returns>Ordered cooldown list.</returns>
        /// <remarks>
        /// Complexity: O(n)
        /// </remarks>
        public List<CooldownEntry> GetAllCooldowns()
        {
            return _skipList.GetAllCooldowns();
        }

        /// <summary>
        /// Gets remaining cooldown time for a specific spell.
        /// </summary>
        /// <param name="spellId">Spell id key.</param>
        /// <returns>Remaining seconds; 0 if spell is not on cooldown.</returns>
        /// <remarks>
        /// Complexity: O(n) due to linear scan through sorted cooldown entries.
        /// </remarks>
        public float GetRemainingTime(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId) || !_skipList.IsOnCooldown(spellId))
            {
                return 0f;
            }

            var all = _skipList.GetAllCooldowns();
            for (var i = 0; i < all.Count; i++)
            {
                var entry = all[i];
                if (string.Equals(entry.spellId, spellId, StringComparison.Ordinal))
                {
                    return Mathf.Max(0f, entry.expiryTimestamp - Time.time);
                }
            }

            return 0f;
        }
    }
}
