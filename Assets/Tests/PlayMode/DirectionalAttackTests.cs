using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using DungeonSong.Combat;
using DungeonSong.Player;
using DungeonSong.World;

namespace DungeonSong.PlayMode.Tests
{
    /// <summary>The pogo: a down attack that connects in the air bounces the player up.</summary>
    public class DirectionalAttackTests
    {
        private ISaveService realSave;
        private bool realRunInBackground;
        private float heldGravity = 1f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Play mode stops advancing frames while the Editor is unfocused, which stalls
            // any test that waits on frames or physics.
            realRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            realSave = GameSave.Service;
            GameSave.Service = new InMemorySaveService();
            ResourcePool.ResetSession();
            yield return SceneManager.LoadSceneAsync("Scene1");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.FindAnyObjectByType<PlayerActor>()?.Health?.RestoreToFull();
            Application.runInBackground = realRunInBackground;
            GameSave.Service = realSave;
            ResourcePool.ResetSession();
            yield return null;
        }

        /// <summary>
        /// One enemy, alone and frozen: every other enemy is switched off, and this one has its
        /// AI, movement and attacks disabled and its body pinned. Its contact damage and hurtbox
        /// stay live, so the real damage path is tested without patrolling enemies (Scene1 has
        /// several) wandering into the player and making the result depend on timing.
        /// </summary>
        private static Hurtbox FindEnemy()
        {
            Hurtbox target = null;
            foreach (Hurtbox h in Object.FindObjectsByType<Hurtbox>())
            {
                if (h.Team != DamageTeam.Enemy)
                {
                    continue;
                }

                GameObject owner = ((Component)h.Owner).gameObject;
                if (target != null)
                {
                    owner.SetActive(false);
                    continue;
                }

                target = h;
                foreach (MonoBehaviour behaviour in owner.GetComponentsInChildren<MonoBehaviour>())
                {
                    if (!(behaviour is ContactDamager || behaviour is Hurtbox || behaviour is IDamageable))
                    {
                        behaviour.enabled = false;
                    }
                }

                var body = owner.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                    body.bodyType = RigidbodyType2D.Kinematic;
                }
            }

