using UnityEngine;
using System.Collections.Generic;

namespace Tomciz.RoadGenerator
{
    /// <summary>
    /// Lightweight static registry that keeps track of all finished roads
    /// so that RoadGenerators can discover nearby endpoints for snapping / merging.
    /// </summary>
    public static class RoadNetwork
    {
        public struct PersistedRoad
        {
            public RoadGenerator Owner;
            public List<Vector3> Path;
            public float Width;
            public int Id;
        }

        public struct SnapResult
        {
            public PersistedRoad Road;
            public bool IsStart;
            public Vector3 EndpointPosition;
            public Vector3 Tangent;
        }

        private static readonly List<PersistedRoad> _roads = new List<PersistedRoad>();
        private static int _nextId;

        public static IReadOnlyList<PersistedRoad> Roads => _roads;

        /// <summary>
        /// Register a finished road. Each call adds a new entry (supports multiple roads per owner).
        /// Returns the road ID.
        /// </summary>
        public static int Register(RoadGenerator owner, List<Vector3> path, float width)
        {
            int id = _nextId++;
            _roads.Add(new PersistedRoad
            {
                Owner = owner,
                Path = new List<Vector3>(path),
                Width = width,
                Id = id
            });
            return id;
        }

        /// <summary>
        /// Remove all roads owned by the given generator.
        /// </summary>
        public static void Unregister(RoadGenerator owner)
        {
            _roads.RemoveAll(r => r.Owner == owner);
        }

        /// <summary>
        /// Remove a specific road by ID.
        /// </summary>
        public static void UnregisterById(int id)
        {
            _roads.RemoveAll(r => r.Id == id);
        }

        /// <summary>
        /// Search for the nearest endpoint across all registered roads.
        /// Optionally skip a specific road by ID (<paramref name="skipRoadId"/>, pass -1 to skip nothing).
        /// Returns true if a road endpoint was found within <paramref name="threshold"/>.
        /// </summary>
        public static bool FindNearestEndpoint(Vector3 pos, float threshold,
            int skipRoadId, out SnapResult result)
        {
            result = default;
            float bestDist = float.MaxValue;
            bool found = false;

            foreach (var road in _roads)
            {
                if (road.Id == skipRoadId) continue;
                if (road.Path == null || road.Path.Count < 2) continue;

                // Check start
                Vector3 start = road.Path[0];
                float dStart = Vector3.Distance(pos, start);
                if (dStart < threshold && dStart < bestDist)
                {
                    bestDist = dStart;
                    result = new SnapResult
                    {
                        Road = road,
                        IsStart = true,
                        EndpointPosition = start,
                        Tangent = (road.Path[0] - road.Path[1]).normalized
                    };
                    found = true;
                }

                // Check end
                int last = road.Path.Count - 1;
                Vector3 end = road.Path[last];
                float dEnd = Vector3.Distance(pos, end);
                if (dEnd < threshold && dEnd < bestDist)
                {
                    bestDist = dEnd;
                    result = new SnapResult
                    {
                        Road = road,
                        IsStart = false,
                        EndpointPosition = end,
                        Tangent = (road.Path[last] - road.Path[last - 1]).normalized
                    };
                    found = true;
                }
            }

            return found;
        }
    }
}
