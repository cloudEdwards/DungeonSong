using UnityEngine;
using UnityEngine.UI;
using DungeonSong.Player;

namespace DungeonSong.UI
{
    /// <summary>
    /// Shows "Press F to take a Long Rest" when the player is near a campfire, and the
    /// equivalent for anything else they can interact with.
    /// <para>
    /// The verb comes from the interactable itself and the key from the input source, so a
    /// new interactable gets a correct prompt for free, and rebinding the key changes the
    /// prompt without anyone remembering to update a string.
    /// </para>
    /// </summary>
    public class InteractionPromptView : HudView
    {
        [Header("Widgets")]
        [SerializeField, Tooltip("Text showing the prompt. Hidden when there is nothing to interact with.")]
        private Text promptText;

        [SerializeField, Tooltip("Optional container shown and hidden with the prompt, for a background panel.")]
        private GameObject container;

        [Header("Format")]
        [SerializeField, Tooltip("{0} is the key, {1} is the interactable's verb.")]
        private string format = "Press {0} to {1}";

        [Header("Animation")]
        [SerializeField, Tooltip("Fade the prompt in and out instead of snapping.")]
        private bool fade = true;

        [SerializeField, Min(0f)] private float fadeDuration = 0.15f;

        private PlayerInteractor interactor;
        private IPlayerInputSource input;
        private CanvasGroup canvasGroup;
        private IInteractable currentTarget;
        private float alpha;

        protected override void OnBind()
        {
            interactor = Player.GetModule<PlayerInteractor>();

            if (container != null)
            {
                canvasGroup = container.GetComponent<CanvasGroup>();
                if (canvasGroup == null && fade)
                {
                    canvasGroup = container.AddComponent<CanvasGroup>();
                }
            }

            if (interactor == null)
            {
                Hide();
                return;
            }

            interactor.AvailableChanged += OnAvailableChanged;
            OnAvailableChanged(interactor.Available);

            input = Player.GetComponent<IPlayerInputSource>();
            if (input != null)
            {
                input.ControlsChanged += OnControlsChanged;
            }
        }

        // Re-draws the prompt with the other device's key while it is on screen.
        private void OnControlsChanged()
        {
            if (currentTarget != null)
            {
                OnAvailableChanged(currentTarget);
            }
        }

        protected override void OnUnbind()
        {
            if (interactor != null)
            {
                interactor.AvailableChanged -= OnAvailableChanged;
                interactor = null;
            }

            if (input != null)
            {
                input.ControlsChanged -= OnControlsChanged;
                input = null;
            }
        }

        private void OnAvailableChanged(IInteractable available)
        {
            currentTarget = available;

            if (available == null)
            {
                Hide();
                return;
            }

            if (promptText != null)
            {
                promptText.text = string.Format(format, ResolveKeyLabel(), available.InteractionPrompt);
            }

            Show();
        }

        private string ResolveKeyLabel()
        {
            string label = (input ?? Player.GetComponent<IPlayerInputSource>())?.InteractKeyLabel;
            return string.IsNullOrEmpty(label) ? "F" : label;
        }

        private void Show()
        {
            if (container != null)
            {
                container.SetActive(true);
            }
            else if (promptText != null)
            {
                promptText.enabled = true;
            }
        }

        private void Hide()
        {
            if (!fade || canvasGroup == null)
            {
                if (container != null)
                {
                    container.SetActive(false);
                }
                else if (promptText != null)
                {
                    promptText.enabled = false;
                }

                alpha = 0f;
                return;
            }

            alpha = 0f;
            canvasGroup.alpha = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (!fade || canvasGroup == null)
            {
                return;
            }

            float target = currentTarget != null ? 1f : 0f;
            float step = fadeDuration > 0f ? deltaTime / fadeDuration : 1f;
            alpha = Mathf.MoveTowards(alpha, target, step);
            canvasGroup.alpha = alpha;

            if (container != null)
            {
                bool shouldBeActive = alpha > 0.001f;
                if (container.activeSelf != shouldBeActive)
                {
                    container.SetActive(shouldBeActive);
                }
            }
        }
    }
}
