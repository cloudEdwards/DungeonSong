using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Combat.Pooling;

namespace DungeonSong.Enemies.Tests
{
    public class TargetingTests
    {
        [SetUp]
        public void SetUp() => TargetRegistry.Clear();

        [TearDown]
        public void TearDown() => TargetRegistry.Clear();

        [Test]
        public void FindNearest_ReturnsTheClosestTarget()
        {
            FakeTarget near = FakeTarget.Create(new Vector2(2f, 0f));
            FakeTarget far = FakeTarget.Create(new Vector2(8f, 0f));

            try
            {
                ITargetable found = TargetRegistry.FindNearest(Vector2.zero, DamageTeam.Player, 20f);
                Assert.AreSame(near, found);
            }
            finally
            {
                near.Dispose();
                far.Dispose();
            }
        }

        [Test]
        public void FindNearest_RespectsMaxDistance()
        {
            FakeTarget target = FakeTarget.Create(new Vector2(30f, 0f));

            try
            {
                Assert.IsNull(TargetRegistry.FindNearest(Vector2.zero, DamageTeam.Player, 10f));
            }
            finally
            {
                target.Dispose();
            }
        }

        [Test]
        public void FindNearest_RespectsTeamMask()
        {
            FakeTarget target = FakeTarget.Create(new Vector2(1f, 0f));
            target.TeamValue = DamageTeam.Enemy;

            try
            {
                Assert.IsNull(TargetRegistry.FindNearest(Vector2.zero, DamageTeam.Player, 20f),
                    "An enemy-team target must be invisible to something hunting the player team.");
                Assert.AreSame(target, TargetRegistry.FindNearest(Vector2.zero, DamageTeam.Enemy, 20f));
            }
            finally
            {
                target.Dispose();
            }
        }

        [Test]
        public void FindNearest_SkipsInvalidTargets()
        {
            FakeTarget dead = FakeTarget.Create(new Vector2(1f, 0f));
            dead.Valid = false;

            try
            {
                Assert.IsNull(TargetRegistry.FindNearest(Vector2.zero, DamageTeam.Player, 20f));
            }
            finally
            {
                dead.Dispose();
            }
        }

        [Test]
        public void Unregister_RemovesTheTarget()
        {
            FakeTarget target = FakeTarget.Create(new Vector2(1f, 0f));
            TargetRegistry.Unregister(target);

            try
            {
                Assert.IsNull(TargetRegistry.FindNearest(Vector2.zero, DamageTeam.Player, 20f));
            }
            finally
            {
                Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void Register_IsIdempotent()
        {
            FakeTarget target = FakeTarget.Create(new Vector2(1f, 0f));
            TargetRegistry.Register(target);
            TargetRegistry.Register(target);

            try
            {
                Assert.AreEqual(1, TargetRegistry.All.Count);
            }
            finally
            {
                target.Dispose();
            }
        }
    }

    public class EnemyLifecycleTests
    {
        private Enemy enemy;

        [TearDown]
        public void TearDown() => EnemyTestFixture.Destroy(enemy);

        [Test]
        public void Initialize_ResolvesModuleRoles()
        {
            enemy = EnemyTestFixture.CreateEnemy(
                EnemyTestFixture.CreateStats(),
                typeof(EnemyStateMachine),
                typeof(AttackController),
                typeof(DefenseController),
                typeof(GroundMovement),
                typeof(EnemyPerception));

            Assert.IsNotNull(enemy.Health);
            Assert.IsNotNull(enemy.Brain);
            Assert.IsNotNull(enemy.Attacks);
            Assert.IsNotNull(enemy.Defense);
            Assert.IsNotNull(enemy.Movement);
            Assert.IsNotNull(enemy.Perception);
            Assert.IsNotNull(enemy.Animation, "Animation must fall back to a null adapter, never be left null.");
        }

        [Test]
        public void Initialize_IsIdempotent()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());

            Assert.DoesNotThrow(() => enemy.Initialize());
            Assert.DoesNotThrow(() => enemy.Initialize());
        }

        [Test]
        public void EnemyWithNoModules_StillInitializesAndTicks()
        {
            // The simplest possible enemy: health only. It must not require a brain,
            // movement or perception to exist.
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());

            Assert.AreEqual(EnemyLifecycleState.Active, enemy.Lifecycle);
            Assert.IsNull(enemy.Brain);
            Assert.IsNull(enemy.Movement);
            Assert.IsTrue(enemy.IsAlive);
        }

