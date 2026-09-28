using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Marks a GameObject as something AI may aim at, and keeps it in
    /// <see cref="TargetRegistry"/> for exactly as long as it is enabled.
    /// Add this to the player prefab; it needs no other wiring.
    /// </summary>
    public class Targetable : MonoBehaviour, ITargetable
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Team this actor belongs to. Enemies hunt targets whose team is in their hostile mask.")]
        private DamageTeam team = DamageTeam.Player;

        [SerializeField, Tooltip("Offset from the transform to the point enemies should aim at (usually chest height).")]
        private Vector2 aimOffset = new Vector2(0f, 0.75f);

        [Header("Validity")]
        [SerializeField, Tooltip("Optional health component. When it reports dead, this target is skipped. Leave empty to search this object and its parents.")]
        private MonoBehaviour damageableSource;

        private IDamageable damageable;
        private bool resolved;

        public Transform Transform => transform;

        public Vector2 AimPosition => (Vector2)transform.position + aimOffset;

        public DamageTeam Team => team;

        public bool IsValidTarget
        {
            get
            {
                if (!resolved)
                {
                    Resolve();
                }

                return damageable == null || damageable.IsAlive;
            }
        }

        private void Resolve()
        {
            resolved = true;
            damageable = damageableSource as IDamageable;

            if (damageable == null)
            {
                // GetComponentInParent resolves interfaces, so this finds any health
                // implementation on this object or an ancestor.
                damageable = GetComponentInParent<IDamageable>();
            }
        }

        private void OnEnable()
        {
            Resolve();
            TargetRegistry.Register(this);
        }

        private void OnDisable() => TargetRegistry.Unregister(this);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(AimPosition, 0.12f);
        }
    }
}
