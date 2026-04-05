using System;
using System.Collections.Generic;
using System.Numerics;

namespace VRArcaneArena.DataStructures
{
    /// <summary>
    /// Represents an entity that can be inserted into an <see cref="Octree"/>.
    /// </summary>
    public sealed class OctreeEntity
    {
        /// <summary>
        /// Unique identifier for this entity.
        /// </summary>
        public string id;

        /// <summary>
        /// XYZ position as a float array of length 3.
        /// </summary>
        public float[] position;

        /// <summary>
        /// Arbitrary payload attached to this entity.
        /// </summary>
        public object data;

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeEntity"/> class.
        /// </summary>
        /// <param name="id">Unique entity identifier.</param>
        /// <param name="position">XYZ position array (length 3).</param>
        /// <param name="data">Arbitrary payload object.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="position"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="position"/> does not have length 3.</exception>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public OctreeEntity(string id, float[] position, object data)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Entity id cannot be null or whitespace.", nameof(id));
            }

            ValidatePosition(position, nameof(position));

            this.id = id;
            this.position = Clone3(position);
            this.data = data;
        }

        internal static void ValidatePosition(float[] pos, string paramName)
        {
            if (pos == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (pos.Length != 3)
            {
                throw new ArgumentOutOfRangeException(paramName, "Position array must have length 3.");
            }

            if (!IsFinite(pos[0]) || !IsFinite(pos[1]) || !IsFinite(pos[2]))
            {
                throw new ArgumentException("Position coordinates must be finite numbers.", paramName);
            }
        }

        internal static float[] Clone3(float[] src)
        {
            return new[] { src[0], src[1], src[2] };
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Spatial partitioning data structure for efficient 3D point insertion and radius queries.
    /// This implementation is pure C# and has no Unity dependency.
    /// </summary>
    public sealed class Octree
    {
        /// <summary>
        /// Node representation used by the octree.
        /// </summary>
        public sealed class OctreeNode
        {
            /// <summary>
            /// Node center as XYZ float array (length 3).
            /// </summary>
            public float[] center;

            /// <summary>
            /// Half-size of this node's axis-aligned cubic bounds.
            /// </summary>
            public float halfSize;

            /// <summary>
            /// Entities stored in this node.
            /// </summary>
            public List<OctreeEntity> entities;

            /// <summary>
            /// Eight child nodes, or null when this node is a leaf.
            /// </summary>
            public OctreeNode[] children;

            /// <summary>
            /// Depth of this node where root depth is 0.
            /// </summary>
            public int depth;

            /// <summary>
            /// Initializes a new node.
            /// </summary>
            /// <param name="center">Node center (length 3).</param>
            /// <param name="halfSize">Half-size of the cubic node bounds.</param>
            /// <param name="depth">Depth of this node.</param>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="halfSize"/> is not positive.</exception>
            /// <remarks>
            /// Complexity: O(1)
            /// </remarks>
            public OctreeNode(float[] center, float halfSize, int depth)
            {
                OctreeEntity.ValidatePosition(center, nameof(center));

                if (!OctreeEntity.IsFinite(halfSize) || halfSize <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(halfSize), "Half-size must be a positive finite number.");
                }

                this.center = OctreeEntity.Clone3(center);
                this.halfSize = halfSize;
                this.depth = depth;
                this.entities = new List<OctreeEntity>();
                this.children = null;
            }
        }

        /// <summary>
        /// Maximum number of entities a leaf node may contain before subdivision.
        /// </summary>
        public int maxEntitiesPerNode;

        /// <summary>
        /// Maximum tree depth where root depth is 0.
        /// </summary>
        public int maxDepth;

        /// <summary>
        /// Root node of the octree.
        /// </summary>
        public OctreeNode root;

        private readonly Dictionary<string, OctreeEntity> _entitiesById;
        private readonly Dictionary<string, OctreeNode> _entityNodeById;
        private readonly Dictionary<OctreeNode, OctreeNode> _parentByNode;

        /// <summary>
        /// Initializes a new octree for a cubic world-space region.
        /// </summary>
        /// <param name="rootCenter">Root center as XYZ float array (length 3).</param>
        /// <param name="rootHalfSize">Root half-size of the cubic bounds.</param>
        /// <param name="maxEntitiesPerNode">Max entities in a leaf before subdivision. Default is 4.</param>
        /// <param name="maxDepth">Maximum octree depth. Default is 8.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="rootHalfSize"/> is not positive, or constraints are invalid.
        /// </exception>
        /// <remarks>
        /// Complexity: O(1)
        /// </remarks>
        public Octree(float[] rootCenter, float rootHalfSize, int maxEntitiesPerNode = 4, int maxDepth = 8)
        {
            OctreeEntity.ValidatePosition(rootCenter, nameof(rootCenter));

            if (!OctreeEntity.IsFinite(rootHalfSize) || rootHalfSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rootHalfSize), "Root half-size must be a positive finite number.");
            }

            if (maxEntitiesPerNode <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntitiesPerNode), "maxEntitiesPerNode must be greater than 0.");
            }

            if (maxDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth must be 0 or greater.");
            }

            this.maxEntitiesPerNode = maxEntitiesPerNode;
            this.maxDepth = maxDepth;
            this.root = new OctreeNode(rootCenter, rootHalfSize, 0);

            _entitiesById = new Dictionary<string, OctreeEntity>(StringComparer.Ordinal);
            _entityNodeById = new Dictionary<string, OctreeNode>(StringComparer.Ordinal);
            _parentByNode = new Dictionary<OctreeNode, OctreeNode>();
        }

        /// <summary>
        /// Inserts an entity into the octree.
        /// </summary>
        /// <param name="entity">Entity to insert.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity has invalid id or position.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an entity with the same id already exists.</exception>
        /// <remarks>
        /// Average complexity: O(log n)
        /// Worst-case complexity: O(d), where d is <see cref="maxDepth"/>.
        /// </remarks>
        public void Insert(OctreeEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (string.IsNullOrWhiteSpace(entity.id))
            {
                throw new ArgumentException("Entity id cannot be null or whitespace.", nameof(entity));
            }

            OctreeEntity.ValidatePosition(entity.position, nameof(entity));

            if (_entitiesById.ContainsKey(entity.id))
            {
                throw new InvalidOperationException($"Entity with id '{entity.id}' already exists.");
            }

            entity.position = OctreeEntity.Clone3(entity.position);
            _entitiesById.Add(entity.id, entity);
            InsertIntoNode(root, entity);
        }

        /// <summary>
        /// Removes an entity by id.
        /// </summary>
        /// <param name="entityId">Id of the entity to remove.</param>
        /// <remarks>
        /// Average complexity: O(log n)
        /// Worst-case complexity: O(n) when merge checks traverse large portions of the tree.
        /// If the id does not exist, this method performs no action.
        /// </remarks>
        public void Remove(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return;
            }

            if (!_entitiesById.TryGetValue(entityId, out var entity))
            {
                return;
            }

            if (_entityNodeById.TryGetValue(entityId, out var node))
            {
                node.entities.Remove(entity);
                _entityNodeById.Remove(entityId);
                TryMergeUpwards(node);
            }

            _entitiesById.Remove(entityId);
        }

        /// <summary>
        /// Updates the position of an existing entity.
        /// </summary>
        /// <param name="entityId">Id of the entity to update.</param>
        /// <param name="newPosition">New XYZ position array (length 3).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="entityId"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="newPosition"/> has invalid shape.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when no entity with <paramref name="entityId"/> exists.</exception>
        /// <remarks>
        /// Average complexity: O(log n)
        /// Worst-case complexity: O(n) due to potential merge operations after relocation.
        /// </remarks>
        public void UpdatePosition(string entityId, float[] newPosition)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException("Entity id cannot be null or whitespace.", nameof(entityId));
            }

            OctreeEntity.ValidatePosition(newPosition, nameof(newPosition));

            if (!_entitiesById.TryGetValue(entityId, out var entity))
            {
                throw new KeyNotFoundException($"No entity with id '{entityId}' was found.");
            }

            if (_entityNodeById.TryGetValue(entityId, out var oldNode))
            {
                oldNode.entities.Remove(entity);
                _entityNodeById.Remove(entityId);
                TryMergeUpwards(oldNode);
            }

            entity.position = OctreeEntity.Clone3(newPosition);
            InsertIntoNode(root, entity);
        }

        /// <summary>
        /// Returns all entities whose positions lie inside or on a sphere.
        /// </summary>
        /// <param name="center">Sphere center XYZ array (length 3).</param>
        /// <param name="radius">Sphere radius.</param>
        /// <returns>List of matching entities.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is negative or not finite.</exception>
        /// <remarks>
        /// Average complexity: O(log n + k)
        /// Worst-case complexity: O(n), where k is the number of returned entities.
        /// </remarks>
        public List<OctreeEntity> QuerySphere(float[] center, float radius)
        {
            OctreeEntity.ValidatePosition(center, nameof(center));

            if (!OctreeEntity.IsFinite(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be a non-negative finite number.");
            }

            var results = new List<OctreeEntity>();
            if (_entitiesById.Count == 0)
            {
                return results;
            }

            QuerySphereRecursive(root, center, radius, radius * radius, results);
            return results;
        }

        /// <summary>
        /// Clears all entities and resets the octree to a single empty root node.
        /// </summary>
        /// <remarks>
        /// Complexity: O(m), where m is current node count (for dictionary and node reset bookkeeping).
        /// </remarks>
        public void Clear()
        {
            var rootCenter = OctreeEntity.Clone3(root.center);
            var rootHalfSize = root.halfSize;

            _entitiesById.Clear();
            _entityNodeById.Clear();
            _parentByNode.Clear();

            root = new OctreeNode(rootCenter, rootHalfSize, 0);
        }

        /// <summary>
        /// Gets the total number of nodes currently in the octree.
        /// </summary>
        /// <returns>Node count including the root node.</returns>
        /// <remarks>
        /// Complexity: O(m), where m is current node count.
        /// </remarks>
        public int GetNodeCount()
        {
            return CountNodes(root);
        }

        // Complexity: Average O(log n), worst-case O(d) where d = maxDepth.
        private void InsertIntoNode(OctreeNode node, OctreeEntity entity)
        {
            if (node.children == null)
            {
                if (node.depth >= maxDepth || node.entities.Count < maxEntitiesPerNode)
                {
                    node.entities.Add(entity);
                    _entityNodeById[entity.id] = node;
                    return;
                }

                Subdivide(node);
            }

            var octantIndex = GetOctantIndex(entity.position, node.center);
            InsertIntoNode(node.children[octantIndex], entity);
        }

        // Complexity: O(e), where e is number of entities currently stored in the node being subdivided.
        private void Subdivide(OctreeNode node)
        {
            if (node.children != null)
            {
                return;
            }

            var childHalf = node.halfSize * 0.5f;
            var children = new OctreeNode[8];

            for (var i = 0; i < 8; i++)
            {
                var xOffset = (i & 1) == 0 ? -childHalf : childHalf;
                var yOffset = (i & 2) == 0 ? -childHalf : childHalf;
                var zOffset = (i & 4) == 0 ? -childHalf : childHalf;

                var childCenter = new[]
                {
                    node.center[0] + xOffset,
                    node.center[1] + yOffset,
                    node.center[2] + zOffset
                };

                var child = new OctreeNode(childCenter, childHalf, node.depth + 1);
                children[i] = child;
                _parentByNode[child] = node;
            }

            node.children = children;

            if (node.entities.Count == 0)
            {
                return;
            }

            var toRedistribute = node.entities;
            node.entities = new List<OctreeEntity>();

            for (var i = 0; i < toRedistribute.Count; i++)
            {
                var entity = toRedistribute[i];
                var octant = GetOctantIndex(entity.position, node.center);
                InsertIntoNode(node.children[octant], entity);
            }
        }

        // Complexity: O(c + e), where c is child count (always 8) and e is total entities in children.
        private bool TryMerge(OctreeNode node)
        {
            if (node.children == null)
            {
                return false;
            }

            var totalChildEntities = 0;
            for (var i = 0; i < node.children.Length; i++)
            {
                var child = node.children[i];
                if (child.children != null)
                {
                    return false;
                }

                totalChildEntities += child.entities.Count;
                if (totalChildEntities > maxEntitiesPerNode)
                {
                    return false;
                }
            }

            for (var i = 0; i < node.children.Length; i++)
            {
                var child = node.children[i];
                for (var e = 0; e < child.entities.Count; e++)
                {
                    var entity = child.entities[e];
                    node.entities.Add(entity);
                    _entityNodeById[entity.id] = node;
                }

                _parentByNode.Remove(child);
            }

            node.children = null;
            return true;
        }

        // Complexity: O(1)
        private bool SphereIntersectsAABB(float[] sphereCenter, float radius, float[] boxCenter, float boxHalfSize)
        {
            var minX = boxCenter[0] - boxHalfSize;
            var maxX = boxCenter[0] + boxHalfSize;
            var minY = boxCenter[1] - boxHalfSize;
            var maxY = boxCenter[1] + boxHalfSize;
            var minZ = boxCenter[2] - boxHalfSize;
            var maxZ = boxCenter[2] + boxHalfSize;

            var clampedX = Clamp(sphereCenter[0], minX, maxX);
            var clampedY = Clamp(sphereCenter[1], minY, maxY);
            var clampedZ = Clamp(sphereCenter[2], minZ, maxZ);

            var dx = sphereCenter[0] - clampedX;
            var dy = sphereCenter[1] - clampedY;
            var dz = sphereCenter[2] - clampedZ;

            var distanceSq = (dx * dx) + (dy * dy) + (dz * dz);
            return distanceSq <= radius * radius;
        }

        // Complexity: O(1)
        private int GetOctantIndex(float[] position, float[] center)
        {
            var index = 0;

            if (position[0] >= center[0])
            {
                index |= 1;
            }

            if (position[1] >= center[1])
            {
                index |= 2;
            }

            if (position[2] >= center[2])
            {
                index |= 4;
            }

            return index;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void QuerySphereRecursive(OctreeNode node, float[] center, float radius, float radiusSq, List<OctreeEntity> output)
        {
            if (!SphereIntersectsAABB(center, radius, node.center, node.halfSize))
            {
                return;
            }

            for (var i = 0; i < node.entities.Count; i++)
            {
                var entity = node.entities[i];
                var delta = new Vector3(
                    entity.position[0] - center[0],
                    entity.position[1] - center[1],
                    entity.position[2] - center[2]);

                if (delta.LengthSquared() <= radiusSq)
                {
                    output.Add(entity);
                }
            }

            if (node.children == null)
            {
                return;
            }

            for (var i = 0; i < node.children.Length; i++)
            {
                QuerySphereRecursive(node.children[i], center, radius, radiusSq, output);
            }
        }

        private int CountNodes(OctreeNode node)
        {
            var count = 1;
            if (node.children == null)
            {
                return count;
            }

            for (var i = 0; i < node.children.Length; i++)
            {
                count += CountNodes(node.children[i]);
            }

            return count;
        }

        private void TryMergeUpwards(OctreeNode start)
        {
            var current = start;
            while (current != null)
            {
                TryMerge(current);
                _parentByNode.TryGetValue(current, out current);
            }
        }
    }
}
