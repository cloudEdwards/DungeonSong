using System;
using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Player;
using DungeonSong.World;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>An ability that records whether it resolved, so activation can be observed.</summary>
    internal class ProbeAbility : AbilityBehaviour
    {
        public int ResolveCount;

        protected override void OnResolve(in TargetInfo target) => ResolveCount++;
    }

    public class PlayerAbilityTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            CheckpointService.Clear();
        }

        private static ResourceDefinition CreateResource(float max = 100f)
        {
            var r = ScriptableObject.CreateInstance<ResourceDefinition>();
            r.Id = "mana";
            r.MaxAmount = max;
            r.StartingAmount = max;
            r.RegenPerSecond = 0f;
            return r;
        }

        private static AbilityDefinition CreateAbility(ResourceDefinition resource = null, float cost = 0f, float cooldown = 0f)
        {
            var a = ScriptableObject.CreateInstance<AbilityDefinition>();
            a.DisplayName = "Probe";
            a.Id = "probe";
            a.CastTime = 0.05f;
            a.Recovery = 0.05f;
            a.Cooldown = cooldown;
            a.LockMovement = false;
            a.Targeting = TargetingMode.Self;
            a.Cost = new ResourceCost { Resource = resource, Amount = cost };
            return a;
        }

        private (PlayerActor actor, ProbeAbility ability, ResourcePool pool) CreatePlayer(AbilityDefinition definition, ResourceDefinition resource = null)
        {
            go = new GameObject("TestPlayer");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<FakeMotionContext>();
            PlayerActor actor = go.AddComponent<PlayerActor>();
            ResourcePool pool = go.AddComponent<ResourcePool>();
            ProbeAbility ability = go.AddComponent<ProbeAbility>();

            if (resource != null)
            {
                var poolSo = new UnityEditor.SerializedObject(pool);
                UnityEditor.SerializedProperty list = poolSo.FindProperty("resources");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = resource;
                poolSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var abilitySo = new UnityEditor.SerializedObject(ability);
            abilitySo.FindProperty("definition").objectReferenceValue = definition;
            abilitySo.ApplyModifiedPropertiesWithoutUndo();

            actor.Initialize();
            return (actor, ability, pool);
        }

        [Test]
        public void Ability_ActivatesAndResolvesAfterCastTime()
        {
            var (_, ability, _) = CreatePlayer(CreateAbility());

            Assert.IsTrue(ability.TryActivate());
            Assert.IsTrue(ability.IsRunning);
            Assert.AreEqual(0, ability.ResolveCount, "Nothing resolves during the cast.");

            ability.Tick(0.06f);
            Assert.AreEqual(1, ability.ResolveCount);

            ability.Tick(0.06f);
            Assert.IsFalse(ability.IsRunning);
        }

        [Test]
        public void Ability_SpendsItsResource()
        {
            ResourceDefinition mana = CreateResource();
            var (_, ability, pool) = CreatePlayer(CreateAbility(mana, cost: 30f), mana);

            ability.TryActivate();

            Assert.AreEqual(70f, pool.GetAmount(mana), 0.001f);
        }

        [Test]
        public void Ability_RefusesWhenResourceIsTooLow()
        {
            ResourceDefinition mana = CreateResource(max: 10f);
            var (_, ability, pool) = CreatePlayer(CreateAbility(mana, cost: 30f), mana);

            Assert.IsFalse(ability.CanActivate());
            Assert.IsFalse(ability.TryActivate());
            Assert.AreEqual(10f, pool.GetAmount(mana), 0.001f, "A refused ability must not spend anything.");
        }

        [Test]
        public void Ability_RespectsCooldown()
        {
            var (_, ability, _) = CreatePlayer(CreateAbility(cooldown: 1f));

            ability.TryActivate();
            ability.Tick(0.06f);
            ability.Tick(0.06f);
            Assert.IsFalse(ability.IsRunning);

            Assert.IsFalse(ability.TryActivate(), "Still cooling down.");

            ability.Tick(1.1f);
            Assert.IsTrue(ability.CanActivate());
        }

        [Test]
        public void UnmetRequirement_BlocksActivationAndExplainsWhy()
        {
            AbilityDefinition definition = CreateAbility();
            var requirement = ScriptableObject.CreateInstance<FakeRequirement>();
            requirement.Satisfied = false;
            requirement.UnmetMessage = "You must be at a campfire to heal.";
            definition.Requirements = new AbilityRequirement[] { requirement };

            var (_, ability, _) = CreatePlayer(definition);

            Assert.IsFalse(ability.CanActivate());
            Assert.IsFalse(ability.AreRequirementsMet(out string reason));
            Assert.AreEqual("You must be at a campfire to heal.", reason);
            Assert.IsFalse(ability.TryActivate());
        }

        [Test]
        public void MetRequirement_AllowsActivation()
        {
            AbilityDefinition definition = CreateAbility();
            var requirement = ScriptableObject.CreateInstance<FakeRequirement>();
            requirement.Satisfied = true;
            definition.Requirements = new AbilityRequirement[] { requirement };

            var (_, ability, _) = CreatePlayer(definition);

            Assert.IsTrue(ability.AreRequirementsMet(out _));
            Assert.IsTrue(ability.TryActivate());
        }

        [Test]
        public void Charges_AreConsumedAndRefilled()
        {
            AbilityDefinition definition = CreateAbility();
            definition.MaxCharges = 2;
            var (_, ability, _) = CreatePlayer(definition);

            Assert.AreEqual(2, ability.ChargesRemaining);

            ability.TryActivate();
            ability.Tick(0.06f); // cast completes, ability resolves
            ability.Tick(0.06f); // recovery completes, ability is free again
            Assert.AreEqual(1, ability.ChargesRemaining);

            ability.TryActivate();
            ability.Tick(0.06f);
            ability.Tick(0.06f);
            Assert.AreEqual(0, ability.ChargesRemaining);
            Assert.IsFalse(ability.TryActivate(), "Out of charges.");

            ability.ResetCharges();
            Assert.AreEqual(2, ability.ChargesRemaining, "Resting refills charges.");
        }

        [Test]
        public void HealEffect_RestoresHealthOnTheTarget()
        {
            var effect = ScriptableObject.CreateInstance<HealEffect>();
            effect.Amount = 25f;

            // Reuse the enemy health implementation as a stand-in damageable/healable.
            EnemyStats stats = EnemyTestFixture.CreateStats(maxHealth: 100f);
            Enemy enemy = EnemyTestFixture.CreateEnemy(stats);

            try
            {
                enemy.Health.TakeDamage(EnemyTestFixture.Hit(40f));
                float before = enemy.Health.Current;

                var context = new EffectContext(null, DamageTeam.Player, enemy.transform.position, Vector2.right, enemy.gameObject);
                effect.Apply(in context);

                Assert.AreEqual(before + 25f, enemy.Health.Current, 0.001f);
            }
            finally
            {
                EnemyTestFixture.Destroy(enemy);
            }
        }

        [Test]
        public void ResourcePool_SpendsRestoresAndClamps()
        {
            ResourceDefinition mana = CreateResource(max: 50f);
            var (_, _, pool) = CreatePlayer(CreateAbility(), mana);

            Assert.AreEqual(50f, pool.GetAmount(mana), 0.001f);

            Assert.IsTrue(pool.TrySpend(new ResourceCost { Resource = mana, Amount = 20f }));
            Assert.AreEqual(30f, pool.GetAmount(mana), 0.001f);

            pool.Restore(mana, 999f);
            Assert.AreEqual(50f, pool.GetAmount(mana), 0.001f, "Restoring must clamp to the maximum.");

            Assert.IsFalse(pool.TrySpend(new ResourceCost { Resource = mana, Amount = 100f }));
            Assert.AreEqual(50f, pool.GetAmount(mana), 0.001f, "A failed spend must not partially drain the pool.");
        }
    }

    public class CheckpointTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            CheckpointService.Clear();
        }

        private Campfire CreateCampfire(string id)
        {
            go = new GameObject("Campfire");
            go.AddComponent<CircleCollider2D>().isTrigger = true;
            Campfire fire = go.AddComponent<Campfire>();

            var so = new UnityEditor.SerializedObject(fire);
            so.FindProperty("restPointId").stringValue = id;
            so.FindProperty("respawnFacing").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            return fire;
        }

        [Test]
        public void NoCheckpoint_ByDefault()
        {
            Assert.IsFalse(CheckpointService.HasCheckpoint);
        }

        [Test]
        public void SetCheckpoint_StoresPositionAndId()
        {
            Campfire fire = CreateCampfire("campfire_test");
            fire.transform.position = new Vector3(12f, 3f, 0f);

            CheckpointService.SetCheckpoint(fire, "Scene1");

            Assert.IsTrue(CheckpointService.HasCheckpoint);
            Assert.AreEqual("campfire_test", CheckpointService.Current.RestPointId);
            Assert.AreEqual("Scene1", CheckpointService.Current.SceneName);
            Assert.AreEqual(new Vector2(12f, 3f), CheckpointService.Current.Position);
        }

        [Test]
        public void Restore_ReinstatesASavedCheckpoint()
        {
            var saved = new CheckpointState
            {
                RestPointId = "campfire_saved",
                SceneName = "Scene2",
                X = 5f,
                Y = 6f,
                Facing = -1,
            };

            CheckpointService.Restore(saved);

            Assert.AreEqual("campfire_saved", CheckpointService.Current.RestPointId);
            Assert.AreEqual(new Vector2(5f, 6f), CheckpointService.Current.Position);
            Assert.AreEqual(-1, CheckpointService.Current.Facing);
        }

        [Test]
        public void CheckpointChanged_FiresOnSet()
        {
            Campfire fire = CreateCampfire("campfire_event");
            bool raised = false;
            Action<CheckpointState> handler = _ => raised = true;
            CheckpointService.CheckpointChanged += handler;

            try
            {
                CheckpointService.SetCheckpoint(fire, "Scene1");
                Assert.IsTrue(raised);
            }
            finally
            {
                CheckpointService.CheckpointChanged -= handler;
            }
        }
    }
}
