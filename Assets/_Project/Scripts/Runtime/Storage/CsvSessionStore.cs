using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Runtime.Storage
{
    /// <summary>One row per anonymous session; checkpoints replace that session's row (FR-10).
    /// Call on the Unity main thread. No scene objects or references to caller records are retained.
    /// </summary>
    public sealed class CsvSessionStore : ISessionStore
    {
        private static readonly string[] Header =
        {
            "schema_version", "session_id", "saved_at_utc", "model_version", "session_outcome",
            "total_seconds", "benchmark_order", "feedback_summary", "final_preference_combination_id",
            "final_trade_off_summary", "record_json"
        };
        private static readonly UTF8Encoding Encoding = new UTF8Encoding(true, true);
        private readonly Dictionary<string, long> latestRequests = new Dictionary<string, long>();

        public string FilePath { get; }

        public CsvSessionStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Storage directory is required.");
            FilePath = Path.Combine(Path.GetFullPath(directory), "sessions.csv");
        }

        public void Save(long requestId, SessionRecord record, Action<SaveResult> completed)
        {
            var result = new SaveResult { RequestId = requestId, Location = FilePath };
            string temporaryPath = null;
            try
            {
                ValidateIdentity(record);
                if (latestRequests.TryGetValue(record.sessionId, out var latest) && requestId < latest)
                {
                    result.ErrorCode = JourneyErrorCode.StaleResult;
                    result.Message = "A newer save request has already been received for this session.";
                }
                else
                {
                    latestRequests[record.sessionId] = requestId;
                    // Snapshot before touching disk. JsonUtility covers every existing schema field.
                    var snapshot = JsonUtility.FromJson<SessionRecord>(JsonUtility.ToJson(record));
                    var row = MakeRow(snapshot, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    using (AcquireLock())
                    {
                        var rows = LoadRows();
                        var index = FindRow(rows, snapshot.sessionId);
                        if (index >= 0)
                        {
                            if (Decode(rows[index]).modelVersion != snapshot.modelVersion)
                                throw new InvalidDataException("A session cannot change its model version.");
                            rows[index] = row;
                        }
                        else rows.Add(row);

                        temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            using (var writer = new StreamWriter(stream, Encoding, 4096, true))
                            {
                                writer.NewLine = "\r\n";
                                WriteRow(writer, Header);
                                foreach (var item in rows) WriteRow(writer, item);
                            }
                            stream.Flush(true);
                        }
                        VerifyRows(temporaryPath, rows);
                        if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
                        else File.Move(temporaryPath, FilePath);
                        temporaryPath = null;
                        VerifyRows(FilePath, rows);
                    }
                    result.Succeeded = true;
                    result.ReadBackVerified = true;
                    result.ErrorCode = JourneyErrorCode.None;
                }
            }
            catch (Exception exception)
            {
                result.ErrorCode = JourneyErrorCode.OperationFailed;
                result.Message = "CSV save failed: " + exception.Message;
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            // Invoke outside the I/O catch: a consumer exception must not cause a second callback.
            completed?.Invoke(result);
        }

        public void TryRead(string sessionId, Action<ReadResult> completed)
        {
            var result = new ReadResult();
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session ID is required.");
                using (AcquireLock())
                {
                    var rows = LoadRows();
                    var index = FindRow(rows, sessionId);
                    if (index < 0) throw new InvalidDataException("No saved record for this session.");
                    result.Record = Decode(rows[index]);
                }
                result.Succeeded = true;
                result.ErrorCode = JourneyErrorCode.None;
            }
            catch (Exception exception)
            {
                result.ErrorCode = JourneyErrorCode.OperationFailed;
                result.Message = "CSV read failed: " + exception.Message;
            }
            completed?.Invoke(result);
        }

        private FileStream AcquireLock() => new FileStream(FilePath + ".lock",
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        private List<string[]> LoadRows() => File.Exists(FilePath)
            ? ReadRows(FilePath) : new List<string[]>();

        private static List<string[]> ReadRows(string path)
        {
            var rows = Parse(File.ReadAllText(path, Encoding));
            if (rows.Count == 0 || !Equal(rows[0], Header))
                throw new InvalidDataException("CSV header or schema version is not supported.");
            rows.RemoveAt(0);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var record = Decode(row);
                if (!ids.Add(record.sessionId)) throw new InvalidDataException("Duplicate session ID in CSV.");
            }
            return rows;
        }

        private static SessionRecord Decode(string[] row)
        {
            if (row.Length != Header.Length || row[0] != "1")
                throw new InvalidDataException("Invalid CSV row or schema version.");
            var record = JsonUtility.FromJson<SessionRecord>(row[10]);
            ValidateIdentity(record);
            if (!DateTime.TryParseExact(row[2], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var savedAt) || savedAt.Kind != DateTimeKind.Utc ||
                !Equal(row, MakeRow(record, row[2])))
                throw new InvalidDataException("CSV summary does not match the stored record.");
            return record;
        }

        private static void ValidateIdentity(SessionRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.sessionId) ||
                string.IsNullOrWhiteSpace(record.modelVersion))
                throw new ArgumentException("Record, anonymous session ID and model version are required.");
        }

        private static string[] MakeRow(SessionRecord record, string savedAt)
        {
            var summary = new StringBuilder();
            if (record.benchmarkFeedback != null)
                foreach (var feedback in record.benchmarkFeedback)
                {
                    if (feedback == null) continue;
                    if (summary.Length > 0) summary.Append("\n");
                    summary.Append(feedback.combinationId).Append(": ").Append(feedback.taskOutcome)
                        .Append("; decision=").Append(feedback.decision)
                        .Append("; category=").Append(feedback.category)
                        .Append("; importance=").Append(feedback.importance)
                        .Append("; explanation=").Append(feedback.shortExplanation)
                        .Append("; reason=").Append(feedback.outcomeReason);
                }
            return new[]
            {
                "1", SpreadsheetText(record.sessionId), savedAt, SpreadsheetText(record.modelVersion),
                record.sessionOutcome.ToString(), record.totalSeconds.ToString("R", CultureInfo.InvariantCulture),
                SpreadsheetText(record.benchmarkOrder == null ? "" : string.Join(" > ", record.benchmarkOrder)),
                SpreadsheetText(summary.ToString()), SpreadsheetText(record.finalPreferenceCombinationId),
                SpreadsheetText(record.finalTradeOffSummary), JsonUtility.ToJson(record)
            };
        }

        // CSV quoting alone does not stop spreadsheet formula evaluation. The lossless JSON stays intact.
        private static string SpreadsheetText(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var trimmed = value.TrimStart();
            return (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) ||
                value[0] == '\t' || value[0] == '\r' || value[0] == '\n' ? "'" + value : value;
        }

        private static int FindRow(List<string[]> rows, string sessionId)
        {
            for (var i = 0; i < rows.Count; i++)
                if (Decode(rows[i]).sessionId == sessionId) return i;
            return -1;
        }

        private static void VerifyRows(string path, List<string[]> expected)
        {
            var actual = ReadRows(path);
            if (actual.Count != expected.Count) throw new InvalidDataException("CSV read-back row count mismatch.");
            for (var i = 0; i < actual.Count; i++)
                if (!Equal(actual[i], expected[i])) throw new InvalidDataException("CSV read-back content mismatch.");
        }

        private static bool Equal(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        private static void WriteRow(TextWriter writer, string[] row)
        {
            for (var i = 0; i < row.Length; i++)
            {
                if (i > 0) writer.Write(',');
                writer.Write('"');
                writer.Write(row[i].Replace("\"", "\"\""));
                writer.Write('"');
            }
            writer.WriteLine();
        }

        // Strict RFC 4180 reader: commas/CR/LF inside quotes belong to the cell.
        private static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            var closed = false;
            var started = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c != '"') cell.Append(c);
                    else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                    continue;
                }
                if (c == ',' || c == '\r' || c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    closed = false;
                    started = false;
                    if (c == ',') continue;
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    rows.Add(row.ToArray());
                    row.Clear();
                }
                else if (closed) throw new InvalidDataException("Unexpected text after CSV closing quote.");
                else if (c == '"')
                {
                    if (started) throw new InvalidDataException("Unexpected CSV quote.");
                    quoted = true;
                    started = true;
                }
                else { cell.Append(c); started = true; }
            }
            if (quoted) throw new InvalidDataException("Unclosed CSV quote (possibly interrupted write).");
            if (started || closed || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }
    }
}
