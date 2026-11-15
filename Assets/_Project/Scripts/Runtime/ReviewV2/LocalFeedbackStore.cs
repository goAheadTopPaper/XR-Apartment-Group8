using System;
using System.IO;
using System.Text;
using UnityEngine;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    public sealed class LocalFeedbackStore
    {
        readonly string filePath;
        public string FilePath => filePath;
        public LocalFeedbackStore(string customPath = null)
        { filePath = customPath ?? System.IO.Path.Combine(Application.persistentDataPath, "ReviewV2", "feedback.json"); }

        public SpatialFeedbackDocument Load()
        {
            if (!File.Exists(filePath)) return new SpatialFeedbackDocument();
            var doc = JsonUtility.FromJson<SpatialFeedbackDocument>(File.ReadAllText(filePath, Encoding.UTF8));
            if (doc == null || doc.records == null || doc.schemaVersion != "review-v2-t3-1")
                throw new InvalidDataException("Invalid or unsupported T3 feedback JSON");
            return doc;
        }

        public void Save(SpatialFeedbackDocument document)
        {
            if (document == null || document.records == null) throw new ArgumentNullException(nameof(document));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
            var temp = filePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(document, true), new UTF8Encoding(false));
            try
            {
                if (File.Exists(filePath)) File.Replace(temp, filePath, null);
                else File.Move(temp, filePath);
            }
            catch (PlatformNotSupportedException)
            {
                var backup = filePath + ".bak";
                if (File.Exists(filePath)) File.Copy(filePath, backup, true);
                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(temp, filePath);
                if (File.Exists(backup)) File.Delete(backup);
            }
            var check = Load();
            if (check.records.Count != document.records.Count) throw new IOException("Feedback read-back verification failed");
        }

        public string ExportJson() => JsonUtility.ToJson(Load(), true);
    }
}
