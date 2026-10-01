using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>The outline shapes the hitbox visualizer draws.</summary>
    public class HitboxDebugTests
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

        [Test]
        public void BoxOutline_IsItsFourCorners_IncludingOffset()
        {
            go = new GameObject("Box");
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(2f, 1f);
            box.offset = new Vector2(1f, 0.5f);

            Vector3[] outline = HitboxDebug.GetOutline(box);

            CollectionAssert.AreEquivalent(
                new[] { new Vector3(0f, 0f), new Vector3(0f, 1f), new Vector3(2f, 1f), new Vector3(2f, 0f) },
                outline);
        }

        [Test]
        public void CircleOutline_SitsOnTheRadius()
        {
            go = new GameObject("Circle");
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.75f;
            circle.offset = new Vector2(0f, 1f);

            Vector3[] outline = HitboxDebug.GetOutline(circle);

            Assert.Greater(outline.Length, 8);
            foreach (Vector3 p in outline)
            {
                Assert.AreEqual(0.75f, Vector2.Distance(p, new Vector2(0f, 1f)), 0.001f);
            }
        }

        [Test]
        public void PolygonOutline_FollowsItsPath()
        {
            go = new GameObject("Polygon");
            var polygon = go.AddComponent<PolygonCollider2D>();
            Vector2[] cone = { new Vector2(0f, -0.3f), new Vector2(2f, -0.8f), new Vector2(2f, 0.8f), new Vector2(0f, 0.3f) };
            polygon.points = cone;

            Vector3[] outline = HitboxDebug.GetOutline(polygon);

            Assert.AreEqual(cone.Length, outline.Length);
            for (int i = 0; i < cone.Length; i++)
            {
                Assert.AreEqual((Vector3)cone[i], outline[i]);
            }
        }
    }
}
