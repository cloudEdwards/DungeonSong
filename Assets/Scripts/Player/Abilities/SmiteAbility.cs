using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>
    /// Divine Smite: channels holy power into the blade. The next few swings deal bonus
    /// radiant damage.
    /// <para>
    /// The slot is spent on cast, hit or miss. Every swing uses one empowered strike
    /// whether or not it connects, and whatever is left fades after a few seconds, so a
    /// smite cannot be banked for the next room.
    /// </para>
    /// </summary>
    public class SmiteAbility : AbilityBehaviour, IAttackModifier
    {
        [Header("Smite")]
        [SerializeField, Min(1), Tooltip("Empowered swings per cast.")]
        private int empoweredStrikes = 3;

        [SerializeField, Min(0f), Tooltip("Seconds before unused strikes fade.")]
        private float duration = 5f;

        [SerializeField, Min(0f), Tooltip("Damage added to each empowered swing.")]
        private float bonusDamage = 10f;

        [SerializeField, Min(0f), Tooltip("Poise damage added to each empowered swing.")]
        private float bonusPoiseDamage = 10f;

        [SerializeField, Tooltip("Damage type added to empowered swings.")]
        private DamageType bonusDamageType = DamageType.Holy;

        [Header("Presentation")]
        [SerializeField, Tooltip("Renderer tinted while empowered. Leave empty to use the player's own sprite.")]
        private SpriteRenderer tintTarget;

        [SerializeField] private Color empoweredTint = new Color(1f, 0.85f, 0.35f, 1f);

        private int strikesLeft;
        private float timeLeft;
        private Color originalTint = Color.white;
        private bool tinted;

        public override int ActiveStacks => strikesLeft;

        public bool IsEmpowered => strikesLeft > 0;

        protected override void OnBind()
        {
            if (tintTarget == null)
            {
                tintTarget = GetComponent<SpriteRenderer>();
            }
        }

        public override bool CanActivate() => !IsEmpowered && base.CanActivate();

        protected override void OnResolve(in TargetInfo target)
        {
            if (Owner.Combat == null)
            {
                Debug.LogWarning($"SmiteAbility on '{name}' found no PlayerCombat; the smite has nothing to empower.", this);
                return;
            }

            strikesLeft = empoweredStrikes;
            timeLeft = duration;
            Owner.Combat.AddAttackModifier(this);
            SetTint(true);
        }

        public void ModifyAttack(PlayerAttackDefinition attack, ref DamageInfo info)
        {
            if (strikesLeft <= 0)
            {
                EndSmite();
                return;
            }

            info.Amount += bonusDamage;
            info.PoiseDamage += bonusPoiseDamage;
            info.Type |= bonusDamageType;

            strikesLeft--;
            if (strikesLeft <= 0)
            {
                EndSmite();
            }
        }

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);

            if (!IsEmpowered)
            {
                return;
            }

            timeLeft -= deltaTime;
            if (timeLeft <= 0f)
            {
                EndSmite();
            }
        }

        public override void OnPlayerDied()
        {
            base.OnPlayerDied();
            EndSmite();
        }

        public override void OnPlayerSpawned()
        {
            base.OnPlayerSpawned();
            EndSmite();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EndSmite();
        }

        private void EndSmite()
        {
            strikesLeft = 0;
            timeLeft = 0f;
            Owner?.Combat?.RemoveAttackModifier(this);
            SetTint(false);
        }

        private void SetTint(bool on)
        {
            if (tintTarget == null || on == tinted)
            {
                return;
            }

            if (on)
            {
                originalTint = tintTarget.color;
                tintTarget.color = empoweredTint;
            }
            else
            {
                tintTarget.color = originalTint;
            }

            tinted = on;
        }
    }
}
