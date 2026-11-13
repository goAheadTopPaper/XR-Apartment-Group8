using UnityEngine;
using XRApartment.Data.ReviewV2;
using XRApartment.Simulation.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    /// Attach to a T2/T3 development scene; useMock works without any Cooking scene.
    public sealed class ReviewConsumerExample : MonoBehaviour
    {
        public bool useMock;
        public ReviewSessionHost host;
        ISessionReadModel model;
        IReviewSessionCommands commands;
        IInteractionEventSource events;
        public SessionSnapshot Current { get; private set; }
        public InteractionEvent LastInteraction { get; private set; }
        void OnEnable()
        {
            if (useMock)
            { var mock = new MockReviewProvider(); model = mock; commands = mock; events = mock; }
            else { model = host; commands = host; events = host; }
            model.Changed += Render; events.Observed += OnInteraction; Render(model.Snapshot);
        }
        void OnDisable() { model.Changed -= Render; events.Observed -= OnInteraction; }
        void Render(SessionSnapshot snapshot) => Current = snapshot;
        void OnInteraction(InteractionEvent fact) => LastInteraction = fact; // Context only, not saved feedback.
        public CommandResult RequestReview() => commands.Execute(CommandType.Review);
    }
}
