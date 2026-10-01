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
    /// <summary>
    /// The Game-view hitbox outlines: on while a hitbox is live (plus a short linger),
    /// nothing at all while switched off.
    /// </summary>
    public class HitboxOutlineTests
    {
        private bool realVisible;
        private bool realRunInBackground;
        private GameObject go;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            realRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            realVisible = HitboxDebug.Visible;
            HitboxDebug.Visible = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (go != null)
            {
                Object.Destroy(go);
            }

            HitboxDebug.Visible = realVisible;
            Application.runInBackground = realRunInBackground;
            yield return null;
        }

        private Hitbox CreateHitbox()
        {
            go = new GameObject("TestHitbox");
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 0.5f);
            return go.AddComponent<Hitbox>();
        }

        private static HitboxOutline OutlineOf(Hitbox hitbox) => hitbox.GetComponentInChildren<HitboxOutline>();

        private static DamageInfo Hit() => DamageInfo.Create(1f, DamageTeam.Player, Vector2.zero);

        [UnityTest]
        public IEnumerator Visible_ShowsTheOutlineWhileActive_ThenItFades()
        {
            Hitbox hitbox = CreateHitbox();
            HitboxDebug.Visible = true;

            hitbox.Activate(Hit());
            yield return null;

            HitboxOutline outline = OutlineOf(hitbox);
            Assert.IsNotNull(outline, "Opening a hitbox with outlines on draws one.");
            Assert.IsTrue(outline.IsShowing);
            Assert.AreEqual(4, outline.GetComponent<LineRenderer>().positionCount, "A box draws as four corners.");

            hitbox.Deactivate();
            yield return null;
            Assert.IsTrue(outline.IsShowing, "It lingers briefly so a fast swing is readable.");

            yield return new WaitForSecondsRealtime(HitboxDebug.LingerSeconds + 0.1f);
            Assert.IsFalse(outline.IsShowing);
        }

        [UnityTest]
        public IEnumerator Hidden_DrawsNothing()
        {
            Hitbox hitbox = CreateHitbox();

            hitbox.Activate(Hit());
            yield return null;

            Assert.IsNull(OutlineOf(hitbox), "With outlines off, nothing is even created.");
        }

        [UnityTest]
        public IEnumerator TogglingOff_HidesOutlinesImmediately()
        {
            Hitbox hitbox = CreateHitbox();
            HitboxDebug.Visible = true;
            hitbox.Activate(Hit());
            yield return null;

            HitboxDebug.Visible = false;

            Assert.IsFalse(OutlineOf(hitbox).IsShowing);
        }

        [UnityTest]
        public IEnumerator PlayerUpAttack_ShowsTheUpHitbox()
        {
            // The case this tool exists for: the up slash has no animation of its own.
            ISaveService realSave = GameSave.Service;
            GameSave.Service = new InMemorySaveService();

            try
            {
                yield return SceneManager.LoadSceneAsync("Scene2");
                yield return null;

                PlayerActor player = Object.FindAnyObjectByType<PlayerActor>();
                HitboxDebug.Visible = true;

                Assert.IsTrue(player.Combat.TryAttack(AttackDirection.Up));
                yield return new WaitForSeconds(player.Combat.CurrentAttack.Startup + 0.02f);

                bool upShowing = false;
                foreach (HitboxOutline outline in player.GetComponentsInChildren<HitboxOutline>())
                {
                    upShowing |= outline.IsShowing && outline.transform.parent.GetComponent<Hitbox>().Key == "Up";
                }

                Assert.IsTrue(upShowing, "The Up hitbox is outlined while the up slash is live.");
            }
            finally
            {
                GameSave.Service = realSave;
            }
        }
    }
}
