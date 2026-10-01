using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Player;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>
    /// Stands in for PlayerController so combat can be tested without movement, input or a
    /// rig. Combat only ever sees this interface, which is what makes that possible.
    /// </summary>
    internal class FakeMotionContext : MonoBehaviour, IPlayerMotionContext
    {
        public int Facing = 1;
        public bool Grounded = true;
        public bool TouchingWall;
        public bool WallSliding;
        public bool Committed;
        public Vector2 CurrentVelocity;
        public bool MovementLocked;

        public int FacingDirection => Facing;

        public bool IsGrounded => Grounded;

        public bool IsTouchingWall => TouchingWall;

        public bool IsWallSliding => WallSliding;

        public bool IsInCommittedMove => Committed;

        public Vector2 Velocity => CurrentVelocity;

        public void SetMovementLock(bool locked) => MovementLocked = locked;

        public void SetVelocity(Vector2 velocity) => CurrentVelocity = velocity;

        public void SetFacing(int sign) => Facing = sign >= 0 ? 1 : -1;
    }

    /// <summary>A requirement whose answer the test controls.</summary>
    internal class FakeRequirement : AbilityRequirement
    {
        public bool Satisfied = true;

        public override bool IsSatisfied(PlayerActor player) => Satisfied;
    }

    public class PlayerCombatTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        private static PlayerAttackDefinition CreateAttack(
            AttackDirection direction,
            AttackContext context = AttackContext.Grounded | AttackContext.Airborne,
            int comboIndex = 0,
            int priority = 0,
            float damage = 10f)
        {
            var a = ScriptableObject.CreateInstance<PlayerAttackDefinition>();
            a.DisplayName = $"{direction} attack";
            a.Direction = direction;
            a.AllowedContext = context;
            a.ComboIndex = comboIndex;
            a.Priority = priority;
            a.Startup = 0.05f;
            a.Active = 0.05f;
            a.Recovery = 0.05f;
            a.Cooldown = 0f;
            a.HitboxKey = "Forward";

            AttackPayload payload = AttackPayload.Default;
            payload.Damage = damage;
            a.Payload = payload;
            return a;
        }

        private (PlayerActor actor, PlayerCombat combat, FakeMotionContext motion) CreatePlayer(params PlayerAttackDefinition[] attacks)
        {
            go = new GameObject("TestPlayer");
            go.AddComponent<Rigidbody2D>();
            FakeMotionContext motion = go.AddComponent<FakeMotionContext>();
            PlayerActor actor = go.AddComponent<PlayerActor>();
            PlayerCombat combat = go.AddComponent<PlayerCombat>();

            var so = new UnityEditor.SerializedObject(combat);
            UnityEditor.SerializedProperty list = so.FindProperty("attacks");
            list.arraySize = attacks.Length;
            for (int i = 0; i < attacks.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = attacks[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            actor.Initialize();
            return (actor, combat, motion);
        }

        [Test]
        public void ForwardAttack_IsSelectedOnTheGround()
        {
            PlayerAttackDefinition forward = CreateAttack(AttackDirection.Forward);
            var (_, combat, _) = CreatePlayer(forward);

            Assert.IsTrue(combat.TryAttack(AttackDirection.Forward));
            Assert.AreSame(forward, combat.CurrentAttack);
            Assert.AreEqual(PlayerAttackPhase.Startup, combat.Phase);
        }

        [Test]
        public void UpAndDown_SelectDifferentAttacks()
        {
            PlayerAttackDefinition up = CreateAttack(AttackDirection.Up);
            PlayerAttackDefinition down = CreateAttack(AttackDirection.Down);
            var (_, combat, _) = CreatePlayer(up, down);

            combat.TryAttack(AttackDirection.Up);
            Assert.AreSame(up, combat.CurrentAttack, "Up input must select the up attack.");

            combat.CancelAttack();
            combat.TryAttack(AttackDirection.Down);
            Assert.AreSame(down, combat.CurrentAttack);
        }

        [Test]
        public void AirDiagonal_WithNoDiagonalAttack_FallsBackToTheVerticalAttack()
        {
            // In the air, W/S plus a horizontal key reads as a diagonal. With no diagonal
            // attacks authored, that must still up-slash or pogo rather than do nothing.
            PlayerAttackDefinition up = CreateAttack(AttackDirection.Up);
            PlayerAttackDefinition pogo = CreateAttack(AttackDirection.Down, AttackContext.Airborne);
            var (_, combat, motion) = CreatePlayer(up, pogo);
            motion.Grounded = false;

            Assert.IsTrue(combat.TryAttack(AttackDirection.DiagonalDownForward));
            Assert.AreSame(pogo, combat.CurrentAttack, "Down + forward in the air is still a pogo.");

            combat.CancelAttack();
            Assert.IsTrue(combat.TryAttack(AttackDirection.DiagonalUpForward));
            Assert.AreSame(up, combat.CurrentAttack);
        }

        [Test]
        public void AirDiagonal_PrefersADiagonalAttackWhenOneExists()
        {
            PlayerAttackDefinition pogo = CreateAttack(AttackDirection.Down, AttackContext.Airborne);
            PlayerAttackDefinition diagonal = CreateAttack(AttackDirection.DiagonalDownForward, AttackContext.Airborne);
            var (_, combat, motion) = CreatePlayer(pogo, diagonal);
            motion.Grounded = false;

            combat.TryAttack(AttackDirection.DiagonalDownForward);

            Assert.AreSame(diagonal, combat.CurrentAttack);
        }

        [Test]
        public void ZeroStartup_IsLiveTheMomentItStarts()
        {
            // A pogo with a wind-up lets the player land on the enemy before the hitbox opens.
            PlayerAttackDefinition pogo = CreateAttack(AttackDirection.Down, AttackContext.Airborne);
            pogo.Startup = 0f;
            var (_, combat, motion) = CreatePlayer(pogo);
            motion.Grounded = false;

            combat.TryAttack(AttackDirection.Down);

            Assert.AreEqual(PlayerAttackPhase.Active, combat.Phase, "No frame passes before the hitbox opens.");
        }

        [Test]
        public void AirborneOnlyAttack_IsNotSelectableOnTheGround()
        {
            PlayerAttackDefinition pogo = CreateAttack(AttackDirection.Down, AttackContext.Airborne);
            var (_, combat, motion) = CreatePlayer(pogo);

            motion.Grounded = true;
            Assert.IsFalse(combat.TryAttack(AttackDirection.Down), "A pogo must not come out while standing.");

            motion.Grounded = false;
            Assert.IsTrue(combat.TryAttack(AttackDirection.Down));
        }

        [Test]
        public void WallAttack_RequiresWallContext()
        {
            PlayerAttackDefinition wall = CreateAttack(AttackDirection.Wall, AttackContext.OnWall);
            var (_, combat, motion) = CreatePlayer(wall);

            Assert.IsFalse(combat.TryAttack(AttackDirection.Wall));

            motion.WallSliding = true;
            Assert.AreEqual(AttackContext.OnWall, combat.CurrentContext());
            Assert.IsTrue(combat.TryAttack(AttackDirection.Wall));
        }

        [Test]
        public void HigherPriority_WinsWhenBothMatch()
        {
            PlayerAttackDefinition plain = CreateAttack(AttackDirection.Down, AttackContext.Airborne, priority: 0);
            PlayerAttackDefinition pogo = CreateAttack(AttackDirection.Down, AttackContext.Airborne, priority: 10);
            var (_, combat, motion) = CreatePlayer(plain, pogo);
            motion.Grounded = false;

            combat.TryAttack(AttackDirection.Down);

            Assert.AreSame(pogo, combat.CurrentAttack);
        }

        [Test]
        public void Attack_RunsThroughStartupActiveRecovery()
        {
            PlayerAttackDefinition forward = CreateAttack(AttackDirection.Forward);
            var (_, combat, _) = CreatePlayer(forward);
            combat.TryAttack(AttackDirection.Forward);

            Assert.AreEqual(PlayerAttackPhase.Startup, combat.Phase);

            combat.Tick(0.06f);
            Assert.AreEqual(PlayerAttackPhase.Active, combat.Phase, "The hitbox opens only after startup.");

            combat.Tick(0.06f);
            Assert.AreEqual(PlayerAttackPhase.Recovery, combat.Phase);

            combat.Tick(0.06f);
            Assert.IsFalse(combat.IsAttacking);
        }

        [Test]
        public void ComboFollowUp_IsSelectedInsideTheWindow()
        {
            PlayerAttackDefinition first = CreateAttack(AttackDirection.Forward);
            PlayerAttackDefinition second = CreateAttack(AttackDirection.Forward, comboIndex: 1);
            first.FollowUp = second;
            first.ComboWindow = 0.5f;

            var (_, combat, _) = CreatePlayer(first, second);

            combat.TryAttack(AttackDirection.Forward);
            combat.Tick(0.06f); // active
            combat.Tick(0.06f); // recovery, combo window opens
            Assert.IsTrue(combat.IsComboWindowOpen);

            combat.TryAttack(AttackDirection.Forward);
            Assert.AreSame(second, combat.CurrentAttack, "A second press inside the window must chain, not repeat.");
        }

        [Test]
        public void ComboFollowUp_IsNotSelectableCold()
        {
            // Follow-ups must be unreachable except through the chain, or every combo would
            // open with its own finisher.
            PlayerAttackDefinition first = CreateAttack(AttackDirection.Forward);
            PlayerAttackDefinition second = CreateAttack(AttackDirection.Forward, comboIndex: 1);
            var (_, combat, _) = CreatePlayer(second, first);

            combat.TryAttack(AttackDirection.Forward);

            Assert.AreSame(first, combat.CurrentAttack);
        }

        [Test]
        public void LockMovement_PushesAndReleasesTheMovementLock()
        {
            PlayerAttackDefinition heavy = CreateAttack(AttackDirection.Forward);
            heavy.LockMovement = true;
            var (_, combat, motion) = CreatePlayer(heavy);

            combat.TryAttack(AttackDirection.Forward);
            Assert.IsTrue(motion.MovementLocked, "A rooting attack must lock movement.");

            combat.Tick(0.06f);
            combat.Tick(0.06f);
            combat.Tick(0.06f);

            Assert.IsFalse(motion.MovementLocked, "The lock must be released when the attack ends.");
        }

        [Test]
        public void CancelAttack_ReleasesTheMovementLock()
        {
            PlayerAttackDefinition heavy = CreateAttack(AttackDirection.Forward);
            heavy.LockMovement = true;
            var (_, combat, motion) = CreatePlayer(heavy);

            combat.TryAttack(AttackDirection.Forward);
            combat.CancelAttack();

            Assert.IsFalse(motion.MovementLocked, "Cancelling mid-attack must never leave the player rooted.");
        }

        [Test]
        public void Cooldown_BlocksReuse()
        {
            PlayerAttackDefinition forward = CreateAttack(AttackDirection.Forward);
            forward.Cooldown = 1f;
            var (_, combat, _) = CreatePlayer(forward);

            combat.TryAttack(AttackDirection.Forward);
            combat.Tick(0.06f);
            combat.Tick(0.06f);
            combat.Tick(0.06f);
            Assert.IsFalse(combat.IsAttacking);

            Assert.IsFalse(combat.TryAttack(AttackDirection.Forward), "The attack is still cooling down.");
        }

        [Test]
        public void NoMatchingAttack_FailsQuietly()
        {
            PlayerAttackDefinition forward = CreateAttack(AttackDirection.Forward);
            var (_, combat, _) = CreatePlayer(forward);

            Assert.IsFalse(combat.TryAttack(AttackDirection.Up), "No up attack is configured, so nothing should happen.");
            Assert.IsFalse(combat.IsAttacking);
        }
    }

    /// <summary>
    /// The shipped attack set: neutral, W and S plus attack, on the ground and in the air.
    /// Pins the agreed controls so a stray edit to an attack asset shows up as a failure.
    /// </summary>
    public class AttackCatalogDataTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        private (PlayerCombat combat, FakeMotionContext motion) CreatePlayerWithShippedAttacks()
        {
            var knight = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/KnightAxios.prefab");
            UnityEditor.SerializedProperty shipped = new UnityEditor.SerializedObject(knight.GetComponent<PlayerCombat>()).FindProperty("attacks");

            go = new GameObject("TestPlayer");
            go.AddComponent<Rigidbody2D>();
            FakeMotionContext motion = go.AddComponent<FakeMotionContext>();
            PlayerActor actor = go.AddComponent<PlayerActor>();
            PlayerCombat combat = go.AddComponent<PlayerCombat>();

            var so = new UnityEditor.SerializedObject(combat);
            UnityEditor.SerializedProperty list = so.FindProperty("attacks");
            list.arraySize = shipped.arraySize;
            for (int i = 0; i < shipped.arraySize; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = shipped.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            actor.Initialize();
            return (combat, motion);
        }

        private static string Select(PlayerCombat combat, AttackDirection direction)
        {
            combat.CancelAttack();
            return combat.TryAttack(direction) ? combat.CurrentAttack.HitboxKey : "nothing";
        }

        [TestCase(true, AttackDirection.Forward, "Forward")]
        [TestCase(true, AttackDirection.Up, "Up")]
        [TestCase(true, AttackDirection.Down, "Down")]
        [TestCase(false, AttackDirection.Forward, "Forward")]
        [TestCase(false, AttackDirection.Up, "Up")]
        [TestCase(false, AttackDirection.Down, "Down")]
        [TestCase(false, AttackDirection.DiagonalUpForward, "Up")]
        [TestCase(false, AttackDirection.DiagonalDownForward, "Down")]
        public void Input_SelectsTheMatchingDirectionalAttack(bool grounded, AttackDirection direction, string expectedHitbox)
        {
            var (combat, motion) = CreatePlayerWithShippedAttacks();
            motion.Grounded = grounded;

            Assert.AreEqual(expectedHitbox, Select(combat, direction));
        }

        [Test]
        public void AirDownAttack_IsAPogo_AndGroundDownIsNot()
        {
            var (combat, motion) = CreatePlayerWithShippedAttacks();

            motion.Grounded = false;
            combat.TryAttack(AttackDirection.Down);
            Assert.Greater(combat.CurrentAttack.RecoilOnHit, 0f, "A down attack that connects in the air bounces the player.");

            motion.Grounded = true;
            combat.CancelAttack();
            combat.TryAttack(AttackDirection.Down);
            Assert.AreEqual(0f, combat.CurrentAttack.RecoilOnHit, "No bounce from a grounded down slash.");
        }

        [Test]
        public void Pogo_IsInstant_AndProtectsTheBounce()
        {
            var (combat, motion) = CreatePlayerWithShippedAttacks();
            motion.Grounded = false;

            combat.TryAttack(AttackDirection.Down);

            Assert.AreEqual(0f, combat.CurrentAttack.Startup, "No wind-up, or the player lands on the enemy first.");
            Assert.Greater(combat.CurrentAttack.InvulnerabilityOnRecoil, 0f, "A landed pogo grants i-frames.");
        }
    }
}
