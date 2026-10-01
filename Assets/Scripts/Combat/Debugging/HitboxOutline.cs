using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// The Game-view outline of one hitbox, drawn with a line renderer on a child object so
    /// it moves, flips and scales with the hitbox. Created on demand by <see cref="HitboxDebug"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitboxOutline : MonoBehaviour
    {
        private const string ChildName = "HitboxOutline";

        private static readonly Color PlayerColor = new Color(1f, 0.85f, 0.1f, 1f);
        private static readonly Color EnemyColor = new Color(1f, 0.2f, 0.25f, 1f);

        private static Material sharedMaterial;

        private Hitbox hitbox;
        private Collider2D shape;
        private LineRenderer line;
        private float lingerTimer;

        /// <summary>True while the outline is drawn.</summary>
        public bool IsShowing => line != null && line.enabled;

        /// <summary>The outline for a hitbox, created the first time it is needed.</summary>
        public static HitboxOutline For(Hitbox hitbox)
        {
            Transform child = hitbox.transform.Find(ChildName);
            if (child != null && child.TryGetComponent(out HitboxOutline existing))
            {
                return existing;
            }

            var go = new GameObject(ChildName);
            go.transform.SetParent(hitbox.transform, false);
            var outline = go.AddComponent<HitboxOutline>();
            outline.Build(hitbox);
            return outline;
        }

        private void Build(Hitbox target)
        {
            hitbox = target;
            shape = target.GetComponent<Collider2D>();

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.05f;
            line.numCapVertices = 0;
            line.sortingOrder = 1000;
            line.sharedMaterial = Material;
            line.enabled = false;
            Rebuild();

            HitboxDebug.VisibilityChanged += OnVisibilityChanged;
        }

        private void OnDestroy() => HitboxDebug.VisibilityChanged -= OnVisibilityChanged;

        private void OnVisibilityChanged(bool on)
        {
            if (!on)
            {
                Hide();
            }
        }

        /// <summary>Shows the outline for as long as the hitbox stays open, plus the linger.</summary>
        public void Show()
        {
            Rebuild();
            lingerTimer = HitboxDebug.LingerSeconds;
            SetAlpha(1f);
            line.enabled = true;
        }

        private void Hide()
        {
            lingerTimer = 0f;
            if (line != null)
            {
                line.enabled = false;
            }
        }

        private void Update()
        {
            if (!line.enabled)
            {
                return;
            }

            if (!HitboxDebug.Visible || hitbox == null)
            {
                Hide();
                return;
            }

            if (hitbox.IsActive)
            {
                lingerTimer = HitboxDebug.LingerSeconds;
                SetAlpha(1f);
                return;
            }

            lingerTimer -= Time.unscaledDeltaTime;
            if (lingerTimer <= 0f)
            {
                Hide();
                return;
            }

            SetAlpha(HitboxDebug.LingerSeconds > 0f ? lingerTimer / HitboxDebug.LingerSeconds : 0f);
        }

        // The shape can change between swings (authoring, mirroring offsets), so it is cheap
        // enough to rebuild on every show.
        private void Rebuild()
        {
            if (shape == null)
            {
                return;
            }

            Vector3[] points = HitboxDebug.GetOutline(shape);
            line.positionCount = points.Length;
            line.SetPositions(points);
        }

        private void SetAlpha(float alpha)
        {
            Color c = (hitbox.TargetTeams & DamageTeam.Enemy) != 0 ? PlayerColor : EnemyColor;
            c.a = alpha;
            line.startColor = c;
            line.endColor = c;
        }

        private static Material Material
        {
            get
            {
                if (sharedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                        ?? Shader.Find("Sprites/Default");
                    sharedMaterial = new Material(shader) { name = "HitboxOutline" };
                }

                return sharedMaterial;
            }
        }
    }
}
