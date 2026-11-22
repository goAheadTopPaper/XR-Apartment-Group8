using System;
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2;
using XRApartment.Simulation.ReviewV2;

namespace XRApartment.Runtime.ReviewV2.UI
{
    /// <summary>Small T2 boundary over the existing V2 host; it does not own session state.</summary>
    public interface IReviewV2UiSession
    {
        SessionSnapshot Snapshot { get; }
        event Action<SessionSnapshot> Changed;
        event Action<OperationResult> OperationCompleted;
        CommandResult Execute(CommandType command);
    }

    public sealed class ReviewV2UiSessionAdapter : UnityEngine.MonoBehaviour, IReviewV2UiSession
    {
        [UnityEngine.SerializeField] ReviewSessionHost host;
        [UnityEngine.SerializeField] bool useMockMode;
        MockReviewProvider mock;

        public SessionSnapshot Snapshot => host != null ? host.Snapshot : mock != null ? mock.Snapshot : null;
        public event Action<SessionSnapshot> Changed;
        public event Action<OperationResult> OperationCompleted;

        void OnEnable()
        {
            if (useMockMode && host == null && mock == null) mock = new MockReviewProvider();
            AttachAndPublish();
        }
        void OnDisable() => Detach();

        public void Bind(ReviewSessionHost sessionHost)
        {
            if (host == sessionHost && mock == null) return;
            Detach();
            mock = null;
            host = sessionHost;
            if (isActiveAndEnabled) AttachAndPublish();
        }

        /// <summary>Explicit development-only session. Its snapshot remains marked IsMock.</summary>
        public void BindMock()
        {
            if (host == null && mock != null) return;
            Detach();
            host = null;
            mock = new MockReviewProvider();
            if (isActiveAndEnabled) AttachAndPublish();
        }

        public CommandResult Execute(CommandType command)
        {
            if (host != null) return host.Execute(command);
            if (mock == null) return new CommandResult(false, "ReviewSessionHost is not assigned");
            if (command == CommandType.CalibrateSeated || command == CommandType.CalibrateStanding)
                return new CommandResult(true, "Mock posture choice only; no XR calibration was performed");
            return mock.Execute(command);
        }

        void AttachAndPublish()
        {
            if (host != null)
            {
                host.Changed += ForwardChanged;
                host.OperationCompleted += ForwardOperation;
            }
            else if (mock != null) mock.Changed += ForwardChanged;

            var snapshot = Snapshot;
            if (snapshot != null) ForwardChanged(snapshot);
        }

        void Detach()
        {
            if (host != null)
            {
                host.Changed -= ForwardChanged;
                host.OperationCompleted -= ForwardOperation;
            }
            if (mock != null) mock.Changed -= ForwardChanged;
        }

        void ForwardChanged(SessionSnapshot snapshot) => Changed?.Invoke(snapshot);
        void ForwardOperation(OperationResult result) => OperationCompleted?.Invoke(result);
    }
}
