using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// A damage-dealing region that is off by default and switched on for the active
    /// frames of an attack. It carries no attack logic of its own: whoever activates it
    /// supplies the <see cref="DamageInfo"/>, so one hitbox serves melee swings,
    /// projectiles and environmental hazards alike.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Name attacks use to find this hitbox on the prefab, e.g. 'Sword', 'Claw', 'Slam'.")]
        private string key = "Primary";

        [Header("Filtering")]
        [SerializeField, Tooltip("Teams this hitbox is allowed to damage.")]
        private DamageTeam targetTeams = DamageTeam.Player;

        [Header("Rules")]
        [SerializeField, Tooltip("Maximum hurtboxes damaged per activation. 0 = unlimited.")]
        private int maxTargetsPerActivation = 1;

        [SerializeField, Tooltip("When true a target can only be hit once per activation, so overlapping frames never multi-hit.")]
        private bool singleHitPerTarget = true;

        /// <summary>Raised for every hurtbox this box damages. Use for VFX, audio, hit-stop.</summary>
        public event System.Action<Hurtbox, DamageResult> Hit;

        // Instance ids of hurtboxes already hit during this activation. A HashSet reused
        // across activations means dedupe costs no allocations after the first few hits.
        private readonly HashSet<EntityId> hitThisActivation = new HashSet<EntityId>();
        private Collider2D[] colliders;
        private DamageInfo template;
        private int hitCount;

        public string Key => key;

        public bool IsActive { get; private set; }

        public DamageTeam TargetTeams
        {
            get => targetTeams;
            set => targetTeams = value;
        }

        private void Awake()
        {
            colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = true;
                colliders[i].enabled = false;
            }
        }

        /// <summary>
        /// Opens the hitbox for business. <paramref name="damageTemplate"/> is copied, so
        /// the caller may reuse its own struct freely.
        /// </summary>
        public void Activate(in DamageInfo damageTemplate)
        {
            template = damageTemplate;
            hitThisActivation.Clear();
            hitCount = 0;
            IsActive = true;
            SetCollidersEnabled(true);
            HitboxDebug.NotifyActivated(this);
        }

        /// <summary>Closes the hitbox. Safe to call when already inactive.</summary>
        public void Deactivate()
        {
            IsActive = false;
            SetCollidersEnabled(false);
        }

        private void SetCollidersEnabled(bool value)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = value;
            }
        }

        private void OnDisable() => Deactivate();

        // Enter covers targets that move in; Stay covers targets already overlapping when
        // the collider was switched on mid-attack. Dedupe keeps that from double-hitting.
        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);

        private void OnTriggerStay2D(Collider2D other) => TryHit(other);

        private void TryHit(Collider2D other)
        {
            if (!IsActive || other == null)
            {
                return;
            }

            if (maxTargetsPerActivation > 0 && hitCount >= maxTargetsPerActivation)
            {
                return;
            }

            if (!other.TryGetComponent(out Hurtbox hurtbox))
            {
                return;
            }

            if ((hurtbox.Team & targetTeams) == 0)
            {
                return;
            }

            EntityId id = hurtbox.GetEntityId();
            if (singleHitPerTarget && !hitThisActivation.Add(id))
            {
                return;
            }

            DamageInfo info = template;
            info.HitPoint = other.ClosestPoint(transform.position);
            if (info.KnockbackForce > 0f && info.KnockbackDirection == Vector2.zero)
            {
                info.AimKnockbackFrom(hurtbox.transform.position, info.KnockbackForce);
            }

            DamageResult result = hurtbox.Receive(info);
            if (result.Applied || result.Blocked)
            {
                hitCount++;
            }

            Hit?.Invoke(hurtbox, result);
        }

        private void OnDrawGizmos()
        {
            if (!IsActive)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;

            Collider2D shape = GetComponent<Collider2D>();
            if (shape == null)
            {
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.25f);
                return;
            }

            Vector3[] outline = HitboxDebug.GetOutline(shape);
            for (int i = 0; i < outline.Length; i++)
            {
                Gizmos.DrawLine(outline[i], outline[(i + 1) % outline.Length]);
            }
        }
    }
}
