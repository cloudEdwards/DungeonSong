using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Combat.Projectiles;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>Minimal damageable, so these tests need no enemy wiring.</summary>
    internal class StubDamageable : MonoBehaviour, IDamageable
    {
        public DamageTeam Team => DamageTeam.Player;

        public bool IsAlive => true;

        public Transform Transform => transform;

        public DamageResult TakeDamage(in DamageInfo info) => default;
    }

    /// <summary>
    /// Regression tests for what stops a projectile.
    /// <para>
    /// The bug these exist for: a projectile despawned on any collider whose layer was in
    /// its terrain mask, including trigger colliders. The player's ground and wall sensors
    /// are triggers sitting on the Default layer, so a shot aimed at an airborne player
    /// hit a sensor first, despawned, and dealt no damage. It reproduced reliably by
    /// jumping — grounded, the sensor is buried in the floor and the shot got through.
    /// </para>
    /// </summary>
    public class ProjectileTests
    {
        private const int TerrainLayer = 8;
        private const int OtherLayer = 11;

        private GameObject projectileObject;
        private GameObject obstacle;

        [TearDown]
        public void TearDown()
        {
            if (projectileObject != null) Object.DestroyImmediate(projectileObject);
            if (obstacle != null) Object.DestroyImmediate(obstacle);
        }

        private Projectile CreateLaunchedProjectile()
        {
            projectileObject = new GameObject("Projectile");
            projectileObject.AddComponent<Rigidbody2D>();

            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;

            projectileObject.AddComponent<Hitbox>();
            Projectile projectile = projectileObject.AddComponent<Projectile>();

            var definition = ScriptableObject.CreateInstance<ProjectileDefinition>();
            definition.Speed = 5f;
            definition.Lifetime = 3f;
            definition.DespawnOnTerrain = true;

            // A projectile only evaluates impacts once it is in flight.
            projectile.Launch(definition, Vector2.right, DamageTeam.Enemy, DamageTeam.Player, null);
            return projectile;
        }

        private Collider2D CreateObstacle(int layer, bool isTrigger, bool damageable = false)
        {
            obstacle = new GameObject("Obstacle") { layer = layer };
            BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
            collider.isTrigger = isTrigger;

            if (damageable)
            {
                obstacle.AddComponent<StubDamageable>();
            }

            return collider;
        }

        [Test]
        public void TriggerOnTerrainLayer_DoesNotStopTheProjectile()
        {
            // The actual reported bug: a character sensor is a trigger on a terrain layer.
            Projectile projectile = CreateLaunchedProjectile();
            Collider2D sensor = CreateObstacle(TerrainLayer, isTrigger: true);

            Assert.IsFalse(projectile.ShouldDespawnOn(sensor),
                "A trigger is not solid geometry; the projectile must pass through it and reach the hurtbox.");
        }

        [Test]
        public void SolidColliderOnTerrainLayer_StopsTheProjectile()
        {
            Projectile projectile = CreateLaunchedProjectile();
            Collider2D wall = CreateObstacle(TerrainLayer, isTrigger: false);

            Assert.IsTrue(projectile.ShouldDespawnOn(wall), "Real terrain must still stop a projectile.");
        }

        [Test]
        public void SolidColliderOffTerrainLayer_DoesNotStopTheProjectile()
        {
            Projectile projectile = CreateLaunchedProjectile();
            Collider2D somethingElse = CreateObstacle(OtherLayer, isTrigger: false);

            Assert.IsFalse(projectile.ShouldDespawnOn(somethingElse));
        }

        [Test]
        public void DamageableCollider_DoesNotStopTheProjectile()
        {
            // Even a solid collider on a terrain layer must not end the flight if it
            // belongs to something damageable: that hit is the hitbox's to resolve, and
            // trigger callback order within a physics step is not guaranteed.
            Projectile projectile = CreateLaunchedProjectile();
            Collider2D victim = CreateObstacle(TerrainLayer, isTrigger: false, damageable: true);

            Assert.IsFalse(projectile.ShouldDespawnOn(victim));
        }

        [Test]
        public void NullCollider_IsHandled()
        {
            Projectile projectile = CreateLaunchedProjectile();

            Assert.IsFalse(projectile.ShouldDespawnOn(null));
        }
    }
}