            return target;
        }

        /// <summary>
        /// Holds the player in the air a little above a point until it reads as airborne.
        /// Gravity is off while waiting: with the Editor in the background, physics runs
        /// several steps per frame and the player would otherwise fall onto the enemy before
        /// the test gets to press attack. <see cref="AttackDown"/> lets go.
        /// </summary>
        private IEnumerator HoverAbove(PlayerActor player, Vector2 point, float height, bool invulnerable = true)
        {
            if (invulnerable)
            {
                player.Health.GrantInvulnerability(10f);
            }

            heldGravity = player.Body.gravityScale;
            player.Body.gravityScale = 0f;
            player.transform.position = point + Vector2.up * height;

            for (int i = 0; i < 30 && player.Motion.IsGrounded; i++)
            {
                player.Body.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
            }

            player.transform.position = point + Vector2.up * height;
            player.Body.linearVelocity = Vector2.zero;
            Assert.IsFalse(player.Motion.IsGrounded, "The player must be airborne for a pogo.");
        }

        /// <summary>Releases the hover and presses down + attack in the same instant.</summary>
        private bool AttackDown(PlayerActor player)
        {
            player.Body.gravityScale = heldGravity;
            return player.Combat.TryAttack(AttackDirection.Down);
        }

        private static IEnumerator PeakUpwardSpeed(PlayerActor player, float seconds, System.Action<float> result)
        {
            float peak = float.MinValue;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                peak = Mathf.Max(peak, player.Body.linearVelocity.y);
                yield return new WaitForFixedUpdate();
            }

            result(peak);
        }

        [UnityTest]
        public IEnumerator DownAttack_HittingAnEnemyInTheAir_BouncesThePlayerUp()
        {
            PlayerActor player = Object.FindAnyObjectByType<PlayerActor>();
            Hurtbox enemy = FindEnemy();
            Assume.That(enemy, Is.Not.Null, "Scene1 needs an enemy for this test.");

            Bounds body = enemy.GetComponent<Collider2D>().bounds;
            var enemyHealth = enemy.Owner as IHealth;
            float before = enemyHealth?.Current ?? 0f;

            yield return HoverAbove(player, new Vector2(body.center.x, body.max.y), 0.4f);
            Assert.IsTrue(AttackDown(player));
            Assert.AreEqual("Down", player.Combat.CurrentAttack.HitboxKey);

            float peak = 0f;
            yield return PeakUpwardSpeed(player, 0.5f, v => peak = v);

            Assert.Greater(peak, 4f, "Connecting with the pogo throws the player upward.");
            if (enemyHealth != null)
            {
                Assert.Less(enemyHealth.Current, before, "The pogo damaged the enemy.");
            }
        }

        [UnityTest]
        public IEnumerator Pogo_GrantsInvulnerability_SoTheEnemyCannotHitBack()
        {
            PlayerActor player = Object.FindAnyObjectByType<PlayerActor>();
            Hurtbox enemy = FindEnemy();
            Assume.That(enemy, Is.Not.Null, "Scene1 needs an enemy for this test.");
            Bounds body = enemy.GetComponent<Collider2D>().bounds;

            // High enough that only the down hitbox reaches the enemy, not the player's body,
            // and no test invulnerability: the pogo itself must provide it.
            yield return HoverAbove(player, new Vector2(body.center.x, body.max.y), 0.8f, invulnerable: false);
            float health = player.Health.Current;

            // Every hit taken, so a failure says who hit the player and when.
            var hits = new System.Text.StringBuilder();
            bool bouncedYet = false;
            System.Action<PlayerActor, DamageInfo, DamageResult> record = (_, info, result) =>
                hits.Append($"\n  {(bouncedYet ? "after" : "before")} bounce: {info.Amount} {info.Type} from {(info.Attacker != null ? info.Attacker.name : "?")}"
                    + $" (target {((Component)enemy.Owner).name}) applied={result.Applied} phase={player.Combat.Phase}"
                    + $" playerBottom={player.GetComponent<Collider2D>().bounds.min.y:0.00} playerX={player.transform.position.x:0.00}"
                    + $" attackerTop={(info.Attacker != null ? info.Attacker.GetComponent<Collider2D>().bounds.max.y : 0f):0.00} attackerX={(info.Attacker != null ? info.Attacker.transform.position.x : 0f):0.00} active={(info.Attacker != null && info.Attacker.activeInHierarchy)}");
            player.Damaged += record;

            Assert.IsTrue(AttackDown(player));

            float deadline = Time.time + 0.5f;
            while (player.Body.linearVelocity.y <= 4f && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            bouncedYet = true;

            Assert.Greater(player.Body.linearVelocity.y, 4f, "The pogo connected.");
            Assert.IsTrue(player.Health.IsInvulnerable, "A pogo that lands grants i-frames.");
            Assert.AreEqual(health, player.Health.Current, 0.001f, "The pogo itself cost nothing." + hits);

            // Inside the window, touching the enemy again must not hurt.
            player.transform.position = new Vector2(body.center.x, body.center.y);
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            player.Damaged -= record;
            Assert.AreEqual(health, player.Health.Current, 0.001f, "Contact damage is ignored during the pogo's i-frames." + hits);
        }

        [UnityTest]
        public IEnumerator DownAttack_HittingNothing_DoesNotBounce()
        {
            PlayerActor player = Object.FindAnyObjectByType<PlayerActor>();
            Hurtbox enemy = FindEnemy();
            Assume.That(enemy, Is.Not.Null, "Scene1 needs an enemy for this test.");

            // Well clear of the enemy and the floor.
            yield return HoverAbove(player, enemy.transform.position, 6f);
            Assert.IsTrue(AttackDown(player));

            float peak = 0f;
            yield return PeakUpwardSpeed(player, 0.4f, v => peak = v);

            Assert.Less(peak, 1f, "A pogo that misses gives no free jump.");
        }
    }
}
