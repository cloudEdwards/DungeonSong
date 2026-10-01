using System;
using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Player;
using DungeonSong.World;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>A health pool the test drives directly, so damage and death can be raised on cue.</summary>
    internal class FakeHealth : MonoBehaviour, IHealth
    {
        public float CurrentValue = 100f;
        public float MaxValue = 100f;
        public bool Dead;

        public float Current => CurrentValue;
        public float Max => MaxValue;
        public float Normalized => MaxValue > 0f ? CurrentValue / MaxValue : 0f;
        public bool IsAlive => !Dead;
        public bool IsInvulnerable => false;

        public event Action<DamageInfo, DamageResult> Damaged;
        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float Heal(float amount)
        {
            float before = CurrentValue;
            CurrentValue = Mathf.Min(MaxValue, CurrentValue + amount);
            HealthChanged?.Invoke(CurrentValue, MaxValue);
            return CurrentValue - before;
        }

        public void GrantInvulnerability(float seconds) { }

        public void RestoreToFull()
        {
            Dead = false;
            CurrentValue = MaxValue;
        }

        public void Hit(float amount)
        {
            CurrentValue -= amount;
            var info = DamageInfo.Create(amount, DamageTeam.Enemy, Vector2.zero);
            Damaged?.Invoke(info, new DamageResult { Applied = true, AmountApplied = amount });
        }

        public void Kill()
        {
            CurrentValue = 0f;
            Dead = true;
            Died?.Invoke();
        }
    }

    /// <summary>
    /// Spell slots, Loyalty and resting: the D&amp;D casting rules, built on resource pools.
    /// Each test pins a behaviour that was checked by hand when it was built or fixed.
    /// </summary>
    public class SpellSlotAndRestTests
    {
        private GameObject go;
        private ResourceDefinition loyalty;
        private ResourceDefinition warlock;
        private ResourceDefinition paladin;

        [SetUp]
        public void SetUp()
        {
            ResourcePool.ResetSession();
            loyalty = Resource("loyalty", max: 100f, start: 0f, RestType.None, emptiedOnDeath: true);
            warlock = Resource("warlock_slots", max: 2f, start: 2f, RestType.Short | RestType.Long);
            paladin = Resource("paladin_slots", max: 2f, start: 2f, RestType.Long);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyPlayer();
            ResourcePool.ResetSession();
            CheckpointService.Clear();
        }

        // --- Fixture ---

        private static ResourceDefinition Resource(string id, float max, float start, RestType refill, bool emptiedOnDeath = false)
        {
            var r = ScriptableObject.CreateInstance<ResourceDefinition>();
            r.Id = id;
            r.DisplayName = id;
            r.MaxAmount = max;
            r.StartingAmount = start;
            r.RegenPerSecond = 0f;
            r.RefilledBy = refill;
            r.EmptiedOnDeath = emptiedOnDeath;
            return r;
        }

        private static AbilityDefinition Ability(ResourceDefinition resource = null, float cost = 0f)
        {
            var a = ScriptableObject.CreateInstance<AbilityDefinition>();
            a.Id = "probe";
            a.CastTime = 0.5f;
            a.Recovery = 0.1f;
            a.Cooldown = 0f;
            a.LockMovement = false;
            a.Targeting = TargetingMode.Self;
            a.Cost = new ResourceCost { Resource = resource, Amount = cost };
            return a;
        }

        private void DestroyPlayer()
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                go = null;
            }
        }

        /// <summary>Builds a player carrying Loyalty and both slot pools, plus any abilities.</summary>
        private PlayerActor CreatePlayer(params (Type type, AbilityDefinition definition)[] abilities)
        {
            go = new GameObject("TestPlayer");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<FakeMotionContext>();
            go.AddComponent<FakeHealth>();
            PlayerActor actor = go.AddComponent<PlayerActor>();
            go.AddComponent<PlayerCombat>();
            ResourcePool pool = go.AddComponent<ResourcePool>();

            var poolSo = new UnityEditor.SerializedObject(pool);
            UnityEditor.SerializedProperty list = poolSo.FindProperty("resources");
            list.arraySize = 3;
            list.GetArrayElementAtIndex(0).objectReferenceValue = loyalty;
            list.GetArrayElementAtIndex(1).objectReferenceValue = warlock;
            list.GetArrayElementAtIndex(2).objectReferenceValue = paladin;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            foreach ((Type type, AbilityDefinition definition) in abilities)
            {
                var ability = (AbilityBehaviour)go.AddComponent(type);
                var so = new UnityEditor.SerializedObject(ability);
                so.FindProperty("definition").objectReferenceValue = definition;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            actor.Initialize();
            return actor;
        }

        private static void Run(AbilityBehaviour ability, float seconds, float step = 0.05f)
        {
            for (float t = 0f; t < seconds; t += step)
            {
                ability.Tick(step);
            }
        }

        // --- Rests ---

        [Test]
        public void ShortRest_RefillsWarlockSlotsButNotPaladinSlots()
        {
            ResourcePool pool = CreatePlayer().Resources;
            pool.TrySpend(new ResourceCost { Resource = warlock, Amount = 2f });
            pool.TrySpend(new ResourceCost { Resource = paladin, Amount = 2f });

            pool.RestoreFor(RestType.Short);

            Assert.AreEqual(2f, pool.GetAmount(warlock), "Warlock slots come back on a short rest.");
            Assert.AreEqual(0f, pool.GetAmount(paladin), "Paladin slots only come back on a long rest.");
        }

        [Test]
        public void LongRest_RefillsBothSlotPoolsButLeavesLoyalty()
        {
            ResourcePool pool = CreatePlayer().Resources;
            pool.Restore(loyalty, 40f);
            pool.TrySpend(new ResourceCost { Resource = warlock, Amount = 1f });
            pool.TrySpend(new ResourceCost { Resource = paladin, Amount = 2f });

            pool.RestoreFor(RestType.Long);

            Assert.AreEqual(2f, pool.GetAmount(warlock));
            Assert.AreEqual(2f, pool.GetAmount(paladin));
            Assert.AreEqual(40f, pool.GetAmount(loyalty), "Resting never grants or removes Loyalty.");
        }

        [Test]
        public void Death_EmptiesLoyalty_AndRespawnRefillsSlots()
        {
            PlayerActor actor = CreatePlayer();
            ResourcePool pool = actor.Resources;
            pool.Restore(loyalty, 70f);
            pool.TrySpend(new ResourceCost { Resource = paladin, Amount = 2f });

            go.GetComponent<FakeHealth>().Kill();
            Assert.AreEqual(0f, pool.GetAmount(loyalty), "Dying loses Loyalty, like Silksong's silk.");

            actor.RespawnAt(Vector2.zero, 1);
            Assert.AreEqual(2f, pool.GetAmount(paladin), "Waking at a checkpoint restores what a long rest would.");
            Assert.AreEqual(0f, pool.GetAmount(loyalty));
        }

        [Test]
        public void PoolValues_SurviveTheSceneChange()
        {
            // The player object is rebuilt per scene; a door must not refill slots or wipe Loyalty.
            ResourcePool pool = CreatePlayer().Resources;
            pool.Restore(loyalty, 30f);
            pool.TrySpend(new ResourceCost { Resource = warlock, Amount = 1f });
            DestroyPlayer();

            ResourcePool next = CreatePlayer().Resources;

            Assert.AreEqual(30f, next.GetAmount(loyalty));
            Assert.AreEqual(1f, next.GetAmount(warlock));
        }

        [Test]
        public void ResetSession_StartsTheNextPlayerFresh()
        {
            CreatePlayer().Resources.Restore(loyalty, 30f);
            DestroyPlayer();

            ResourcePool.ResetSession();
            ResourcePool next = CreatePlayer().Resources;

            Assert.AreEqual(0f, next.GetAmount(loyalty));
            Assert.AreEqual(2f, next.GetAmount(warlock));
        }

        [Test]
        public void SetAmountById_ClampsAndIgnoresUnknownIds()
        {
            ResourcePool pool = CreatePlayer().Resources;

            Assert.IsTrue(pool.SetAmountById("loyalty", 55f));
            Assert.AreEqual(55f, pool.GetAmount(loyalty));

            Assert.IsTrue(pool.SetAmountById("warlock_slots", 9f));
            Assert.AreEqual(2f, pool.GetAmount(warlock), "Loaded values are clamped to the pool's range.");

            Assert.IsFalse(pool.SetAmountById("mana", 10f), "A resource the player no longer carries is skipped.");
        }

        // --- Casting ---

        [Test]
        public void Cantrip_CostsNothing()
        {
            PlayerActor actor = CreatePlayer((typeof(ProbeAbility), Ability()));
            var cantrip = go.GetComponent<ProbeAbility>();

            Assert.IsTrue(cantrip.TryActivate());
            Assert.AreEqual(2f, actor.Resources.GetAmount(warlock));
            Assert.AreEqual(2f, actor.Resources.GetAmount(paladin));
        }

        [Test]
        public void SlotSpell_RefusesWithNoSlotsLeft()
        {
            PlayerActor actor = CreatePlayer((typeof(ProbeAbility), Ability(paladin, 1f)));
            var spell = go.GetComponent<ProbeAbility>();
            actor.Resources.TrySpend(new ResourceCost { Resource = paladin, Amount = 2f });

            Assert.IsFalse(spell.TryActivate());
        }

        [Test]
        public void SpendCostOnResolve_InterruptedCastKeepsTheCost()
        {
            AbilityDefinition rest = Ability(loyalty, 100f);
            rest.SpendCostOnResolve = true;
            rest.InterruptedByDamage = true;
            PlayerActor actor = CreatePlayer((typeof(ProbeAbility), rest));
            var ability = go.GetComponent<ProbeAbility>();
            actor.Resources.Restore(loyalty, 100f);

            Assert.IsTrue(ability.TryActivate());
            Assert.AreEqual(100f, actor.Resources.GetAmount(loyalty), "Nothing is spent while channelling.");

            go.GetComponent<FakeHealth>().Hit(5f);

            Assert.IsFalse(ability.IsRunning, "Damage during the wind-up interrupts the cast.");
            Assert.AreEqual(0, ability.ResolveCount);
            Assert.AreEqual(100f, actor.Resources.GetAmount(loyalty), "An interrupted short rest keeps its Loyalty.");
        }

        [Test]
        public void SpendCostOnResolve_CompletedCastSpends()
        {
            AbilityDefinition rest = Ability(loyalty, 100f);
            rest.SpendCostOnResolve = true;
            PlayerActor actor = CreatePlayer((typeof(ProbeAbility), rest));
            var ability = go.GetComponent<ProbeAbility>();
            actor.Resources.Restore(loyalty, 100f);

            ability.TryActivate();
            Run(ability, 0.55f);

            Assert.AreEqual(1, ability.ResolveCount);
            Assert.AreEqual(0f, actor.Resources.GetAmount(loyalty));
        }

        [Test]
        public void DamageAfterResolve_DoesNotCancel()
        {
            AbilityDefinition definition = Ability();
            definition.InterruptedByDamage = true;
            definition.Recovery = 1f;
            CreatePlayer((typeof(ProbeAbility), definition));
            var ability = go.GetComponent<ProbeAbility>();

            ability.TryActivate();
            Run(ability, 0.55f);
            Assert.AreEqual(1, ability.ResolveCount);

            go.GetComponent<FakeHealth>().Hit(5f);

            Assert.IsTrue(ability.IsRunning, "Once resolved, the ability has happened; recovery is not interruptible.");
        }

        [Test]
        public void SuspendInAir_HoversWhileCasting_ThenFallsAgain()
        {
            AbilityDefinition rest = Ability();
            rest.SuspendInAir = true;
            CreatePlayer((typeof(ProbeAbility), rest));
            var ability = go.GetComponent<ProbeAbility>();
            var body = go.GetComponent<Rigidbody2D>();
            go.GetComponent<FakeMotionContext>().Grounded = false;
            body.gravityScale = 3f;
            body.linearVelocity = new Vector2(2f, -8f);

            ability.TryActivate();

            Assert.AreEqual(0f, body.gravityScale, "Gravity is off while resting in the air.");
            Assert.AreEqual(Vector2.zero, body.linearVelocity, "The fall stops the moment the rest begins.");

            body.linearVelocity = new Vector2(0f, -3f);
            ability.Tick(0.05f);
            Assert.AreEqual(Vector2.zero, body.linearVelocity, "Held in place for the whole channel.");

            Run(ability, 0.7f);
            Assert.IsFalse(ability.IsRunning);
            Assert.AreEqual(3f, body.gravityScale, "Gravity comes back exactly as it was.");
        }

        [Test]
        public void SuspendInAir_InterruptedRestoresGravity()
        {
            AbilityDefinition rest = Ability();
            rest.SuspendInAir = true;
            rest.InterruptedByDamage = true;
            CreatePlayer((typeof(ProbeAbility), rest));
            var ability = go.GetComponent<ProbeAbility>();
            var body = go.GetComponent<Rigidbody2D>();
            go.GetComponent<FakeMotionContext>().Grounded = false;
            body.gravityScale = 1f;

            ability.TryActivate();
            go.GetComponent<FakeHealth>().Hit(5f);

            Assert.AreEqual(1f, body.gravityScale);
        }

        [Test]
        public void SuspendInAir_GroundedCastIsLeftAlone()
        {
            AbilityDefinition rest = Ability();
            rest.SuspendInAir = true;
            CreatePlayer((typeof(ProbeAbility), rest));
            var body = go.GetComponent<Rigidbody2D>();
            body.gravityScale = 1f;

            go.GetComponent<ProbeAbility>().TryActivate();

            Assert.AreEqual(1f, body.gravityScale);
        }

        // --- Divine Smite ---

        private SmiteAbility CreateSmite(out PlayerActor actor)
        {
            AbilityDefinition definition = Ability(paladin, 1f);
            definition.CastTime = 0.05f;
            definition.Recovery = 0f;
            actor = CreatePlayer((typeof(SmiteAbility), definition));
            return go.GetComponent<SmiteAbility>();
        }

        private static DamageInfo Swing(SmiteAbility smite)
        {
            DamageInfo info = DamageInfo.Create(10f, DamageTeam.Player, Vector2.zero);
            info.Type = DamageType.Physical;
            ((IAttackModifier)smite).ModifyAttack(null, ref info);
            return info;
        }

        [Test]
        public void Smite_SpendsAPaladinSlotOnCast_HitOrMiss()
        {
            SmiteAbility smite = CreateSmite(out PlayerActor actor);

            smite.TryActivate();
            Run(smite, 0.1f);

            Assert.AreEqual(1f, actor.Resources.GetAmount(paladin));
            Assert.AreEqual(3, smite.ActiveStacks);
        }

        [Test]
        public void Smite_EmpowersThreeSwings_ThenEnds()
        {
            SmiteAbility smite = CreateSmite(out _);
            smite.TryActivate();
            Run(smite, 0.1f);

            for (int i = 0; i < 3; i++)
            {
                DamageInfo empowered = Swing(smite);
                Assert.AreEqual(20f, empowered.Amount, 0.001f, $"Swing {i + 1} carries the radiant bonus.");
                Assert.IsTrue((empowered.Type & DamageType.Holy) != 0);
            }

            Assert.AreEqual(0, smite.ActiveStacks);
            Assert.IsFalse(smite.IsEmpowered);
            Assert.AreEqual(10f, Swing(smite).Amount, 0.001f, "The fourth swing is ordinary.");
        }

        [Test]
        public void Smite_UnusedStrikesFadeAfterTheDuration()
        {
            SmiteAbility smite = CreateSmite(out _);
            smite.TryActivate();
            Run(smite, 0.1f);
            Swing(smite);

            Run(smite, 5.1f);

            Assert.IsFalse(smite.IsEmpowered, "A smite cannot be banked.");
        }

        [Test]
        public void Smite_CannotBeRecastWhileEmpowered()
        {
            SmiteAbility smite = CreateSmite(out PlayerActor actor);
            smite.TryActivate();
            Run(smite, 0.1f);

            Assert.IsFalse(smite.TryActivate(), "Recasting would waste a slot.");
            Assert.AreEqual(1f, actor.Resources.GetAmount(paladin));
        }

        [Test]
        public void Smite_TintsThePlayerOnlyWhileEmpowered()
        {
            SmiteAbility smite = CreateSmite(out _);
            var sprite = go.GetComponent<SpriteRenderer>();
            sprite.color = Color.white;

            smite.TryActivate();
            Run(smite, 0.1f);
            Assert.AreNotEqual(Color.white, sprite.color);

            Swing(smite);
            Swing(smite);
            Swing(smite);
            Assert.AreEqual(Color.white, sprite.color);
        }
    }

    /// <summary>
    /// The shipped spell assets. Tuning lives in data, so a stray edit in the inspector is a
    /// regression these catch: the 30% short rest heal, cantrip costs, the key layout.
    /// </summary>
    public class SpellbookDataTests
    {
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"Missing asset at {path}");
            return asset;
        }

        private static AbilityDefinition Ability(string name) => Load<AbilityDefinition>($"Assets/PlayerData/Abilities/{name}.asset");

        [Test]
        public void EldritchBlast_IsACantrip()
        {
            Assert.IsTrue(Ability("Ability_EldritchBlast").Cost.IsFree);
        }

        [Test]
        public void SlotSpells_CostOneSlotOfTheRightClass()
        {
            Assert.AreEqual("warlock_slots", Ability("Ability_BurningHands").Cost.Resource.Id);
            Assert.AreEqual("paladin_slots", Ability("Ability_DivineSmite").Cost.Resource.Id);
            Assert.AreEqual("paladin_slots", Ability("Ability_CureWounds").Cost.Resource.Id);

            Assert.AreEqual(1f, Ability("Ability_BurningHands").Cost.Amount);
            Assert.AreEqual(1f, Ability("Ability_DivineSmite").Cost.Amount);
            Assert.AreEqual(1f, Ability("Ability_CureWounds").Cost.Amount);
            Assert.IsEmpty(Ability("Ability_CureWounds").Requirements, "Cure Wounds works anywhere now.");
        }

        [Test]
        public void ShortRest_NeedsFullLoyalty_HoversAndSurvivesInterruption()
        {
            AbilityDefinition rest = Ability("Ability_ShortRest");

            Assert.AreEqual("loyalty", rest.Cost.Resource.Id);
            Assert.AreEqual(rest.Cost.Resource.MaxAmount, rest.Cost.Amount, "A short rest needs a full bar.");
            Assert.IsTrue(rest.SpendCostOnResolve);
            Assert.IsTrue(rest.SuspendInAir);
            Assert.IsTrue(rest.InterruptedByDamage);
        }

        [Test]
        public void ShortRest_HealsThirtyPercent_AndRestoresShortRestPools()
        {
            AbilityDefinition rest = Ability("Ability_ShortRest");

            HealEffect heal = null;
            RestoreResourcesEffect restore = null;
            foreach (GameplayEffect effect in rest.Effects)
            {
                heal = heal != null ? heal : effect as HealEffect;
                restore = restore != null ? restore : effect as RestoreResourcesEffect;
            }

            Assert.IsNotNull(heal);
            Assert.AreEqual(0.3f, heal.PercentOfMax, 0.0001f);
            Assert.AreEqual(0f, heal.Amount);
            Assert.IsNotNull(restore);
            Assert.AreEqual(RestType.Short, restore.Rest);
        }

        [Test]
        public void Resources_RecoverOnTheRightRests()
        {
            var loyalty = Load<ResourceDefinition>("Assets/PlayerData/Resource_Loyalty.asset");
            var warlock = Load<ResourceDefinition>("Assets/PlayerData/Resource_WarlockSlots.asset");
            var paladin = Load<ResourceDefinition>("Assets/PlayerData/Resource_PaladinSlots.asset");

            Assert.AreEqual(RestType.None, loyalty.RefilledBy);
            Assert.IsTrue(loyalty.EmptiedOnDeath);
            Assert.AreEqual(0f, loyalty.StartingAmount);
            Assert.AreEqual(RestType.Short | RestType.Long, warlock.RefilledBy);
            Assert.AreEqual(RestType.Long, paladin.RefilledBy);
            Assert.AreEqual(2f, warlock.MaxAmount, "Level 2 Warlock.");
            Assert.AreEqual(2f, paladin.MaxAmount, "Level 2 Paladin.");
        }

        [Test]
        public void Player_HasTheAgreedKeyLayout()
        {
            var knight = Load<GameObject>("Assets/Prefabs/KnightAxios.prefab");
            var input = knight.GetComponent<LegacyInputSource>();
            var loadout = knight.GetComponent<AbilityLoadout>();

            string[] keys = { "Q", "E", "R", "C", "Tab" };
            string[] ids = { "eldritch_blast", "divine_smite", "burning_hands", "cure_wounds", "short_rest" };

            Assert.AreEqual(ids.Length, loadout.Slots.Count);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(keys[i], input.GetAbilityKeyLabel(i), $"Slot {i} key");
                Assert.AreEqual(ids[i], loadout.Slots[i].Definition.Id, $"Slot {i} ability");
            }

            Assert.AreEqual("F", input.InteractKeyLabel, "F stays interact / long rest.");
        }

        [Test]
        public void Player_GainsLoyaltyFromHits()
        {
            var knight = Load<GameObject>("Assets/Prefabs/KnightAxios.prefab");
            Assert.IsNotNull(knight.GetComponent<ResourceOnHit>());
        }
    }
}
