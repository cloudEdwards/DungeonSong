using UnityEngine;

public class OozlingEnemy : EnemyScript
{
    [SerializeField]
    protected float contactDamage = 20f;
    protected override float GetContactDamage() => contactDamage;

}
