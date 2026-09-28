using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>Names one hitbox on the player rig.</summary>
    [Serializable]
    public struct NamedHitbox
    {
        [Tooltip("Key attack definitions refer to, e.g. 'Forward', 'Up', 'Down', 'Wall'.")]
        public string Key;

        [Tooltip("The hitbox this key opens.")]
        public Hitbox Hitbox;

        [Tooltip("Mirror this hitbox to the player's facing. Off for up and down boxes, which are symmetrical.")]
        public bool MirrorWithFacing;
    }

    /// <summary>
    /// The player's set of attack hitboxes, addressed by name.
    /// <para>
    /// This is what keeps directional combat from becoming a pile of hard-coded transforms:
    /// a definition says "open the box called Up", and which object that is stays a prefab
    /// concern. Adding a diagonal attack later means adding one child and one row here.
    /// </para>
    /// </summary>
    public class HitboxDirectory : PlayerModule
    {
        [Header("Hitboxes")]
        [SerializeField, Tooltip("Every attack hitbox on this rig, keyed by name.")]
        private NamedHitbox[] hitboxes = Array.Empty<NamedHitbox>();

        private readonly Dictionary<string, NamedHitbox> lookup = new Dictionary<string, NamedHitbox>();

        protected override void OnBind()
        {
            lookup.Clear();

            for (int i = 0; i < hitboxes.Length; i++)
            {
                NamedHitbox entry = hitboxes[i];
                if (string.IsNullOrEmpty(entry.Key) || entry.Hitbox == null)
                {
                    continue;
                }

                lookup[entry.Key] = entry;
                entry.Hitbox.TargetTeams = Owner.HostileTeams;
            }
        }

        /// <summary>Finds a hitbox by key. Returns null when the key is unmapped.</summary>
        public Hitbox Resolve(string key)
        {
            if (string.IsNullOrEmpty(key) || !lookup.TryGetValue(key, out NamedHitbox entry))
            {
                return null;
            }

            if (entry.MirrorWithFacing)
            {
                MirrorToFacing(entry.Hitbox.transform);
            }

            return entry.Hitbox;
        }

        /// <summary>
        /// Flips a hitbox to the side the player faces. Sprite flipping does not move child
        /// transforms, so without this a left-facing player swings into empty air.
        /// </summary>
        private void MirrorToFacing(Transform hitboxTransform)
        {
            Vector3 local = hitboxTransform.localPosition;
            float wanted = Mathf.Abs(local.x) * Owner.FacingDirection;

            if (!Mathf.Approximately(local.x, wanted))
            {
                local.x = wanted;
                hitboxTransform.localPosition = local;
            }
        }

        /// <summary>Closes every hitbox. Used when an attack is cancelled or the player dies.</summary>
        public void DeactivateAll()
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i].Hitbox != null)
                {
                    hitboxes[i].Hitbox.Deactivate();
                }
            }
        }

        public override void OnPlayerDied() => DeactivateAll();
    }
}
