using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Finds the nearest usable <see cref="IInteractable"/> and triggers it on input.
    /// <para>
    /// Interactables register themselves as they come into range via trigger colliders, so
    /// this never runs a physics query or a Find. With a handful of candidates the nearest
    /// check is trivial.
    /// </para>
    /// </summary>
    public class PlayerInteractor : PlayerModule
    {
        [Header("Range")]
        [SerializeField, Min(0f), Tooltip("Maximum distance at which an interactable can be used.")]
        private float interactionRange = 2f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        /// <summary>Raised when the available interactable changes, for prompt UI. May be null.</summary>
        public event Action<IInteractable> AvailableChanged;

        private readonly List<IInteractable> candidates = new List<IInteractable>(4);
        private PlayerInputRouter input;
        private IInteractable available;

        public override int TickOrder => PlayerTickOrder.Interaction;

        /// <summary>The interactable the player would use right now, or null.</summary>
        public IInteractable Available => available;

        protected override void OnBind() => input = GetComponent<PlayerInputRouter>();

        /// <summary>Called by an interactable when the player comes into range.</summary>
        public void Register(IInteractable interactable)
        {
            if (interactable != null && !candidates.Contains(interactable))
            {
                candidates.Add(interactable);
            }
        }

        /// <summary>Called by an interactable when the player leaves range.</summary>
        public void Unregister(IInteractable interactable)
        {
            candidates.Remove(interactable);
        }

        public override void Tick(float deltaTime)
        {
            IInteractable nearest = FindNearest();

            if (!ReferenceEquals(nearest, available))
            {
                available = nearest;
                AvailableChanged?.Invoke(available);
            }

            if (available == null || input == null || input.Source == null)
            {
                return;
            }

            if (input.Source.InteractPressed)
            {
                available.Interact(Owner);
            }
        }

        private IInteractable FindNearest()
        {
            Vector2 origin = transform.position;
            float bestSqr = interactionRange * interactionRange;
            IInteractable best = null;

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                IInteractable candidate = candidates[i];

                if (candidate == null || candidate.Transform == null)
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                if (!candidate.CanInteract(Owner))
                {
                    continue;
                }

                float sqr = ((Vector2)candidate.Transform.position - origin).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
