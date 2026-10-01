using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using DungeonSong.Player;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>
    /// The shipped controls, pressed on simulated devices against the real actions asset:
    /// every key does what the HUD says it does.
    /// </summary>
    public class InputSystemSourceTests : InputTestFixture
    {
        private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad pad;
        private GameObject go;
        private InputSystemSource source;
        private InputActionAsset actions;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            pad = InputSystem.AddDevice<Gamepad>();

            // A private copy, so enabling actions never touches the project-wide asset.
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            actions = Object.Instantiate(shipped);
            actions.Enable();

            go = new GameObject("InputSource");
            source = go.AddComponent<InputSystemSource>();
            var so = new UnityEditor.SerializedObject(source);
            so.FindProperty("actions").objectReferenceValue = actions;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(go);
            actions.Disable();
            Object.DestroyImmediate(actions);
            base.TearDown();
        }

        [TestCase(0, "Q")]
        [TestCase(1, "E")]
        [TestCase(2, "R")]
        [TestCase(3, "C")]
        [TestCase(4, "Tab")]
        public void AbilityKey_FiresItsSlot_AndLabelsIt(int slot, string label)
        {
            Assert.AreEqual(label, source.GetAbilityKeyLabel(slot), "The HUD shows the bound key.");

            Key key = label == "Tab" ? Key.Tab : (Key)System.Enum.Parse(typeof(Key), label);
            Press(keyboard[key]);

            Assert.IsTrue(source.AbilityPressed(slot), $"{label} activates slot {slot}.");
            for (int other = 0; other < source.AbilitySlotCount; other++)
            {
                if (other != slot)
                {
                    Assert.IsFalse(source.AbilityPressed(other), $"{label} must not also fire slot {other}.");
                }
            }
        }

        [Test]
        public void F_Interacts()
        {
            Assert.AreEqual("F", source.InteractKeyLabel);
            Press(keyboard.fKey);
            Assert.IsTrue(source.InteractPressed);
        }

        [Test]
        public void E_IsSmite_NotInteract()
        {
            // The template bound interact to E; here E is Divine Smite.
            Press(keyboard.eKey);
            Assert.IsFalse(source.InteractPressed);
        }

        [Test]
        public void Space_JumpsOnPress_AndReportsTheRelease()
        {
            Press(keyboard.spaceKey);
            Assert.IsTrue(source.JumpPressed);

            Release(keyboard.spaceKey);
            Assert.IsTrue(source.JumpReleased, "The release shortens the jump.");
        }

        [Test]
        public void LeftShift_Rolls()
        {
            Press(keyboard.leftShiftKey);
            Assert.IsTrue(source.RollPressed);
        }

        [Test]
        public void LeftMouse_Attacks()
        {
            Press(mouse.leftButton);
            Assert.IsTrue(source.AttackPressed);
            Assert.IsTrue(source.AttackHeld);

            Release(mouse.leftButton);
            Assert.IsTrue(source.AttackReleased);
        }

        [Test]
        public void RightMouse_Blocks()
        {
            Press(mouse.rightButton);
            Assert.IsTrue(source.BlockPressed);

            Release(mouse.rightButton);
            Assert.IsTrue(source.BlockReleased);
        }

        [TestCase(Key.D, 1f, 0f)]
        [TestCase(Key.A, -1f, 0f)]
        [TestCase(Key.W, 0f, 1f)]
        [TestCase(Key.S, 0f, -1f)]
        [TestCase(Key.RightArrow, 1f, 0f)]
        [TestCase(Key.UpArrow, 0f, 1f)]
        public void MovementKeys_DriveTheMoveAxis(Key key, float x, float y)
        {
            Press(keyboard[key]);

            Vector2 axis = source.MoveAxis;
            Assert.AreEqual(x, axis.x, 0.001f);
            Assert.AreEqual(y, axis.y, 0.001f, "W/S also aim the up and down attacks.");
        }

        // --- Gamepad: Hollow Knight-style layout ---

        private ButtonControl Pad(string path) => (ButtonControl)pad[path];

        [TestCase(0, "rightTrigger")]
        [TestCase(1, "leftTrigger")]
        [TestCase(2, "buttonEast")]
        [TestCase(3, "dpad/down")]
        [TestCase(4, "select")]
        public void PadButton_FiresItsAbilitySlot(int slot, string button)
        {
            Press(Pad(button));

            Assert.IsTrue(source.AbilityPressed(slot), $"{button} activates slot {slot}.");
            for (int other = 0; other < source.AbilitySlotCount; other++)
            {
                if (other != slot)
                {
                    Assert.IsFalse(source.AbilityPressed(other), $"{button} must not also fire slot {other}.");
                }
            }
        }

        [Test]
        public void Pad_RollsBlocksJumpsAttacksAndInteracts()
        {
            Press(pad.rightShoulder);
            Assert.IsTrue(source.RollPressed, "RB rolls.");

            Press(pad.leftShoulder);
            Assert.IsTrue(source.BlockPressed, "LB blocks.");
            Release(pad.leftShoulder);
            Assert.IsTrue(source.BlockReleased);

            Press(pad.buttonSouth);
            Assert.IsTrue(source.JumpPressed, "South jumps.");

            Press(pad.buttonWest);
            Assert.IsTrue(source.AttackPressed, "West attacks.");

            Press(pad.buttonNorth);
            Assert.IsTrue(source.InteractPressed, "North interacts.");
        }

        [Test]
        public void DpadDown_CastsWithoutAimingDown()
        {
            // D-pad down is Cure Wounds. If it also fed Move, it would aim attacks downward.
            Press(pad.dpad.down);
            Assert.AreEqual(Vector2.zero, source.MoveAxis);
        }

        [Test]
        public void LeftStick_Moves()
        {
            Set(pad.leftStick, new Vector2(1f, 0f));
            Assert.Greater(source.MoveAxis.x, 0.9f);
        }

        [Test]
        public void Labels_FollowTheDeviceInUse()
        {
            int changes = 0;
            source.ControlsChanged += () => changes++;
            Assert.IsFalse(source.UsingGamepad);
            Assert.AreEqual("Q", source.GetAbilityKeyLabel(0));

            Press(pad.rightTrigger);
            Assert.IsTrue(source.UsingGamepad);
            Assert.AreEqual(1, changes, "Picking up the pad announces the change once.");

            string padLabel = source.GetAbilityKeyLabel(0);
            TestContext.WriteLine($"Pad labels: {string.Join(", ", System.Linq.Enumerable.Range(0, source.AbilitySlotCount).Select(source.GetAbilityKeyLabel))}; interact {source.InteractKeyLabel}");
            Assert.IsNotEmpty(padLabel);
            Assert.AreNotEqual("Q", padLabel, "The HUD shows the pad button, not the key.");
            Assert.LessOrEqual(padLabel.Length, 6, "Fits the ability slot.");

            Release(pad.rightTrigger);
            Press(pad.rightTrigger);
            Assert.AreEqual(1, changes, "More pad input is not another change.");

            // Let go first: while RT holds Ability1 down, Q on the same action does not re-trigger it.
            Release(pad.rightTrigger);
            Press(keyboard.qKey);
            Assert.IsFalse(source.UsingGamepad);
            Assert.AreEqual(2, changes);
            Assert.AreEqual("Q", source.GetAbilityKeyLabel(0), "Back on the keyboard, back to keys.");
        }

        [Test]
        public void EveryAbilityLabel_FitsItsSlot_OnBothDevices()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int slot = 0; slot < source.AbilitySlotCount; slot++)
                {
                    string label = source.GetAbilityKeyLabel(slot);
                    Assert.IsNotEmpty(label, $"Slot {slot} is bound (gamepad={source.UsingGamepad}).");
                    Assert.LessOrEqual(label.Length, 6, $"'{label}' is too long for an ability slot (gamepad={source.UsingGamepad}).");
                }

                Press(pad.rightTrigger);
                Release(pad.rightTrigger);
            }

            Assert.IsTrue(source.UsingGamepad, "The second pass checked the pad labels.");
            Assert.AreEqual("D\u2193", source.GetAbilityKeyLabel(3), "D-pad down reads as an arrow.");
        }

        [Test]
        public void AbilityBar_RedrawsKeyLabels_WhenThePadIsPickedUp()
        {
            var player = new GameObject("HudPlayer");
            var bar = new GameObject("AbilityBar");
            try
            {
                player.AddComponent<Rigidbody2D>();
                var actor = player.AddComponent<PlayerActor>();
                var loadout = player.AddComponent<AbilityLoadout>();
                var ability = player.AddComponent<ProbeAbility>();
                var playerInput = player.AddComponent<InputSystemSource>();

                var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
                definition.DisplayName = "Probe";
                SetField(ability, "definition", definition);
                SetField(playerInput, "actions", actions);
                var slots = new UnityEditor.SerializedObject(loadout);
                slots.FindProperty("slots").arraySize = 1;
                slots.FindProperty("slots").GetArrayElementAtIndex(0).objectReferenceValue = ability;
                slots.ApplyModifiedPropertiesWithoutUndo();
                actor.Initialize();

                // A slot template with just a key label.
                var template = new GameObject("SlotTemplate");
                template.transform.SetParent(bar.transform, false);
                var slotView = template.AddComponent<DungeonSong.UI.AbilitySlotView>();
                var keyGo = new GameObject("Key");
                keyGo.transform.SetParent(template.transform, false);
                SetField(slotView, "keyText", keyGo.AddComponent<UnityEngine.UI.Text>());
                var view = bar.AddComponent<DungeonSong.UI.AbilityBarView>();
                SetField(view, "slotTemplate", slotView);

                typeof(DungeonSong.UI.HudView)
                    .GetMethod("Bind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { actor });

                UnityEngine.UI.Text Shown() => bar.transform.Find("AbilitySlot_0/Key").GetComponent<UnityEngine.UI.Text>();
                Assert.AreEqual("Q", Shown().text);

                Press(pad.rightTrigger);

                Assert.AreEqual(playerInput.GetAbilityKeyLabel(0), Shown().text, "The bar shows the pad button once the pad is used.");
                Assert.AreNotEqual("Q", Shown().text);
            }
            finally
            {
                Object.DestroyImmediate(bar);
                Object.DestroyImmediate(player);
            }
        }

        private static void SetField(Object target, string field, Object value)
        {
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void HitboxToggle_IsF1()
        {
            InputAction toggle = actions.FindAction("Debug/ToggleHitboxes", throwIfNotFound: true);
            Press(keyboard.f1Key);
            Assert.IsTrue(toggle.WasPressedThisFrame());
        }
    }

    /// <summary>The keyboard ramp that used to come from Input.GetAxis("Horizontal").</summary>
    public class InputAxisSmoothingTests
    {
        private static float Hold(float start, float target, float seconds, float step = 1f / 60f)
        {
            float value = start;
            for (float t = 0f; t < seconds - 0.0001f; t += step)
            {
                value = InputAxisSmoothing.Step(value, target, step);
            }

            return value;
        }

        [Test]
        public void Pressing_RampsToFullInAThirdOfASecond()
        {
            Assert.AreEqual(0.5f, Hold(0f, 1f, 1f / 6f), 0.02f, "Halfway after a sixth of a second.");
            Assert.AreEqual(1f, Hold(0f, 1f, 1f / 3f), 0.001f);
            Assert.AreEqual(1f, Hold(0f, 1f, 1f), 0.001f, "Never overshoots.");
        }

        [Test]
        public void Releasing_EasesBackToZero()
        {
            Assert.AreEqual(0.5f, Hold(1f, 0f, 1f / 6f), 0.02f);
            Assert.AreEqual(0f, Hold(1f, 0f, 1f / 3f), 0.001f);
        }

        [Test]
        public void Reversing_SnapsThroughZero()
        {
            float value = InputAxisSmoothing.Step(1f, -1f, 1f / 60f);
            Assert.AreEqual(-InputAxisSmoothing.DefaultSensitivity / 60f, value, 0.0001f, "Turning around starts from zero, not from full speed the other way.");
        }
    }

    /// <summary>Keeps the migration finished: nothing may go back to the legacy Input Manager.</summary>
    public class NoLegacyInputTests
    {
        private static readonly Regex LegacyCall = new Regex(@"(?<![\w.])(UnityEngine\.)?Input\.(Get\w+|anyKey\w*|mousePosition|touch\w*|inputString)\b");

        [Test]
        public void GameplayScripts_DoNotUseTheLegacyInputApi()
        {
            var offenders = new System.Text.StringBuilder();
            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i].Split(new[] { "//" }, System.StringSplitOptions.None)[0];
                    if (LegacyCall.IsMatch(code))
                    {
                        offenders.AppendLine($"{path}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }

            Assert.IsEmpty(offenders.ToString(), "Read input through IPlayerInputSource / the actions asset instead:\n" + offenders);
        }
    }
}
