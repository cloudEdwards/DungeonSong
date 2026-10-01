using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DungeonSong.Combat;

namespace DungeonSong.PlayMode.Tests
{
    /// <summary>Counts the contact hits it takes.</summary>
    internal class CountingDamageable : MonoBehaviour, IDamageable
    {
        public int Hits;

        public DamageTeam Team => DamageTeam.Player;

        public bool IsAlive => true;

        public Transform Transform => transform;

        public DamageResult TakeDamage(in DamageInfo info)
        {
            Hits++;
            return new DamageResult { Applied = true, AmountApplied = info.Amount };
        }
    }

    /// <summary>
    /// Contact damage hurts what touches the enemy's body: the player's body or hurtbox,
    /// never the player's sword. Found by pogoing: the down-slash overlapping an enemy made
    /// that enemy's contact damage hit the player.
    /// </summary>
    public class ContactDamagerTests
    {
        private GameObject enemy;
        private GameObject player;

        [SetUp]
        public void SetUp()
        {
            enemy = new GameObject("ContactEnemy");
            enemy.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            enemy.AddComponent<BoxCollider2D>().size = Vector2.one;
            enemy.AddComponent<ContactDamager>();
            enemy.transform.position = new Vector3(100f, 100f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(enemy);
            Object.Destroy(player);
        }

        /// <summary>A player whose body sits <paramref name="bodyOffset"/> from the enemy, with a child collider over the enemy.</summary>
        private CountingDamageable CreatePlayer(Vector2 bodyOffset, System.Action<GameObject> configureChild)
        {
            player = new GameObject("ContactPlayer");
            player.transform.position = enemy.transform.position + (Vector3)bodyOffset;
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            player.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            var damageable = player.AddComponent<CountingDamageable>();

            if (configureChild != null)
            {
                var child = new GameObject("Child");
                child.transform.SetParent(player.transform, false);
                child.transform.position = enemy.transform.position;
                configureChild(child);
            }

            return damageable;
        }

        private static IEnumerator Physics(int steps = 5)
        {
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator SwordOverlappingTheEnemy_DoesNotHurtThePlayer()
        {
            // The player's body is well clear; only an attack hitbox reaches the enemy.
            CountingDamageable target = CreatePlayer(new Vector2(0f, 3f), child =>
            {
                child.AddComponent<BoxCollider2D>().isTrigger = true;
                child.AddComponent<Hitbox>().Activate(DamageInfo.Create(1f, DamageTeam.Player, Vector2.zero));
            });

            yield return Physics();

            Assert.AreEqual(0, target.Hits, "An attack hitbox touching the enemy is not the player touching it.");
        }

        [UnityTest]
        public IEnumerator SensorOverlappingTheEnemy_DoesNotHurtThePlayer()
        {
            CountingDamageable target = CreatePlayer(new Vector2(0f, 3f), child =>
            {
                var sensor = child.AddComponent<CircleCollider2D>();
                sensor.isTrigger = true;
                sensor.radius = 0.2f;
            });

            yield return Physics();

            Assert.AreEqual(0, target.Hits, "Ground and wall sensors are not the player's body.");
        }

        [UnityTest]
        public IEnumerator HurtboxOverlappingTheEnemy_HurtsThePlayer()
        {
            CountingDamageable target = CreatePlayer(new Vector2(0f, 3f), child =>
            {
                child.AddComponent<BoxCollider2D>().isTrigger = true;
                child.AddComponent<Hurtbox>();
            });

            yield return Physics();

            Assert.AreEqual(1, target.Hits, "A hurtbox is exactly what contact damage should hit.");
        }

        [UnityTest]
        public IEnumerator BodyTouchingTheEnemy_HurtsThePlayer()
        {
            // No hurtbox at all: the solid body still counts, as for actors without one.
            CountingDamageable target = CreatePlayer(new Vector2(0f, 0.6f), null);

            yield return Physics();

            Assert.AreEqual(1, target.Hits);
        }
    }
}
