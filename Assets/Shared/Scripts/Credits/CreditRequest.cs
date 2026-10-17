using System;

namespace Shared.Credits
{
    /// <summary>Saved as a number: never reorder these.</summary>
    public enum CreditRequestStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    /// <summary>One request for test credits. Pending means NOTHING has been credited.</summary>
    [Serializable]
    public class CreditRequest
    {
        public string requestId;       // REQ-000001
        public string accountId;       // the test account that made the request
        public string packageId;
        public long credits;           // snapshot of the package value at request time
        public CreditRequestStatus status;
        public string createdUtc;
        public string resolvedUtc;     // empty while Pending
        public string rejectReason;    // only for Rejected
        public string transactionId;   // wallet ledger entry, only for Approved
    }
}