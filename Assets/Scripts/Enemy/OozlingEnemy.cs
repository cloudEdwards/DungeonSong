using UnityEngine;

public class OozlingEnemy : EnemyScript
{
    protected new float m_contactDamage = 20f;
    protected override float GetContactDamage() => m_contactDamage;

}
