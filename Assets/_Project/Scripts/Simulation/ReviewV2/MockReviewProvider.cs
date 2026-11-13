using System;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Simulation.ReviewV2
{
    public sealed class MockReviewProvider : ISessionReadModel, IReviewSessionCommands, IInteractionEventSource, ISchemePort, IWorldPort
    {
        readonly SessionController core;
        long clock;
        public bool FailNextOperation { get; set; }
        public MockReviewProvider() { core = new SessionController(this, this, isMock: true); }
        public SessionSnapshot Snapshot => core.Snapshot;
        public event Action<SessionSnapshot> Changed { add => core.Changed += value; remove => core.Changed -= value; }
        public event Action<InteractionEvent> Observed;
        public CommandResult Execute(CommandType command, string targetSchemeId = null) => core.Execute(command, targetSchemeId);
        public bool SetGoal(string id, GoalState state) => core.ObserveGoal(Snapshot.SessionId, Snapshot.SchemeVisitId, id, state, "MOCK-" + Guid.NewGuid().ToString("N"), ++clock);
        public void EmitGrab() => Observed?.Invoke(new InteractionEvent(Snapshot, InteractionKind.ObjectGrabbed, "MOCK_TOMATO", ++clock));
        public bool Supports(string schemeId) => schemeId == "MOCK_A" || schemeId == "MOCK_B";
        public void Change(string requestId, string schemeId, Action<string, bool, string> completed) => Reset(requestId, completed);
        public void Reset(string requestId, Action<string, bool, string> completed)
        { bool success = !FailNextOperation; FailNextOperation = false; completed(requestId, success, "Mock world operation only"); }
        public CommandResult Recenter() => new CommandResult(false, "MockHasNoXR");
    }
}
