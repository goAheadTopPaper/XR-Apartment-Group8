using System;
using System.IO;
using System.Linq;
using UnityEngine;
using XRApartment.Data.ReviewV2;
namespace XRApartment.Runtime.ReviewV2
{
    public sealed class ReviewFeedbackSavePort : MonoBehaviour, ISessionSavePort
    {
        public SpatialFeedbackRecorder recorder;
        [Serializable] public sealed class GoalCheckpoint { public string id, evidenceId; public GoalState state; public bool available, hasUpdatedAt; public long updatedAtMilliseconds; }
        [Serializable] public sealed class Checkpoint
        {
            public string schemaVersion = "review-v2-session-1";
            public string requestId, sessionId, schemeId, schemeVisitId, modelVersion;
            public SessionPhase phase;
            public string[] schemeHistory;
            public GoalCheckpoint[] goals;
        }
        public static string CheckpointPath(string sessionId) => Path.Combine(LocalFeedbackStore.DefaultDirectory, "sessions", sessionId + ".json");
        public async void Save(string requestId, SessionSnapshot snapshot, Action<string, bool, string> completed)
        {
            bool success = false; string message;
            try
            {
                if (snapshot.IsMock) throw new InvalidOperationException("Mock sessions cannot be verified");
                if (recorder == null || !await recorder.RetrySaveAsync()) throw new IOException(recorder == null ? "Feedback recorder is not configured" : recorder.Error ?? "Feedback is unavailable");
                var checkpoint = new Checkpoint { requestId = requestId, sessionId = snapshot.SessionId, schemeId = snapshot.SchemeId,
                    schemeVisitId = snapshot.SchemeVisitId, modelVersion = snapshot.ModelVersion, phase = snapshot.Phase,
                    schemeHistory = snapshot.SchemeHistory.ToArray(), goals = snapshot.Goals.Select(g => new GoalCheckpoint { id = g.Id,
                    state = g.State, available = g.Available, evidenceId = g.EvidenceId, hasUpdatedAt = g.UpdatedAtMilliseconds.HasValue,
                    updatedAtMilliseconds = g.UpdatedAtMilliseconds ?? 0 }).ToArray() };
                var path = CheckpointPath(snapshot.SessionId);
                await LocalFeedbackStore.WriteAsync(() => LocalFeedbackStore.WriteVerified(path, JsonUtility.ToJson(checkpoint, true)));
                success = true; message = "反馈与会话快照已写盘并读回验证。";
            }
            catch (Exception ex) { message = "保存失败：" + ex.Message; }
            completed(requestId, success, message);
        }
    }
}
