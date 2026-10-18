using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Runtime.Storage;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public class CsvSessionStoreTests
    {
        private string directory;
        private CsvSessionStore store;

        [Test]
        public void ReviewPrefab_UsesLocalCsvForTheFeedbackHost()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Core/PREF_Core_PlayValidation.prefab");
            Assert.That(prefab, Is.Not.Null);
            var host = prefab.GetComponent<XRApartment.Runtime.Core.JourneyRuntimeHost>();
            Assert.That(host, Is.Not.Null);
            var serialized = new UnityEditor.SerializedObject(host);
            Assert.That(serialized.FindProperty("storageAdapter").objectReferenceValue,
                Is.TypeOf<LocalCsvStorageAdapter>());
            Assert.That(prefab.GetComponent<XRApartment.Runtime.Debugging.MockStorageAdapter>(), Is.Null);
        }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "XRApartment-CsvTests", Guid.NewGuid().ToString("N"));
            store = new CsvSessionStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static SessionRecord Record(string id = "anonymous-1")
        {
            var record = new SessionRecord
            {
                sessionId = id, modelVersion = "prototype-v1", sessionOutcome = SessionOutcome.Completed,
                finalPreferenceCombinationId = "B2", finalTradeOffSummary = "更宽敞, 保留\"原说明\"\r\n第二行",
                totalSeconds = 123.75f
            };
            record.benchmarkOrder.AddRange(new[] { "B1", "B2" });
            record.benchmarkFeedback.Add(new BenchmarkFeedback
            {
                combinationId = "B1", taskOutcome = TaskOutcome.Completed,
                decision = CombinationDecision.NeedsRevision, category = FeedbackCategory.DoorClearance,
                importance = Importance.High, shortExplanation = "门挡住了, \"通道\"\n需要后退",
                experience = ExperienceTag.DoorBlocked | ExperienceTag.NeedsToStepBack,
                everydayImpact = EverydayImpact.ProlongsTask, discoveryMode = DiscoveryMode.HumanObserved
            });
            record.benchmarkFeedback.Add(new BenchmarkFeedback
            { combinationId = "B2", taskOutcome = TaskOutcome.Skipped, outcomeReason = "需要休息" });
            record.markers.Add(new MarkerRecord
            { markerId = "marker-1", combinationId = "B1", category = MarkerCategory.FridgeDoor, x = 0.25f });
            record.conflicts.Add(new ConflictRecord
            {
                issueId = "issue-1", combinationId = "B1", modelVersion = "prototype-v1", objectId = "door",
                role = ReviewRole.FridgeDoor, assumptionKey = "registered-test-assumption",
                measurement = 0.125f, sessionSeconds = 12.5f, message = "测试冲突", feedbackRecorded = true
            });
            record.promptEvents.Add(new TimedEvent { type = "prompt", combinationId = "B1", sessionSeconds = 10f });
            record.assistanceEvents.Add(new TimedEvent { type = "reset", combinationId = "B1", sessionSeconds = 20f });
            record.comfortOrAbort.Add(new TimedEvent { type = "pause", combinationId = "B2", sessionSeconds = 30f });
            return record;
        }

        private static SaveResult Save(CsvSessionStore target, long requestId, SessionRecord record)
        {
            SaveResult result = null;
            var callbacks = 0;
            target.Save(requestId, record, value => { result = value; callbacks++; });
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.RequestId, Is.EqualTo(requestId));
            return result;
        }

        private static ReadResult Read(CsvSessionStore target, string id)
        {
            ReadResult result = null;
            target.TryRead(id, value => result = value);
            return result;
        }

        [Test]
        public void Save_RestartRead_PreservesAllFieldsAndUnicodeMultilineText()
        {
            var record = Record();
            var result = Save(store, 1, record);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.ReadBackVerified, Is.True);
            Assert.That(result.Location, Is.EqualTo(store.FilePath));
            var recovered = Read(new CsvSessionStore(directory), record.sessionId);
            Assert.That(recovered.Succeeded, Is.True, recovered.Message);
            Assert.That(JsonUtility.ToJson(recovered.Record), Is.EqualTo(JsonUtility.ToJson(record)));
            var bytes = File.ReadAllBytes(store.FilePath);
            CollectionAssert.AreEqual(new byte[] { 0xef, 0xbb, 0xbf }, bytes.Take(3).ToArray());
            StringAssert.Contains("decision=NeedsRevision", File.ReadAllText(store.FilePath));
            StringAssert.Contains("experience=DoorBlocked, NeedsToStepBack", File.ReadAllText(store.FilePath));
            StringAssert.Contains("everydayImpact=ProlongsTask", File.ReadAllText(store.FilePath));
        }

        [Test]
        public void DifferentUsers_AddRows_WhileCheckpointAndRetryKeepOneRowPerSession()
        {
            var first = Record();
            Assert.That(Save(store, 1, first).Succeeded, Is.True);
            Assert.That(Save(store, 2, Record("anonymous-2")).Succeeded, Is.True);
            first.finalTradeOffSummary = "更新结果";
            Assert.That(Save(store, 3, first).Succeeded, Is.True);
            Assert.That(Save(store, 4, first).Succeeded, Is.True);
            var csv = File.ReadAllText(store.FilePath);
            // Count schema-version cells at row boundaries; feedback newlines cannot create these.
            Assert.That(csv.Split(new[] { "\r\n\"1\"," }, StringSplitOptions.None).Length - 1, Is.EqualTo(2));
            Assert.That(Read(store, "anonymous-1").Record.finalTradeOffSummary, Is.EqualTo("更新结果"));
            Assert.That(Read(store, "anonymous-2").Succeeded, Is.True);
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void EarlierCsvSummary_RemainsReadableAfterAddingChoiceDetails()
        {
            Assert.That(Save(store, 1, Record()).Succeeded, Is.True);
            var csv = File.ReadAllText(store.FilePath)
                .Replace("; experience=DoorBlocked, NeedsToStepBack; everydayImpact=ProlongsTask; discoveryMode=HumanObserved", "")
                .Replace("; experience=None; everydayImpact=Unset; discoveryMode=Unset", "");
            File.WriteAllText(store.FilePath, csv, new UTF8Encoding(true));
            Assert.That(Read(new CsvSessionStore(directory), "anonymous-1").Succeeded, Is.True);
            Assert.That(Save(store, 2, Record("anonymous-2")).Succeeded, Is.True);
            Assert.That(Read(store, "anonymous-1").Succeeded, Is.True);
        }

        [Test]
        public void SavedRecord_IsIndependentFromCallerAndReadResults()
        {
            var record = Record();
            Assert.That(Save(store, 1, record).Succeeded, Is.True);
            record.benchmarkFeedback[0].shortExplanation = "改动内存";
            var loaded = Read(store, record.sessionId).Record;
            Assert.That(loaded.benchmarkFeedback[0].shortExplanation, Does.Contain("门挡住了"));
            loaded.benchmarkFeedback.Clear();
            Assert.That(Read(store, record.sessionId).Record.benchmarkFeedback.Count, Is.EqualTo(2));
        }

        [Test]
        public void CheckpointsAndAbortedSessions_DoNotRequireCompletedForm()
        {
            var record = new SessionRecord { sessionId = "checkpoint", modelVersion = "prototype-v1" };
            Assert.That(Save(store, 1, record).Succeeded, Is.True);
            record.sessionOutcome = SessionOutcome.Aborted;
            Assert.That(Save(store, 2, record).Succeeded, Is.True);
            Assert.That(Read(store, record.sessionId).Record.sessionOutcome, Is.EqualTo(SessionOutcome.Aborted));
        }

        [Test]
        public void LockedCsv_ReturnsFailure_PreservesOldContent_AndRetrySucceeds()
        {
            Assert.That(Save(store, 1, Record()).Succeeded, Is.True);
            var oldBytes = File.ReadAllBytes(store.FilePath);
            var updated = Record();
            updated.finalTradeOffSummary = "重试结果";
            using (File.Open(store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = Save(store, 2, updated);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.ReadBackVerified, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo(JourneyErrorCode.OperationFailed));
            }
            CollectionAssert.AreEqual(oldBytes, File.ReadAllBytes(store.FilePath));
            Assert.That(Save(store, 3, updated).Succeeded, Is.True);
            Assert.That(Read(store, updated.sessionId).Record.finalTradeOffSummary, Is.EqualTo("重试结果"));
        }

        [Test]
        public void SharedStoreLock_PreventsAnotherInstanceFromWriting()
        {
            Directory.CreateDirectory(directory);
            using (File.Open(store.FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                Assert.That(Save(new CsvSessionStore(directory), 1, Record()).Succeeded, Is.False);
            Assert.That(Save(store, 2, Record()).Succeeded, Is.True);
        }

        [TestCase("broken header\r\n")]
        [TestCase("\"unclosed")]
        public void CorruptCsv_IsNotOverwritten(string contents)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.FilePath, contents, new UTF8Encoding(true));
            var result = Save(store, 1, Record());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Read(store, "anonymous-1").Succeeded, Is.False);
            Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo(contents));
        }

        [Test]
        public void UnwritableDestination_ReturnsFailure()
        {
            Directory.CreateDirectory(directory);
            var blockingFile = Path.Combine(directory, "file-instead-of-directory");
            File.WriteAllText(blockingFile, "keep");
            Assert.That(Save(new CsvSessionStore(blockingFile), 1, Record()).Succeeded, Is.False);
            Assert.That(File.ReadAllText(blockingFile), Is.EqualTo("keep"));
        }

        [Test]
        public void MissingIdentityAndUnknownSession_ReturnExplicitFailures()
        {
            Assert.That(Save(store, 1, null).Succeeded, Is.False);
            Assert.That(Save(store, 2, new SessionRecord { sessionId = "id" }).Succeeded, Is.False);
            Assert.That(Read(store, "unknown").Succeeded, Is.False);
            Assert.That(File.Exists(store.FilePath), Is.False);
        }

        [Test]
        public void OlderRequestAndChangedModel_CannotReplaceNewerSnapshot()
        {
            var record = Record();
            Assert.That(Save(store, 5, record).Succeeded, Is.True);
            record.finalTradeOffSummary = "旧请求";
            Assert.That(Save(store, 4, record).ErrorCode, Is.EqualTo(JourneyErrorCode.StaleResult));
            record.modelVersion = "different-model";
            Assert.That(Save(store, 6, record).Succeeded, Is.False);
            Assert.That(Read(store, record.sessionId).Record.modelVersion, Is.EqualTo("prototype-v1"));
        }

        [TestCase("=1+1")]
        [TestCase("  @SUM(1,2)")]
        [TestCase("\t=1+1")]
        public void SpreadsheetFormulaText_IsEscapedWithoutChangingRecoveredAnswer(string answer)
        {
            var record = Record();
            record.finalTradeOffSummary = answer;
            Assert.That(Save(store, 1, record).Succeeded, Is.True);
            StringAssert.Contains("\"'" + answer + "\"", File.ReadAllText(store.FilePath));
            Assert.That(Read(store, record.sessionId).Record.finalTradeOffSummary, Is.EqualTo(answer));
        }

        [Test]
        public void ConsumerException_DoesNotInvokeCallbackTwice()
        {
            var callbacks = 0;
            Assert.Throws<InvalidOperationException>(() => store.Save(1, Record(), _ =>
            {
                callbacks++;
                throw new InvalidOperationException("Consumer failure");
            }));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(Read(store, "anonymous-1").Succeeded, Is.True);
        }
    }
}
