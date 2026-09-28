using UnityEngine;
using DungeonSong.Combat.Pooling;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Turns a hit into something the player can see: a brief white flash and a burst of
    /// particles.
    /// <para>
    /// It listens to <see cref="IHealth.Damaged"/>, which both the player and the enemies
    /// implement, so the same component gives feedback on either side. The flash is driven
    /// through a <see cref="MaterialPropertyBlock"/> rather than by instancing a material,
    /// so any number of actors can flash at once without adding draw calls, and no
    /// materials leak at runtime.
    /// </para>
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField, Tooltip("Health to watch. Leave empty to search this object and its parents.")]
        private MonoBehaviour healthSource;

        [Header("Flash")]
        [SerializeField, Tooltip("Renderers to flash. Leave empty to use every SpriteRenderer in children.")]
        private SpriteRenderer[] renderers;

        [SerializeField, Tooltip("Colour the sprite blends toward on a hit.")]
        private Color flashColor = Color.white;

        [SerializeField, Min(0f), Tooltip("Seconds the flash lasts.")]
        private float flashDuration = 0.25f;

        [SerializeField, Tooltip("How the flash fades out over its duration. Left to right is start to end.")]
        private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [SerializeField, Tooltip("Flash even when a hit was blocked or absorbed. Off means only real damage flashes.")]
        private bool flashOnBlockedHits;

        [Header("Particles")]
        [SerializeField, Tooltip("Particle prefab spawned at the hit point. Pooled automatically. Leave empty for no particles.")]
        private GameObject hitParticles;

        [SerializeField, Tooltip("Tint applied to the particle burst. Green for this project's ooze enemies.")]
        private Color particleColor = new Color(0.35f, 0.9f, 0.25f, 1f);

        [SerializeField, Min(0f), Tooltip("Seconds before the particle instance returns to the pool.")]
        private float particleLifetime = 1.2f;

        [SerializeField, Tooltip("Rotate the burst to face away from the attacker, so blood sprays the right way.")]
        private bool orientToHitDirection = true;

        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        private MaterialPropertyBlock block;
        private IHealth health;
        private float flashTimer;

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            health = healthSource as IHealth ?? GetComponentInParent<IHealth>();

            if (health == null)
            {
                Debug.LogWarning($"HitFeedback on '{name}' found no IHealth to watch; it will never fire.", this);
                return;
            }

            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }

            SetFlashAmount(0f);
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (!result.Applied && !(flashOnBlockedHits && result.Blocked))
            {
                return;
            }

            flashTimer = flashDuration;
            SpawnParticles(in info);
        }

        private void Update()
        {
            if (flashTimer <= 0f)
            {
                return;
            }

            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f)
            {
                flashTimer = 0f;
                SetFlashAmount(0f);
                return;
            }

            // Curve is sampled from 0 (just hit) to 1 (flash over).
            float t = flashDuration > 0f ? 1f - (flashTimer / flashDuration) : 1f;
            SetFlashAmount(Mathf.Clamp01(flashCurve.Evaluate(t)));
        }

        private void SetFlashAmount(float amount)
        {
            if (renderers == null || block == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(block);
                block.SetColor(FlashColorId, flashColor);
                block.SetFloat(FlashAmountId, amount);
                renderer.SetPropertyBlock(block);
            }
        }

        private void SpawnParticles(in DamageInfo info)
        {
            if (hitParticles == null)
            {
                return;
            }

            Vector2 point = info.HitPoint != Vector2.zero ? info.HitPoint : (Vector2)transform.position;

            Quaternion rotation = Quaternion.identity;
            if (orientToHitDirection)
            {
                Vector2 away = (Vector2)transform.position - info.Origin;
                if (away.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.FromToRotation(Vector3.right, away.normalized);
                }
            }

            GameObject instance = PrefabPool.Get(hitParticles, point, rotation);
            if (instance == null)
            {
                return;
            }

            var particles = instance.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = particleColor;
                particles.Clear();
                particles.Play();
            }

            // Pooled prefabs need returning; a one-shot burst has no natural owner to do it.
            var recycler = instance.GetComponent<PooledLifetime>();
            if (recycler == null)
            {
                recycler = instance.AddComponent<PooledLifetime>();
            }

            recycler.Begin(particleLifetime);
        }
    }

    /// <summary>
    /// Returns a pooled one-shot effect to the pool after a fixed time. Small on purpose:
    /// VFX prefabs have no natural owner to release them.
    /// </summary>
    public class PooledLifetime : MonoBehaviour, IPoolable
    {
        private float timer;
        private bool running;

        public void Begin(float seconds)
        {
            timer = seconds;
            running = seconds > 0f;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                running = false;
                PrefabPool.Release(gameObject);
            }
        }

        public void OnSpawnedFromPool() => running = false;

        public void OnReturnedToPool() => running = false;
    }
}
