using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// A damageable region. Put one on each collider that should take hits, so an actor
    /// can have several zones (body, weak point, armoured back) that all feed the same
    /// health component with different multipliers.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hurtbox : MonoBehaviour
    {
        [Header("Owner")]
        [SerializeField, Tooltip("Component implementing IDamageable. Leave empty to search this object and its parents on Awake.")]
        private MonoBehaviour ownerSource;

        [Header("Zone")]
        [SerializeField, Range(0f, 10f), Tooltip("Damage multiplier for hits landing on this zone. 2 = weak point, 0.5 = armoured plate.")]
        private float damageMultiplier = 1f;

        [SerializeField, Tooltip("Purely informational: lets VFX and audio react differently to critical zones.")]
        private bool isWeakPoint;

        private IDamageable owner;
        private bool resolved;

        /// <summary>Health component this zone forwards damage to. Null if unwired.</summary>
        public IDamageable Owner
        {
            get
            {
                if (!resolved)
                {
                    Resolve();
                }

                return owner;
            }
        }

        public bool IsWeakPoint => isWeakPoint;

        public float DamageMultiplier => damageMultiplier;

        /// <summary>Team of the owning actor, or <see cref="DamageTeam.None"/> when unwired.</summary>
        public DamageTeam Team => Owner?.Team ?? DamageTeam.None;

        private void Awake() => Resolve();

        private void Resolve()
        {
            resolved = true;
            owner = ownerSource as IDamageable ?? GetComponentInParent<IDamageable>();

            if (owner == null)
            {
                Debug.LogWarning($"Hurtbox on '{name}' found no IDamageable owner; hits on it will be discarded.", this);
            }
        }

        /// <summary>
        /// Applies this zone's multiplier and forwards the hit to the owner. Returns
        /// <see cref="DamageResult.Ignored"/> when there is no owner to damage.
        /// </summary>
        public DamageResult Receive(DamageInfo info)
        {
            IDamageable target = Owner;
            if (target == null)
            {
                return DamageResult.Ignored;
            }

            info.Amount *= damageMultiplier;
            info.PoiseDamage *= damageMultiplier;
            return target.TakeDamage(in info);
        }
    }
}
