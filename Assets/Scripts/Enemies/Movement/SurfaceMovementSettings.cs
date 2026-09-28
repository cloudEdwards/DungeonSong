using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Tuning for enemies that crawl along floors, walls and ceilings.
    /// <para>
    /// Kept in its own file so Unity can bind a MonoScript to it; see
    /// <see cref="GroundMovementSettings"/> for why that matters.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "SurfaceMovementSettings", menuName = "Dungeon/Enemies/Movement/Surface")]
    public class SurfaceMovementSettings : MovementSettings
    {
        [Header("Surface Adhesion")]
        [Min(0.01f), Tooltip("How far below itself the crawler looks for its surface.")]
        public float StickDistance = 0.6f;

        [Min(0f), Tooltip("Gap kept between the crawler's origin and the surface.")]
        public float SurfaceOffset = 0.25f;

        [Min(0f), Tooltip("Degrees/second the crawler rotates to match a new surface angle.")]
        public float AlignSpeed = 540f;

        [Header("Corners")]
        [Min(0.01f), Tooltip("How far ahead it looks for a wall to climb.")]
        public float ForwardProbeDistance = 0.4f;

        [Tooltip("Fall when no surface is found rather than searching around the corner.")]
        public bool FallWhenDetached;
    }
}
