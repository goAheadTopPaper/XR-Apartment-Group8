using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using XRApartment.Data.ReviewV2;
namespace XRApartment.Runtime.ReviewV2
{
    public sealed class LocalFeedbackStore
    {
        static readonly SemaphoreSlim WriteGate = new SemaphoreSlim(1);
        readonly string filePath;

        // Only detached data and paths cross to the worker; no Unity objects are captured.
        internal static Task WriteAsync(Action write) => Task.Run(async () =>
        {
            await WriteGate.WaitAsync().ConfigureAwait(false);
            try { write(); }
            finally { WriteGate.Release(); }
        });
        public string FilePath => filePath;
        public static string DefaultDirectory => Environment.GetEnvironmentVariable("REVIEW_V2_FEEDBACK_DIRECTORY") ?? Path.Combine(Application.persistentDataPath, "ReviewV2");
        public LocalFeedbackStore(string customPath = null) { filePath = customPath ?? Path.Combine(DefaultDirectory, "feedback.json"); }
        public SpatialFeedbackDocument Load() => File.Exists(filePath) ? Decode(File.ReadAllText(filePath, Encoding.UTF8)) : new SpatialFeedbackDocument();
        [Serializable] sealed class Header { public string schemaVersion = null; }
        static SpatialFeedbackDocument Decode(string json)
        {
            if (JsonUtility.FromJson<Header>(json)?.schemaVersion != "review-v2-t3-1") throw new InvalidDataException("Invalid feedback schema");
            var doc = JsonUtility.FromJson<SpatialFeedbackDocument>(json);
            if (doc == null || doc.records == null || doc.schemaVersion != "review-v2-t3-1" || doc.records.Exists(r => r == null || string.IsNullOrEmpty(r.markerId) || string.IsNullOrEmpty(r.sessionId) || string.IsNullOrEmpty(r.schemeVisitId)))
                throw new InvalidDataException("Invalid or unsupported T3 feedback JSON");
            return doc;
        }
        public void Save(SpatialFeedbackDocument document)
        {
            if (document == null || document.records == null) throw new ArgumentNullException(nameof(document));
            var json = JsonUtility.ToJson(document, true); Decode(json);
            WriteVerified(filePath, json);
        }
        internal static void WriteVerified(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp", backup = path + ".bak";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.ReadAllText(temp, Encoding.UTF8) != json) throw new IOException("Temporary write verification failed");
            if (File.Exists(path))
            {
                try { File.Replace(temp, path, backup); }
                catch (PlatformNotSupportedException) { File.Copy(path, backup, true); File.Copy(temp, path, true); File.Delete(temp); }
            }
            else File.Move(temp, path);
            if (File.ReadAllText(path, Encoding.UTF8) != json) throw new IOException("Read-back verification failed");
            if (File.Exists(backup)) File.Delete(backup);
        }
        public string ExportJson() => JsonUtility.ToJson(Load(), true);
    }
}
