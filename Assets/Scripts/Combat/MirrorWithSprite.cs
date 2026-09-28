using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Mirrors this object's local X position to match a <see cref="SpriteRenderer"/>'s
    /// flip state.
    /// <para>
    /// Flipping a sprite with <c>flipX</c> does not move child objects, so an attack hitbox
    /// parented to such an actor would stay on its right side while the actor faces left.
    /// This keeps the two in agreement without the actor's own movement code having to
    /// know that a hitbox exists.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MirrorWithSprite : MonoBehaviour
    {
        [SerializeField, Tooltip("Sprite whose flip state drives the mirroring. Leave empty to search parents.")]
        private SpriteRenderer source;

        [SerializeField, Tooltip("Also mirror local scale, for directional art on this object.")]
        private bool mirrorScale;

        private float baseLocalX;
        private float baseScaleX;

        private void Awake()
        {
            if (source == null)
            {
                source = GetComponentInParent<SpriteRenderer>();
            }

            baseLocalX = Mathf.Abs(transform.localPosition.x);
            baseScaleX = Mathf.Abs(transform.localScale.x);
        }

        private void LateUpdate()
        {
            if (source == null)
            {
                return;
            }

            float sign = source.flipX ? -1f : 1f;

            Vector3 position = transform.localPosition;
            float wanted = baseLocalX * sign;

            if (!Mathf.Approximately(position.x, wanted))
            {
                position.x = wanted;
                transform.localPosition = position;
            }

            if (!mirrorScale)
            {
                return;
            }

            Vector3 scale = transform.localScale;
            scale.x = baseScaleX * sign;
            transform.localScale = scale;
        }
    }
}
