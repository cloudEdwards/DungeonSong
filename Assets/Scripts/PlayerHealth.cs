using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    private Animator m_animator;
    
    [SerializeField]
    bool m_noBlood = false;
    [SerializeField]
    protected float health = 100f;

    [SerializeField]
    protected float damageIFrames = 1f;
    protected float damageIFramesTimer = 0f;

    private bool isDead = false;

    void Start()
    {
        m_animator = GetComponent<Animator>();
    }

    public void Damage(float damage)
    {
        if (health <= 0 || damageIFramesTimer > 0)
        {
            return;
        }

        health -= damage;

        m_animator.SetTrigger("Hurt");
        damageIFramesTimer = damageIFrames;
    }

    void Update()
    {
        if (damageIFramesTimer > 0)
        {
            damageIFramesTimer -= Time.deltaTime;
        }

        if (! isDead && health <= 0)
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
