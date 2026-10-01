using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Draws attack hitboxes in the Game view while they are live, for tuning attacks that
    /// have no animation of their own yet (the up and down slashes).
    /// <para>
    /// Toggle with the <c>Debug/ToggleHitboxes</c> action (F1) during play. In the Editor,
    /// Dungeon ▸ Debug ▸ Show Hitboxes sets whether it starts on. Off, it costs nothing:
    /// outlines are only created the first time a hitbox opens while it is on.
    /// </para>
    /// </summary>
    public static class HitboxDebug
    {
#if UNITY_EDITOR
        /// <summary>EditorPrefs key for Dungeon ▸ Debug ▸ Show Hitboxes.</summary>
        public const string ShowOnPlayPrefKey = "DungeonSong.ShowHitboxes";
#endif

        private const int CircleSegments = 24;

        private static bool visible;

        /// <summary>Action, in the project-wide actions asset, that toggles the outlines.</summary>
        public const string ToggleActionPath = "Debug/ToggleHitboxes";

        /// <summary>The toggle's key as the player would read it, e.g. "F1".</summary>
        public static string ToggleLabel
        {
            get
            {
                InputAction action = FindToggleAction();
                return action != null ? action.GetBindingDisplayString() : "(unbound)";
            }
        }

        /// <summary>The toggle action, or null when the project's actions don't define it.</summary>
        public static InputAction FindToggleAction()
        {
            return InputSystem.actions != null ? InputSystem.actions.FindAction(ToggleActionPath) : null;
        }

        /// <summary>Seconds an outline stays up after its hitbox closes, so short swings are readable.</summary>
        public static float LingerSeconds { get; set; } = 0.2f;

        /// <summary>Raised when outlines are switched on or off.</summary>
        public static event System.Action<bool> VisibilityChanged;

        public static bool Visible
        {
            get => visible;
            set
            {
                if (visible == value)
                {
                    return;
                }

                visible = value;
                VisibilityChanged?.Invoke(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlay()
        {
            VisibilityChanged = null;
#if UNITY_EDITOR
            visible = UnityEditor.EditorPrefs.GetBool(ShowOnPlayPrefKey, false);
#else
            visible = false;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateToggle()
        {
            var go = new GameObject("HitboxDebugToggle") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<HitboxDebugToggle>();
        }

        /// <summary>Called by a hitbox as it opens. Shows its outline when outlines are on.</summary>
        public static void NotifyActivated(Hitbox hitbox)
        {
            if (!visible || hitbox == null || !Application.isPlaying)
            {
                return;
            }

            HitboxOutline.For(hitbox).Show();
        }

        /// <summary>
        /// The collider's outline in the collider's own local space, ready for a non-world-space
        /// line renderer parented to the collider. Supports box, circle, capsule and polygon
        /// shapes; anything else falls back to its bounds.
        /// </summary>
        public static Vector3[] GetOutline(Collider2D collider)
        {
            var points = new List<Vector3>(CircleSegments);

            switch (collider)
            {
                case BoxCollider2D box:
                    AddBox(points, box.offset, box.size);
                    break;

                case CircleCollider2D circle:
                    AddEllipse(points, circle.offset, new Vector2(circle.radius, circle.radius));
                    break;

                case CapsuleCollider2D capsule:
                    AddEllipse(points, capsule.offset, capsule.size * 0.5f);
                    break;

                case PolygonCollider2D polygon when polygon.pathCount > 0:
                    foreach (Vector2 p in polygon.GetPath(0))
                    {
                        points.Add(p + polygon.offset);
                    }

                    break;

                default:
                    Bounds b = collider.bounds;
                    Transform t = collider.transform;
                    Vector3 min = t.InverseTransformPoint(b.min);
                    Vector3 max = t.InverseTransformPoint(b.max);
                    AddBox(points, (min + max) * 0.5f, max - min);
                    break;
            }

            return points.ToArray();
        }

        private static void AddBox(List<Vector3> points, Vector2 center, Vector2 size)
        {
            Vector2 h = size * 0.5f;
            points.Add(center + new Vector2(-h.x, -h.y));
            points.Add(center + new Vector2(-h.x, h.y));
            points.Add(center + new Vector2(h.x, h.y));
            points.Add(center + new Vector2(h.x, -h.y));
        }

        private static void AddEllipse(List<Vector3> points, Vector2 center, Vector2 radii)
        {
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / CircleSegments;
                points.Add(center + new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y));
            }
        }
    }

    /// <summary>Watches the toggle action. Debug tooling, so it reads its action directly.</summary>
    internal sealed class HitboxDebugToggle : MonoBehaviour
    {
        private InputAction toggle;

        private void OnEnable()
        {
            toggle = HitboxDebug.FindToggleAction();
            toggle?.Enable();
        }

        private void Update()
        {
            if (toggle != null && toggle.WasPressedThisFrame())
            {
                HitboxDebug.Visible = !HitboxDebug.Visible;
                Debug.Log($"Hitbox outlines {(HitboxDebug.Visible ? "on" : "off")} ({HitboxDebug.ToggleLabel}).");
            }
        }
    }
}
