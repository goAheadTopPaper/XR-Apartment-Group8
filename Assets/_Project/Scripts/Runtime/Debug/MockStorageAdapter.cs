using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Debugging
{
    /// <summary>In-memory, ordered stand-in for the independent local storage module.</summary>
    public sealed class MockStorageAdapter : MonoBehaviour, ISessionStore
    {
        private sealed class SaveWork
        {
            public long RequestId;
            public string Json;
            public bool Fail;
            public Action<SaveResult> Completed;
        }

        [SerializeField, Min(0f)] private float responseDelaySeconds;
        [SerializeField] private bool failNextSave;
        [SerializeField] private bool omitNextCallback;

        private readonly Queue<SaveWork> pending = new Queue<SaveWork>();
        private readonly Dictionary<string, string> saved = new Dictionary<string, string>();
        private Coroutine pump;

        public void Save(long requestId, SessionRecord record, Action<SaveResult> completed)
        {
            if (record == null)
            {
                completed?.Invoke(new SaveResult
                {
                    RequestId = requestId, ErrorCode = JourneyErrorCode.OperationFailed,
                    Message = "Record is missing."
                });
                return;
            }
            pending.Enqueue(new SaveWork
            {
                RequestId = requestId,
                Json = JsonUtility.ToJson(record),
                Fail = failNextSave,
                Completed = omitNextCallback ? null : completed
            });
            failNextSave = false;
            omitNextCallback = false;
            if (pump == null) pump = StartCoroutine(WriteInOrder());
        }

        private IEnumerator WriteInOrder()
        {
            while (pending.Count > 0)
            {
                var work = pending.Dequeue();
                if (responseDelaySeconds > 0f)
                    yield return new WaitForSecondsRealtime(responseDelaySeconds);
                if (work.Fail)
                {
                    work.Completed?.Invoke(new SaveResult
                    {
                        RequestId = work.RequestId,
                        ErrorCode = JourneyErrorCode.OperationFailed,
                        Message = "Mock write failure."
                    });
                    continue;
                }
                var record = JsonUtility.FromJson<SessionRecord>(work.Json);
                if (record != null && !string.IsNullOrEmpty(record.sessionId))
                    saved[record.sessionId] = work.Json;
                SessionRecord readBack = null;
                if (record != null && saved.TryGetValue(record.sessionId, out var json))
                    readBack = JsonUtility.FromJson<SessionRecord>(json);
                var verified = readBack != null && readBack.sessionId == record.sessionId &&
                    readBack.modelVersion == record.modelVersion &&
                    readBack.sessionOutcome == record.sessionOutcome &&
                    readBack.benchmarkFeedback.Count == record.benchmarkFeedback.Count;
                work.Completed?.Invoke(new SaveResult
                {
                    RequestId = work.RequestId,
                    Succeeded = verified, ReadBackVerified = verified,
                    Location = verified ? "memory://" + record.sessionId : null,
                    ErrorCode = verified ? JourneyErrorCode.None : JourneyErrorCode.OperationFailed,
                    Message = verified ? null : "Mock read-back mismatch."
                });
            }
            pump = null;
        }

        public void TryRead(string sessionId, Action<ReadResult> completed)
        {
            string json = null;
            var found = !string.IsNullOrEmpty(sessionId) && saved.TryGetValue(sessionId, out json);
            completed?.Invoke(new ReadResult
            {
                Succeeded = found,
                Record = found ? JsonUtility.FromJson<SessionRecord>(json) : null,
                ErrorCode = found ? JourneyErrorCode.None : JourneyErrorCode.OperationFailed,
                Message = found ? null : "No mock record for session."
            });
        }
    }
}
