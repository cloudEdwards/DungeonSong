using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using DungeonSong.Combat;

public class PlayerHealth : MonoBehaviour, IDamageable
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
    }

    // --- IDamageable ---
    // Lets the enemy framework damage the player through the same pipeline it uses for
    // everything else, without the enemy code knowing this class exists. The existing
    // Damage(float) entry point is untouched, so the legacy EnemyScript still works.

    public DamageTeam Team => DamageTeam.Player;

    public bool IsAlive => !isDead && playerData.Health > 0f;

    public Transform Transform => transform;

    public DamageResult TakeDamage(in DamageInfo info)
    {
        if (!IsAlive)
        {
            return DamageResult.Ignored;
        }

        float before = playerData.Health;
        Damage(info.Amount);
        float applied = before - playerData.Health;

        return new DamageResult
        {
            Applied = applied > 0f,
            AmountApplied = applied,
            Killed = playerData.Health <= 0f,
            Reaction = playerData.Health <= 0f ? HitReaction.Death : HitReaction.Flinch,
        };
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
            isDead = true;

            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            GetComponent<BoxCollider2D>().enabled = false;
            GetComponent<PlayerController>().enabled = false;
        }
    }
}
