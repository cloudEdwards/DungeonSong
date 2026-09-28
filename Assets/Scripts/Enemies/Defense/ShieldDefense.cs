using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Directional block. Only hits arriving inside a frontal arc are reduced, so the
    /// counterplay is to get behind the enemy. Raised and lowered by states and attacks
    /// (see <see cref="DefendState"/>), never by itself.
    /// </summary>
    public class ShieldDefense : DefenseBehaviour
    {
        [Header("Coverage")]
        [SerializeField, Range(0f, 360f), Tooltip("Width of the protected arc, centred on the facing direction.")]
        private float arcDegrees = 140f;

        [SerializeField, Tooltip("Also block hits from above, for enemies that shield overhead.")]
        private bool coversAbove;

        [Header("Mitigation")]
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of damage that gets through a successful block.")]
        private float blockedMultiplier;

        [SerializeField, Min(0f), Tooltip("Damage returned to the attacker on a successful block. 0 for none.")]
        private float riposteDamage;

        [Header("Presentation")]
        [SerializeField, Tooltip("Animation key played when the shield goes up.")]
        private string raiseAnimationKey = "shield_up";

        [SerializeField, Tooltip("Animation key played when the shield comes down.")]
        private string lowerAnimationKey = "shield_down";

        public override int ModifierOrder => 20;

        /// <summary>True when the last evaluated hit was inside the protected arc.</summary>
        public bool LastHitWasBlocked { get; private set; }

        public override void ModifyIncoming(in DamageInfo info, ref DefenseEvaluation evaluation)
        {
            LastHitWasBlocked = false;

            if (info.Has(DamageFlags.Unblockable))
            {
                return;
            }

            if (!IsHitWithinArc(info.Origin))
            {
                return;
            }

            LastHitWasBlocked = true;
            evaluation.Blocked = true;
            evaluation.Multiplier *= blockedMultiplier;
            evaluation.SuppressStagger = true;
            evaluation.SuppressKnockback = true;
            evaluation.ReflectDamage += riposteDamage;
        }

        private bool IsHitWithinArc(Vector2 origin)
        {
            Vector2 toAttacker = origin - (Vector2)transform.position;

            if (coversAbove && toAttacker.y > Mathf.Abs(toAttacker.x))
            {
                return true;
            }

            if (toAttacker.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            Vector2 facing = new Vector2(Owner != null ? Owner.FacingDirection : 1, 0f);
            float angle = Vector2.Angle(facing, toAttacker.normalized);
            return angle <= arcDegrees * 0.5f;
        }

        protected override void OnActiveChanged(bool isActive)
        {
            string key = isActive ? raiseAnimationKey : lowerAnimationKey;
            if (Owner != null && !string.IsNullOrEmpty(key))
            {
                Owner.Animation.PlayAction(key);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!IsActive)
            {
                return;
            }

            int facing = Owner != null ? Owner.FacingDirection : 1;
            Vector3 centre = transform.position;
            float half = arcDegrees * 0.5f;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);

            for (float a = -half; a <= half; a += 10f)
            {
                Vector3 dir = Quaternion.Euler(0f, 0f, a) * new Vector3(facing, 0f, 0f);
                Gizmos.DrawLine(centre, centre + dir * 1.2f);
            }
        }
    }
}
