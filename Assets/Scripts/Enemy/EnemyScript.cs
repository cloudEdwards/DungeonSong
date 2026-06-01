using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    private Rigidbody2D m_body2d;
    [SerializeField]
    protected float m_speed = 1f;
    protected float m_contactDamage;
    protected float m_direction = 1f;

    void Start()
    {
        m_body2d = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        m_body2d.linearVelocity = new Vector2(m_direction * m_speed, m_body2d.linearVelocity.y);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(TagEnum.Player.ToString()))
        {
            collision.gameObject.GetComponent<PlayerHealth>().Damage(GetContactDamage());
        }
    }

    protected virtual float GetContactDamage()
    {
        return m_contactDamage;
    }
}
