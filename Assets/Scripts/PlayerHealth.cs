using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using DungeonSong.Combat;

public class PlayerHealth : MonoBehaviour, IDamageable, IHealth
{
    private Animator m_animator;
    
    [SerializeField]
    bool m_noBlood = false;
    [SerializeField]
    protected PlayerDataDto playerData;

    [SerializeField]
    protected float damageIFrames = 1f;
    protected float damageIFramesTimer = 0f;


    [SerializeField]
    protected TextController textController;

    private bool isDead = false;

    private IEnumerator healingCoroutine;

    void Start()
    {
        m_animator = GetComponent<Animator>();
        textController = GetComponentInChildren<TextController>();
    }

    public void Damage(float damage)
    {
        if (playerData.Health <= 0 || damageIFramesTimer > 0)
        {
            return;
        }

        playerData.Health -= damage;

        m_animator.SetTrigger("Hurt");
        damageIFramesTimer = damageIFrames;
    }

    public void HealHold(float healHp = 0f)
    {
        healingCoroutine = Healing(5f, healHp);
        StartCoroutine(healingCoroutine);
    }

    public void HealHoldStop()
    {
        StopCoroutine(healingCoroutine);
    }

    IEnumerator Healing(float time, float healHp = 0f)
    {
        float timer = 0;
        float timerInterval = 1f;

        while (timer <= time)
        {
            Heal(healHp);

            timer += Time.deltaTime;
            yield return new WaitForSeconds(timerInterval);
        }
    }

    public void Heal(float healHp = 0f)
    {
        Debug.Log("Heal");

        float healAmount = healHp > 0f ? healHp : playerData.HealRate;
        if (playerData.Health >= playerData.MaxHealth)
        {
            return;
        }

        playerData.Health += healAmount;
        playerData.Health = Mathf.Min(playerData.Health, playerData.MaxHealth);
        HealthChanged?.Invoke(Current, Max);
    }

    // --- IDamageable / IHealth ---
    // The player receives damage through exactly the same pipeline as every enemy, so an
    // attack does not care which side it hits. Runtime health still lives in PlayerDataDto
    // so it survives scene changes, which is the project's existing behaviour.

    public DamageTeam Team => DamageTeam.Player;

    public bool IsAlive => !isDead && playerData.Health > 0f;

    public Transform Transform => transform;

    public float Current => playerData != null ? playerData.Health : 0f;

    public float Max => playerData != null ? playerData.MaxHealth : 0f;

    public float Normalized => Max > 0f ? Current / Max : 0f;

    public bool IsInvulnerable => damageIFramesTimer > 0f;

    /// <summary>Raised for every hit that reached the player, including ignored ones.</summary>
    public event System.Action<DamageInfo, DamageResult> Damaged;

    /// <summary>Raised on any health change, as (current, max).</summary>
    public event System.Action<float, float> HealthChanged;

    /// <summary>Raised once when the player dies.</summary>
    public event System.Action Died;

    public DamageResult TakeDamage(in DamageInfo info)
    {
        if (!IsAlive)
        {
            return DamageResult.Ignored;
        }

        // Friendly fire is discarded here rather than in every hitbox.
        if ((info.SourceTeam & Team) != 0)
        {
            return DamageResult.Ignored;
        }

        if (IsInvulnerable && !info.Has(DamageFlags.IgnoreInvulnerability))
        {
            return DamageResult.Ignored;
        }

        float before = playerData.Health;
        ApplyDamage(info.Amount, info.Has(DamageFlags.NoInvulnerabilityWindow));
        float applied = before - playerData.Health;

        var result = new DamageResult
        {
            Applied = applied > 0f,
            AmountApplied = applied,
            Killed = playerData.Health <= 0f,
        };

        if (!info.Has(DamageFlags.NoKnockback) && info.KnockbackForce > 0f)
        {
            ApplyKnockback(in info);
            result.Reaction = HitReaction.Knockback;
        }
        else
        {
            result.Reaction = result.Killed ? HitReaction.Death : HitReaction.Flinch;
        }

        Damaged?.Invoke(info, result);
        HealthChanged?.Invoke(Current, Max);

        if (result.Killed)
        {
            RaiseDied();
        }

        return result;
    }

    private void ApplyDamage(float amount, bool skipInvulnerabilityWindow)
    {
        playerData.Health -= amount;

        m_animator.SetTrigger("Hurt");

        if (!skipInvulnerabilityWindow)
        {
            damageIFramesTimer = damageIFrames;
        }
    }

    private void ApplyKnockback(in DamageInfo info)
    {
        var receiver = GetComponent<IKnockbackReceiver>();
        if (receiver != null)
        {
            Vector2 direction = info.KnockbackDirection == Vector2.zero
                ? ((Vector2)transform.position - info.Origin).normalized
                : info.KnockbackDirection;

            receiver.ApplyKnockback(direction, info.KnockbackForce, 0.2f);
            return;
        }

        // No dedicated receiver: push the body directly so knockback still reads.
        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            Vector2 direction = info.KnockbackDirection == Vector2.zero
                ? ((Vector2)transform.position - info.Origin).normalized
                : info.KnockbackDirection;

            body.linearVelocity = direction * info.KnockbackForce;
        }
    }

    /// <summary>Opens an invulnerability window, for dodges and abilities.</summary>
    public void GrantInvulnerability(float seconds)
    {
        damageIFramesTimer = Mathf.Max(damageIFramesTimer, seconds);
    }

    /// <summary>Refills health and clears death state. Used on respawn and at campfires.</summary>
    public void RestoreToFull()
    {
        playerData.Health = playerData.MaxHealth;
        damageIFramesTimer = 0f;

        if (isDead)
        {
            isDead = false;
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            GetComponent<BoxCollider2D>().enabled = true;
            GetComponent<PlayerController>().enabled = true;
        }

        HealthChanged?.Invoke(Current, Max);
    }

    float IHealth.Heal(float amount)
    {
        float before = playerData.Health;
        Heal(amount);
        return playerData.Health - before;
    }

    private void RaiseDied()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        Died?.Invoke();
    }

    void Update()
    {
        textController.SetHealthText(playerData.Health.ToString());

        if (damageIFramesTimer > 0)
        {
            damageIFramesTimer -= Time.deltaTime;
        }

        if (! isDead && playerData.Health <= 0)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");

            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            GetComponent<BoxCollider2D>().enabled = false;
            GetComponent<PlayerController>().enabled = false;

            // Raises Died exactly once, which is what PlayerRespawner listens for.
            RaiseDied();
        }
    }
}
