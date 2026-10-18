using UnityEngine;
using UnityEngine.SceneManagement;
using XRApartment.Data;
using XRApartment.Runtime.Core;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Debugging
{
    /// <summary>Temporary Play-mode controls; no button can fabricate task evidence.</summary>
    public sealed class JourneyDebugUI : MonoBehaviour, IJourneyView
    {
        [SerializeField] private JourneyRuntimeHost host;
        [SerializeField] private bool feedbackSubmittedByDedicatedView;
        private JourneySnapshot snapshot;
        private JourneyErrorCode lastErrorCode;
        private string message;
        private string explanation = "Door clearance affects the daily task.";
        private string skipReason = "";
        private string tradeOff = "B2 leaves more circulation space.";
        private Vector2 scrollPosition;
        private int chosenDecision = 1;
        private int chosenCategory = 1;
        private int chosenImportance = 2;

        public bool IsMenuExpanded { get; private set; } = true;
        public string LastMessage => message;

        public string CurrentTaskSummary
        {
            get
            {
                if (snapshot == null) return host == null
                    ? "Journey host is missing." : host.StartupError ?? "Waiting for journey host.";
                var benchmark = snapshot.ActiveBenchmarkId ?? snapshot.DisplayedBenchmarkId;
                var title = string.IsNullOrEmpty(benchmark)
                    ? snapshot.State.ToString() : snapshot.State + " | " + benchmark;
                if (snapshot.State == SessionState.ChallengeActive && snapshot.CurrentStep.HasValue)
                {
                    title += " | Step " + ((int)snapshot.CurrentStep.Value + 1) + "/8";
                    if (snapshot.TotalItemCount > 0)
                        title += " | Items " + snapshot.CompletedItemCount + "/" + snapshot.TotalItemCount;
                }
                return title + "\n" + Instruction(snapshot);
            }
        }

        public void Render(JourneySnapshot current, JourneyErrorCode errorCode, string detail)
        {
            snapshot = current;
            lastErrorCode = errorCode;
            message = errorCode == JourneyErrorCode.None ? detail : errorCode + ": " + detail;
        }

        private void OnGUI()
        {
            if (host == null) return;
            if (GUI.Button(new Rect(12, 12, 124, 28),
                IsMenuExpanded ? "Hide menu" : "Open menu")) ToggleMenu();
            if (!IsMenuExpanded)
            {
                DrawCompactStatus();
                return;
            }
            GUILayout.BeginArea(new Rect(12, 48, Mathf.Min(480, Screen.width - 24),
                Mathf.Min(720, Screen.height - 60)), GUI.skin.box);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            if (snapshot == null)
            {
                GUILayout.Label(host.StartupError ?? "Waiting for journey host.");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label("CORE PLAY TEST (temporary controls)");
            GUILayout.Label($"{snapshot.State} | {snapshot.ActiveBenchmarkId ?? snapshot.DisplayedBenchmarkId}");
            GUILayout.Label($"Step: {snapshot.CurrentStep}  Items: {snapshot.CompletedItemCount}/{snapshot.TotalItemCount}");
            GUILayout.Label($"Save: {snapshot.Save}  Outcome: {snapshot.Outcome}");
            GUILayout.Label(Instruction(snapshot));
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);
            if (snapshot.CurrentSafety != null) GUILayout.Label("RED: " + snapshot.CurrentSafety.Message);
            foreach (var conflict in snapshot.Conflicts)
                GUILayout.Label((conflict.FeedbackRecorded ? "AMBER: " : "YELLOW: ") + conflict.Message);

            if (snapshot.State == SessionState.Setup && GUILayout.Button("Complete setup"))
                Send(JourneyCommandType.CompleteSetup, prerequisites: true);
            if (snapshot.State == SessionState.Orientation && GUILayout.Button("Acknowledge orientation"))
                Send(JourneyCommandType.AcknowledgeOrientation, prerequisites: true);
            if (snapshot.State == SessionState.Explore)
            {
                GUILayout.Label("Show previews a design. Start begins its 8-step task. Finish B1 feedback and save before starting B2.");
                foreach (var id in snapshot.BenchmarkOrder)
                {
                    if (GUILayout.Button("Show " + id)) Send(JourneyCommandType.ShowBenchmark, id);
                    if (snapshot.BenchmarkOutcomes[id] == TaskOutcome.Unset &&
                        GUILayout.Button("Start " + id)) Send(JourneyCommandType.StartChallenge, id);
                }
            }
            if (snapshot.State == SessionState.ChallengeActive)
            {
                if (snapshot.CurrentStep == ChallengeStep.Reorganise &&
                    GUILayout.Button("Confirm physical reorganisation"))
                    Send(JourneyCommandType.ConfirmReorganisation);
                GUILayout.Label("Restore current step resets only this step. Release task items first. Reset task restarts at step 1; Skip ends the task.");
                if (GUILayout.Button("Restore current step to default")) Send(JourneyCommandType.RestoreCurrentStep);
                GUILayout.Label("Skip reason (only if you choose Skip challenge):");
                skipReason = GUILayout.TextField(skipReason);
                if (GUILayout.Button("Skip challenge (then submit feedback)"))
                    host.Submit(new JourneyCommand
                    { Type = JourneyCommandType.SkipChallenge, Reason = skipReason });
                if (GUILayout.Button("Reset current task to step 1")) Send(JourneyCommandType.ResetCurrentTask);
            }
            if (snapshot.State == SessionState.FeedbackPending)
            {
                if (feedbackSubmittedByDedicatedView)
                {
                    GUILayout.Label("Use the dedicated XR feedback panel to review and submit this benchmark.");
                }
                else
                {
                var skipped = snapshot.ActiveBenchmarkId != null &&
                    snapshot.BenchmarkOutcomes.TryGetValue(snapshot.ActiveBenchmarkId, out var outcome) &&
                    outcome == TaskOutcome.Skipped;
                GUILayout.Label(skipped
                    ? "Task skipped. Its skip reason was recorded when you clicked Skip; submit this feedback to continue. Ratings are optional."
                    : "Task completed. Select decision, category and importance; enter a short explanation; then submit to save and continue.");
                chosenDecision = GUILayout.SelectionGrid(chosenDecision,
                    new[] { "Unset", "Accept", "Revise", "Reject" }, 4);
                chosenCategory = GUILayout.SelectionGrid(chosenCategory,
                    new[] { "Unset", "Door", "Circulation", "Storage", "Space", "Other" }, 3);
                chosenImportance = GUILayout.SelectionGrid(chosenImportance,
                    new[] { "Unset", "Low", "Medium", "High" }, 4);
                GUILayout.Label("Short explanation:");
                explanation = GUILayout.TextField(explanation);
                if (GUILayout.Button("Validate and submit feedback"))
                    host.SubmitFeedback(new FeedbackDraft
                    {
                        Decision = (CombinationDecision)chosenDecision,
                        Category = (FeedbackCategory)chosenCategory,
                        Importance = (Importance)chosenImportance,
                        ShortExplanation = explanation,
                        SkipReason = skipReason
                    });
                }
            }
            if (snapshot.State == SessionState.ComparisonSummary)
            {
                GUILayout.Label("Both benchmark records are saved. Enter the trade-off and choose a completed design.");
                GUILayout.Label("Final trade-off:");
                tradeOff = GUILayout.TextField(tradeOff);
                foreach (var id in snapshot.BenchmarkOrder)
                    if (snapshot.BenchmarkOutcomes[id] == TaskOutcome.Completed &&
                        GUILayout.Button("Prefer " + id))
                        host.Submit(new JourneyCommand
                        {
                            Type = JourneyCommandType.ConfirmComparison,
                            Comparison = new FinalComparison
                            { PreferredBenchmarkId = id, TradeOffSummary = tradeOff }
                        });
                if (GUILayout.Button("Finish with no preference (all skipped)"))
                    host.Submit(new JourneyCommand
                    { Type = JourneyCommandType.ConfirmComparison, Comparison = new FinalComparison() });
            }
            if (snapshot.State != SessionState.Finished && snapshot.State != SessionState.Exited)
            {
                if (snapshot.State == SessionState.Paused)
                {
                    if (GUILayout.Button("Resume")) Send(JourneyCommandType.Resume);
                }
                else if (GUILayout.Button("Pause")) Send(JourneyCommandType.Pause);
                if ((snapshot.Save == SaveStatus.Failed || lastErrorCode == JourneyErrorCode.OperationFailed) &&
                    GUILayout.Button("Retry failed operation"))
                    Send(JourneyCommandType.Retry);
                if (GUILayout.Button("Safe exit (abort this session)")) Send(JourneyCommandType.RequestExit);
            }
            else if (snapshot.Save == SaveStatus.Pending)
                GUILayout.Label("Waiting for the final save before a new test session can start.");
            else if (GUILayout.Button("Start NEW test session (reload scene)"))
                StartNewTestSession();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public void ToggleMenu() => IsMenuExpanded = !IsMenuExpanded;

        private void DrawCompactStatus()
        {
            var width = Mathf.Min(360, Screen.width - 24);
            var x = Screen.width - width - 12;
            var y = x < 148 ? 48 : 12;
            var extraHeight = snapshot == null ? 0 :
                (string.IsNullOrEmpty(message) ? 0 : 42) +
                (snapshot.CurrentSafety == null ? 0 : 42) +
                (snapshot.Conflicts.Count == 0 ? 0 : 24);
            GUILayout.BeginArea(new Rect(x, y, width,
                Mathf.Min(170 + extraHeight, Screen.height - y - 12)), GUI.skin.box);
            GUILayout.Label("CURRENT TASK");
            GUILayout.Label(CurrentTaskSummary);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);
            if (snapshot != null)
            {
                if (snapshot.Save == SaveStatus.Pending || snapshot.Save == SaveStatus.Failed)
                    GUILayout.Label("Save: " + snapshot.Save + " (open menu for details)");
                if (snapshot.CurrentSafety != null)
                    GUILayout.Label("RED: " + snapshot.CurrentSafety.Message);
                if (snapshot.Conflicts.Count > 0)
                    GUILayout.Label(snapshot.Conflicts.Count + " design notice(s); open menu for details.");
            }
            GUILayout.EndArea();
        }

        public void StartNewTestSession()
        {
            if (snapshot == null || snapshot.Save == SaveStatus.Pending ||
                (snapshot.State != SessionState.Finished && snapshot.State != SessionState.Exited)) return;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private static string Instruction(JourneySnapshot current)
        {
            switch (current.State)
            {
                case SessionState.Setup: return "Complete setup, then acknowledge orientation. These are test prerequisites.";
                case SessionState.Orientation: return "Acknowledge orientation to enter Explore.";
                case SessionState.Explore: return "Choose Show B1/B2 to preview, then Start the next unfinished task.";
                case SessionState.ChallengeActive: return StepInstruction(current.CurrentStep);
                case SessionState.FeedbackPending: return "The task is over; feedback and a verified save are required before the next design.";
                case SessionState.ComparisonSummary: return "Compare B1 and B2, then save the final preference.";
                case SessionState.Paused: return "Paused. Resume to return to the previous stage; this does not undo task steps.";
                case SessionState.Finished: return "Session finished. To repeat, start a new test session below.";
                case SessionState.Exited: return "Safe exit aborted this session. It cannot resume; start a new test session below.";
                default: return "";
            }
        }

        private static string StepInstruction(ChallengeStep? step)
        {
            switch (step)
            {
                case ChallengeStep.Approach: return "1/8 Approach: move your headset into the GREEN zone.";
                case ChallengeStep.Open: return "2/8 Open: fully open BOTH upper Freezer and lower Fresh doors. Task items are locked until both doors are open.";
                case ChallengeStep.TakeOut: return "3/8 Take out: remove TOMATO and ONION from the upper FREEZER.";
                case ChallengeStep.TemporaryPlacement: return "4/8 Temporary placement: release TOMATO and ONION on the PURPLE counter highlight.";
                case ChallengeStep.Reorganise: return "5/8 Reorganise: move and release TOMATO or ONION from PURPLE onto ANY lower Fresh shelf; wait briefly, then confirm. CEREAL BOX counts in step 6.";
                case ChallengeStep.PutBack: return "6/8 Put back: release all THREE items into ANY lower Fresh shelf. All three are valid.";
                case ChallengeStep.Close: return "7/8 Close: fully close BOTH Fresh and Freezer doors.";
                case ChallengeStep.Leave: return "8/8 Leave: exit GREEN, then move your headset into the BLUE zone.";
                default: return "Wait for the current scene action to complete.";
            }
        }

        private void Send(JourneyCommandType type, string benchmarkId = null, bool prerequisites = false)
        {
            host.Submit(new JourneyCommand
            { Type = type, BenchmarkId = benchmarkId, PrerequisitesMet = prerequisites });
        }
    }
}
