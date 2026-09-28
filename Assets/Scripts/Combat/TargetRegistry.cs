using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// The set of things AI can currently aim at. Targets push themselves in and out,
    /// so perception never calls <c>GameObject.Find*</c> or runs a physics query just to
    /// locate the player: with one player this turns target acquisition into a single
    /// distance check per enemy per perception tick.
    /// </summary>
    public static class TargetRegistry
    {
        private static readonly List<ITargetable> Targets = new List<ITargetable>(8);

        /// <summary>Live view of every registered target. Do not mutate.</summary>
        public static IReadOnlyList<ITargetable> All => Targets;

        public static void Register(ITargetable target)
        {
            if (target == null || Targets.Contains(target))
            {
                return;
            }

            Targets.Add(target);
        }

        public static void Unregister(ITargetable target)
        {
            if (target == null)
            {
                return;
            }

            Targets.Remove(target);
        }

        /// <summary>
        /// Nearest valid target on one of <paramref name="teamMask"/>'s teams within
        /// <paramref name="maxDistance"/>. Returns null when nothing qualifies.
        /// </summary>
        public static ITargetable FindNearest(Vector2 from, DamageTeam teamMask, float maxDistance)
        {
            float bestSqr = maxDistance * maxDistance;
            ITargetable best = null;

            for (int i = Targets.Count - 1; i >= 0; i--)
            {
                ITargetable candidate = Targets[i];

                // Destroyed targets that skipped Unregister (scene teardown) are pruned here.
                if (candidate == null || candidate.Transform == null)
                {
                    Targets.RemoveAt(i);
                    continue;
                }

                if (!candidate.IsValidTarget || (candidate.Team & teamMask) == 0)
                {
                    continue;
                }

                float sqr = ((Vector2)candidate.Transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Drops every entry. Call on hard scene resets to avoid stale targets.</summary>
        public static void Clear() => Targets.Clear();
    }
}
