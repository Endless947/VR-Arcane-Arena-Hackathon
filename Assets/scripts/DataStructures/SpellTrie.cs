using System;
using System.Collections.Generic;

namespace VRArcaneArena.DataStructures
{
    /// <summary>
    /// Prefix tree used to map gesture token sequences to spell metadata.
    /// </summary>
    public sealed class SpellTrie
    {
        /// <summary>
        /// Represents a node in the gesture trie.
        /// </summary>
        public sealed class TrieNode
        {
            /// <summary>
            /// Outgoing edges keyed by gesture token.
            /// </summary>
            public Dictionary<char, TrieNode> children;

            /// <summary>
            /// True if this node terminates a complete spell sequence.
            /// </summary>
            public bool isTerminal;

            /// <summary>
            /// Spell name for terminal nodes.
            /// </summary>
            public string spellName;

            /// <summary>
            /// Spell description for terminal nodes.
            /// </summary>
            public string spellDescription;

            /// <summary>
            /// Initializes an empty trie node.
            /// </summary>
            /// <remarks>
            /// Complexity: O(1)
            /// </remarks>
            public TrieNode()
            {
                children = new Dictionary<char, TrieNode>();
                isTerminal = false;
                spellName = null;
                spellDescription = null;
            }
        }

        private readonly TrieNode _root;
        private TrieNode _current;
        private bool _isValidPrefix;

        /// <summary>
        /// Initializes a new <see cref="SpellTrie"/>.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public SpellTrie()
        {
            _root = new TrieNode();
            _current = _root;
            _isValidPrefix = true;
        }

        /// <summary>
        /// Inserts or updates a spell mapped to a gesture sequence.
        /// </summary>
        /// <param name="gestureSequence">Sequence of gesture tokens (for example, "FP").</param>
        /// <param name="spellName">Display name of the spell.</param>
        /// <param name="spellDescription">Description text of the spell.</param>
        /// <exception cref="ArgumentException">Thrown when any input is null, empty, or whitespace.</exception>
        /// <remarks>
        /// Complexity: O(m), where m is the length of <paramref name="gestureSequence"/>.
        /// </remarks>
        public void Insert(string gestureSequence, string spellName, string spellDescription)
        {
            if (string.IsNullOrWhiteSpace(gestureSequence))
            {
                throw new ArgumentException("Gesture sequence cannot be null or whitespace.", nameof(gestureSequence));
            }

            if (string.IsNullOrWhiteSpace(spellName))
            {
                throw new ArgumentException("Spell name cannot be null or whitespace.", nameof(spellName));
            }

            if (string.IsNullOrWhiteSpace(spellDescription))
            {
                throw new ArgumentException("Spell description cannot be null or whitespace.", nameof(spellDescription));
            }

            var node = _root;
            for (var i = 0; i < gestureSequence.Length; i++)
            {
                var token = gestureSequence[i];
                if (!node.children.TryGetValue(token, out var next))
                {
                    next = new TrieNode();
                    node.children[token] = next;
                }

                node = next;
            }

            node.isTerminal = true;
            node.spellName = spellName;
            node.spellDescription = spellDescription;
        }

        /// <summary>
        /// Advances the traversal pointer by one gesture token.
        /// </summary>
        /// <param name="gestureToken">Next gesture token to consume.</param>
        /// <returns>
        /// The spell name when the new current node is terminal; otherwise <see langword="null"/>.
        /// If traversal leaves the trie, returns <see langword="null"/> and marks the prefix as invalid until <see cref="Reset"/>.
        /// </returns>
        /// <remarks>
        /// Complexity: O(1) average.
        /// </remarks>
        public string Traverse(char gestureToken)
        {
            if (!_isValidPrefix)
            {
                return null;
            }

            if (!_current.children.TryGetValue(gestureToken, out var next))
            {
                _current = null;
                _isValidPrefix = false;
                return null;
            }

            _current = next;
            return _current.isTerminal ? _current.spellName : null;
        }

        /// <summary>
        /// Gets all spell names reachable from the current traversal pointer.
        /// </summary>
        /// <returns>List of reachable spell names sorted in ascending ordinal order.</returns>
        /// <remarks>
        /// Complexity: O(v + s log s), where v is visited trie nodes in the reachable subtree and s is reachable spell count.
        /// </remarks>
        public List<string> GetReachableSpells()
        {
            var result = new List<string>();
            if (!_isValidPrefix || _current == null)
            {
                return result;
            }

            CollectReachableSpellsDepthFirst(_current, result);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// Determines whether the current traversal pointer is still on a valid trie path.
        /// </summary>
        /// <returns><see langword="true"/> when current input is a valid prefix; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public bool IsValidPrefix()
        {
            return _isValidPrefix;
        }

        /// <summary>
        /// Resets traversal state back to the trie root.
        /// </summary>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public void Reset()
        {
            _current = _root;
            _isValidPrefix = true;
        }

        /// <summary>
        /// Gets the current spell name if the traversal pointer is on a terminal node.
        /// </summary>
        /// <returns>Spell name when terminal; otherwise <see langword="null"/>.</returns>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public string GetCurrentSpell()
        {
            if (!_isValidPrefix || _current == null)
            {
                return null;
            }

            return _current.isTerminal ? _current.spellName : null;
        }

        /// <summary>
        /// Loads the default spell set into this trie.
        /// Existing mappings for the same sequences are overwritten.
        /// </summary>
        /// <remarks>
        /// Complexity: O(totalCharacters), where totalCharacters is the sum of all predefined gesture sequence lengths.
        /// </remarks>
        public void LoadDefaultSpells()
        {
            Insert("FP", "Fireball", "Launches a high-damage fire projectile");
            Insert("OOS", "Blizzard", "AoE slow and damage in a sphere");
            Insert("PPF", "Lightning Bolt", "Chain lightning hitting 3 enemies");
            Insert("SO", "Arcane Shield", "Absorbs next 3 hits");
            Insert("FFF", "Meteor Strike", "Massive AoE with long cooldown");
            Insert("OPS", "Gravity Well", "Pulls all enemies toward center");
            Insert("PO", "Frost Nova", "Freezes nearby enemies instantly");
            Insert("SFF", "Void Blast", "Tears a hole dealing massive damage");
        }

        // Complexity: O(v), where v is the number of nodes visited in the current subtree.
        private static void CollectReachableSpellsDepthFirst(TrieNode node, List<string> output)
        {
            if (node.isTerminal && !string.IsNullOrEmpty(node.spellName))
            {
                output.Add(node.spellName);
            }

            foreach (var pair in node.children)
            {
                CollectReachableSpellsDepthFirst(pair.Value, output);
            }
        }
    }
}
