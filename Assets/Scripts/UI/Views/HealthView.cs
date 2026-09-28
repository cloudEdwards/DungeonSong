using UnityEngine;
using UnityEngine.UI;
using DungeonSong.Combat;

namespace DungeonSong.UI
{
    /// <summary>
    /// Shows the player's health, as a number, a bar, or both.
    /// <para>
    /// Event-driven rather than polled: health changes rarely, so there is no reason to
    /// read it every frame.
    /// </para>
    /// </summary>
    public class HealthView : HudView
    {
        [Header("Widgets")]
        [SerializeField, Tooltip("Optional text readout. Leave empty to show only a bar.")]
        private Text valueText;

        [SerializeField, Tooltip("Optional fill image. Set its Image Type to Filled.")]
        private Image fillImage;

        [Header("Format")]
        [SerializeField, Tooltip("Format for the text readout. {0} is current, {1} is max.")]
        private string format = "{0:0}/{1:0}";

        [Header("Damage Flash")]
        [SerializeField, Tooltip("Tint the bar briefly when health drops.")]
        private bool flashOnDamage = true;

        [SerializeField] private Color damageFlashColor = new Color(1f, 0.4f, 0.4f, 1f);

        [SerializeField, Min(0f)] private float damageFlashDuration = 0.2f;

        private IHealth health;
        private Color baseFillColor = Color.white;
        private float flashTimer;

        protected override void OnBind()
        {
            health = Player.Health;
            if (health == null)
            {
                return;
            }

            if (fillImage != null)
            {
                baseFillColor = fillImage.color;
            }

            health.HealthChanged += OnHealthChanged;
            OnHealthChanged(health.Current, health.Max);
        }

        protected override void OnUnbind()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                health = null;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (valueText != null)
            {
                valueText.text = string.Format(format, current, max);
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            }

            if (flashOnDamage && fillImage != null)
            {
                flashTimer = damageFlashDuration;
            }
        }

        public override void Tick(float deltaTime)
        {
            if (flashTimer <= 0f || fillImage == null)
            {
                return;
            }

            flashTimer -= deltaTime;
            fillImage.color = flashTimer > 0f
                ? Color.Lerp(baseFillColor, damageFlashColor, flashTimer / Mathf.Max(0.0001f, damageFlashDuration))
                : baseFillColor;
        }
    }
}