        [Test]
        public void Spawn_ActivatesAndRaisesSpawned()
        {
            var go = new GameObject("TestEnemy");
            go.AddComponent<Rigidbody2D>();
            Enemy e = go.AddComponent<Enemy>();
            EnemyHealth health = go.AddComponent<EnemyHealth>();
            e.Initialize();
            health.Configure(EnemyTestFixture.CreateStats());
            enemy = e;

            bool spawned = false;
            e.Spawned += _ => spawned = true;

            e.Spawn();

            Assert.IsTrue(spawned);
            Assert.AreEqual(EnemyLifecycleState.Active, e.Lifecycle);
        }

        [Test]
        public void Deactivate_GoesDormantWithoutLeavingTheWorld()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());

            enemy.Deactivate();

            Assert.AreEqual(EnemyLifecycleState.Dormant, enemy.Lifecycle);
            Assert.IsTrue(enemy.IsAlive);
        }

        [Test]
        public void Activate_ResumesFromDormant()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());
            enemy.Deactivate();

            enemy.Activate();

            Assert.AreEqual(EnemyLifecycleState.Active, enemy.Lifecycle);
        }

        [Test]
        public void Kill_MovesToDeadAndRaisesDied()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());
            bool died = false;
            enemy.Died += _ => died = true;

            enemy.Kill();

            Assert.IsTrue(died);
            Assert.AreEqual(EnemyLifecycleState.Dead, enemy.Lifecycle);
            Assert.IsFalse(enemy.IsAlive);
        }

        [Test]
        public void DamageEvents_AreReRaisedOnTheEnemy()
        {
            // Other systems subscribe to the Enemy, not to its modules.
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());
            DamageResult captured = default;
            bool raised = false;
            enemy.Damaged += (_, _, result) => { captured = result; raised = true; };

            enemy.Health.TakeDamage(EnemyTestFixture.Hit(12f));

            Assert.IsTrue(raised);
            Assert.AreEqual(12f, captured.AmountApplied, 0.001f);
        }

        [Test]
        public void StaggerEvent_IsReRaisedOnTheEnemy()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxPoise: 5f));
            bool staggered = false;
            enemy.Staggered += (_, _) => staggered = true;

            enemy.Health.TakeDamage(EnemyTestFixture.Hit(1f, poiseDamage: 10f));

            Assert.IsTrue(staggered);
        }

        [Test]
        public void SetFacing_UpdatesFacingDirection()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());

            enemy.SetFacing(-1);
            Assert.AreEqual(-1, enemy.FacingDirection);

            enemy.SetFacing(1);
            Assert.AreEqual(1, enemy.FacingDirection);
        }

        [Test]
        public void FaceTowards_TurnsTowardAPosition()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());
            enemy.SetFacing(1);

            enemy.FaceTowards(new Vector2(-10f, 0f));

            Assert.AreEqual(-1, enemy.FacingDirection);
        }
    }

    public class PoolingTests
    {
        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = new GameObject("PooledPrefab");
            prefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            PrefabPool.Clear();
            if (prefab != null)
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Release_ThenGet_ReusesTheSameInstance()
        {
            GameObject first = PrefabPool.Get(prefab, Vector3.zero, Quaternion.identity);
            PrefabPool.Release(first);

            GameObject second = PrefabPool.Get(prefab, Vector3.zero, Quaternion.identity);

            Assert.AreSame(first, second, "A released instance must come back out of the pool, not be re-instantiated.");

            PrefabPool.Release(second);
        }

        [Test]
        public void Prewarm_CreatesInstancesUpFront()
        {
            PrefabPool.Prewarm(prefab, 3);

            Assert.AreEqual(3, PrefabPool.CountInactive(prefab));
        }

        [Test]
        public void Get_ActivatesTheInstance()
        {
            GameObject instance = PrefabPool.Get(prefab, Vector3.zero, Quaternion.identity);

            Assert.IsTrue(instance.activeSelf);

            PrefabPool.Release(instance);
            Assert.IsFalse(instance.activeSelf, "A released instance must be deactivated.");
        }

        [Test]
        public void Get_PlacesTheInstanceAtTheRequestedPosition()
        {
            var position = new Vector3(3f, 4f, 0f);

            GameObject instance = PrefabPool.Get(prefab, position, Quaternion.identity);

            Assert.AreEqual(position, instance.transform.position);
            PrefabPool.Release(instance);
        }
    }
}
