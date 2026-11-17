using System;
using System.Collections.Generic;

namespace XRApartment.Data.ReviewV2
{
    public enum FeedbackType { Positive, Concern, Discuss }
    public enum FeedbackSaveStatus { Pending, Verified, Failed }

    [Serializable]
    public sealed class SpatialFeedbackRecord
    {
        public string schemaVersion = "review-v2-t3-1";
        public string sessionId, schemeId, schemeVisitId, modelVersion, markerId;
        public long timestampUtcMilliseconds;
        public string objectId, anchorId, zoneId;
        public float localX, localY, localZ;
        public bool hasObjectLocalPosition;
        public float worldX, worldY, worldZ;
        public FeedbackType type;
        public string beforeEventId, afterEventId;
        public string explanation, importance;
        public FeedbackSaveStatus saveStatus;
    }

    [Serializable]
    public sealed class SpatialFeedbackDocument
    {
        public string schemaVersion = "review-v2-t3-1";
        public List<SpatialFeedbackRecord> records = new List<SpatialFeedbackRecord>();
    }
}
