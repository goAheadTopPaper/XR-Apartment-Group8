using System;
using System.IO;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Storage
{
    /// <summary>Assign this component to JourneyRuntimeHost's Storage Adapter port.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("XR Apartment/Storage/Local CSV Storage Adapter")]
    public sealed class LocalCsvStorageAdapter : MonoBehaviour, ISessionStore
    {
        private CsvSessionStore store;
        public string CsvPath => Store.FilePath;
        private CsvSessionStore Store => store ?? (store = new CsvSessionStore(
            Path.Combine(Application.persistentDataPath, "ReviewRecords")));

        public void Save(long requestId, SessionRecord record, Action<SaveResult> completed) =>
            Store.Save(requestId, record, completed);

        public void TryRead(string sessionId, Action<ReadResult> completed) =>
            Store.TryRead(sessionId, completed);

        [ContextMenu("Log CSV Storage Path")]
        private void LogStoragePath() => Debug.Log("Local review CSV: " + CsvPath, this);
    }
}
