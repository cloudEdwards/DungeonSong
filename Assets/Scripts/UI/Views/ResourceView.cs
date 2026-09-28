using UnityEngine;
using UnityEngine.UI;
using DungeonSong.Player;

namespace DungeonSong.UI
{
    /// <summary>
    /// Shows one resource pool: mana today, soul or faith later.
    /// <para>
    /// It displays whichever <see cref="ResourceDefinition"/> it is pointed at, so adding a
    /// second meter is duplicating this object and changing one reference.
    /// </para>
    /// </summary>
    public class ResourceView : HudView
    {
        [Header("Resource")]
        [SerializeField, Tooltip("Which resource to display. Leave empty to show the player's first one.")]
        private ResourceDefinition resource;

        [Header("Widgets")]
        [SerializeField] private Text valueText;

        [SerializeField, Tooltip("Optional fill image. Set its Image Type to Filled.")]
        private Image fillImage;

        [SerializeField] private Text labelText;

        [Header("Format")]
        [SerializeField, Tooltip("{0} is current, {1} is max.")]
        private string format = "{0:0}/{1:0}";

        private ResourcePool pool;

        protected override void OnBind()
        {
            pool = Player.Resources;
            if (pool == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (resource == null && pool.Resources.Count > 0)
            {
                resource = pool.Resources[0];
            }

            if (resource == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (labelText != null)
            {
                labelText.text = resource.DisplayName;
            }

            pool.ResourceChanged += OnResourceChanged;
            Refresh();
        }

        protected override void OnUnbind()
        {
            if (pool != null)
            {
                pool.ResourceChanged -= OnResourceChanged;
                pool = null;
            }
        }

        private void OnResourceChanged(ResourceDefinition changed, float current, float max)
        {
            if (changed == resource)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            float current = pool.GetAmount(resource);
            float max = pool.GetMax(resource);

            if (valueText != null)
            {
                valueText.text = string.Format(format, current, max);
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            }
        }
    }
}
