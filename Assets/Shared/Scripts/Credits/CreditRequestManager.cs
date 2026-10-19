using System;
using System.Collections.Generic;
using UnityEngine;
using Shared.Save;
using Shared.Wallet;

namespace Shared.Credits
{
    [Serializable]
    public class CreditRequestsSaveData
    {
        public string accountId;
        public long lastRequestNumber;
        public List<CreditRequest> requests = new List<CreditRequest>();
    }

    /// <summary>
    /// Stores credit requests (in their own save section) and applies approvals through the wallet.
    /// ONLY ACTIVE IN TEST CREDITS MODE: in Virtual mode it does nothing and writes nothing.
    ///
    /// Double-approval protection:
    ///  1. The request must exist, be Pending, belong to this account, and match the current package value.
    ///  2. It is marked Approved in memory BEFORE the wallet call, so the wallet's auto-save already stores it.
    ///  3. If the wallet refuses, the request goes back to Pending and nothing is credited.
    ///  4. On load, a Pending request that already has a ledger entry is repaired to Approved (crash safety).
    ///  5. A busy flag blocks a second approval while one is running (repeated clicks).
    /// LOCAL PROTOTYPE ONLY: anyone who can edit the save file can change these records.
    /// A real version needs a backend that owns accounts, balances and approvals.
    /// </summary>
    public class CreditRequestManager : MonoBehaviour
    {
        public const string SaveKey = "TestCredits.Requests";
        private const int MaxPendingRequests = 5;

        [SerializeField] private SaveManager saveManager;
        [SerializeField] private VirtualWalletManager walletManager;
        [SerializeField] private CreditPackageConfig packageConfig;

        /// <summary>Raised whenever a request is created, approved, rejected, loaded or reset.</summary>
        public event Action OnRequestsChanged;

        private CreditRequestsSaveData data = new CreditRequestsSaveData();
        private bool isReady;
        private bool loaded;
        private bool busy;

        public bool IsActive => WalletModeSettings.IsTest;
        public string AccountId => data.accountId;
        public IReadOnlyList<CreditRequest> Requests => data.requests;
        public CreditPackageConfig Packages => packageConfig;

        public int PendingCount
        {
            get
            {
                int n = 0;
                foreach (CreditRequest r in data.requests)
                {
                    if (r.status == CreditRequestStatus.Pending) n++;
                }
                return n;
            }
        }

        private void Awake()
        {
            if (saveManager == null || walletManager == null || packageConfig == null)
            {
                Debug.LogError("[CreditRequestManager] A reference is missing in the Inspector " +
                               "(Save Manager, Wallet Manager, Package Config).", this);
                return;
            }
            isReady = true;
        }

        private void OnEnable()
        {
            if (!isReady) return;
            saveManager.OnBeforeSave += HandleBeforeSave;
            saveManager.OnGameLoaded += HandleGameLoaded;
            saveManager.OnGameReset += HandleGameReset;
        }

        private void Start()
        {
            // The save may have loaded before we started listening. Catch up once.
            if (isReady && !loaded && saveManager.IsReady) HandleGameLoaded();
        }

        private void OnDisable()
        {
            if (saveManager == null) return;
            saveManager.OnBeforeSave -= HandleBeforeSave;
            saveManager.OnGameLoaded -= HandleGameLoaded;
            saveManager.OnGameReset -= HandleGameReset;
        }

        // ---------- Creating ----------

        public bool CreateRequest(string packageId, out CreditRequest request, out string error)
        {
            request = null;
            if (!CanOperate(out error)) return false;

            if (!packageConfig.TryGetPackage(packageId, out CreditPackage package))
            {
                error = "Unknown package.";
                return false;
            }

            if (PendingCount >= MaxPendingRequests)
            {
                error = $"You already have {MaxPendingRequests} pending requests. Wait for the admin.";
                return false;
            }

            data.lastRequestNumber++;
            request = new CreditRequest
            {
                requestId = "REQ-" + data.lastRequestNumber.ToString("000000"),
                accountId = data.accountId,
                packageId = package.packageId,
                credits = package.credits,
                status = CreditRequestStatus.Pending,
                createdUtc = DateTime.UtcNow.ToString("o"),
                resolvedUtc = string.Empty,
                rejectReason = string.Empty,
                transactionId = string.Empty
            };

            data.requests.Add(request);
            saveManager.SaveGame();
            OnRequestsChanged?.Invoke();
            return true;
        }

        // ---------- Approving ----------

        public bool ApproveRequest(string requestId, out string error)
        {
            if (!CanOperate(out error)) return false;

            CreditRequest r = Find(requestId);
            if (r == null) { error = "Request not found."; return false; }
            if (r.status != CreditRequestStatus.Pending) { error = $"Request is already {r.status}."; return false; }

            if (LedgerHasApproval(r.requestId))
            {
                MarkApprovedFromLedger(r);
                saveManager.SaveGame();
                OnRequestsChanged?.Invoke();
                error = "These credits were already added earlier. The request was marked Approved.";
                return false;
            }

            if (r.accountId != data.accountId) { error = "This request belongs to a different account."; return false; }

            if (!packageConfig.TryGetPackage(r.packageId, out CreditPackage package) || package.credits != r.credits)
            {
                error = "The package no longer matches this request. Reject it and ask for a new one.";
                return false;
            }

            busy = true;
            try
            {
                r.status = CreditRequestStatus.Approved;
                r.resolvedUtc = DateTime.UtcNow.ToString("o");
                r.rejectReason = string.Empty;

                bool ok = walletManager.AddCredits(r.credits, TransactionType.TestCreditApproved, r.requestId);
                if (!ok)
                {
                    r.status = CreditRequestStatus.Pending;
                    r.resolvedUtc = string.Empty;
                    error = "The wallet refused the credit. The request is still Pending.";
                    return false;
                }

                r.transactionId = FindLedgerTransactionId(r.requestId);
                saveManager.SaveGame();
            }
            finally
            {
                busy = false;
            }

            OnRequestsChanged?.Invoke();
            error = null;
            return true;
        }

        // ---------- Rejecting ----------

        public bool RejectRequest(string requestId, string reason, out string error)
        {
            if (!CanOperate(out error)) return false;

            CreditRequest r = Find(requestId);
            if (r == null) { error = "Request not found."; return false; }
            if (r.status != CreditRequestStatus.Pending) { error = $"Request is already {r.status}."; return false; }

            r.status = CreditRequestStatus.Rejected;
            r.resolvedUtc = DateTime.UtcNow.ToString("o");
            r.rejectReason = string.IsNullOrWhiteSpace(reason) ? "Rejected by admin" : reason.Trim();

            saveManager.SaveGame();
            OnRequestsChanged?.Invoke();
            error = null;
            return true;
        }

        // ---------- Save hooks ----------

        private void HandleBeforeSave()
        {
            if (!loaded || !IsActive) return; // Virtual mode never writes this section
            saveManager.SetSection(SaveKey, data);
        }

        private void HandleGameLoaded()
        {
            if (!IsActive) return;

            if (!saveManager.TryGetSection(SaveKey, out CreditRequestsSaveData saved) || saved.requests == null)
            {
                saved = new CreditRequestsSaveData();
            }

            data = saved;
            EnsureAccountId();
            loaded = true;

            bool repaired = false;
            foreach (CreditRequest r in data.requests)
            {
                if (r != null && r.status == CreditRequestStatus.Pending && LedgerHasApproval(r.requestId))
                {
                    MarkApprovedFromLedger(r);
                    repaired = true;
                }
            }

            if (repaired) saveManager.SaveGame();
            OnRequestsChanged?.Invoke();
        }

        private void HandleGameReset()
        {
            if (!IsActive) return;
            data = new CreditRequestsSaveData();
            EnsureAccountId();
            loaded = true;
            OnRequestsChanged?.Invoke();
        }

        // ---------- Helpers ----------

        private bool CanOperate(out string error)
        {
            error = null;
            if (!isReady) error = "Credit system is not set up.";
            else if (!IsActive) error = "Credit requests are only available in Test Credits mode.";
            else if (!loaded) error = "Save data is not loaded yet.";
            else if (busy) error = "Another operation is running.";
            return error == null;
        }

        private void EnsureAccountId()
        {
            if (string.IsNullOrEmpty(data.accountId))
            {
                data.accountId = "TEST-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            }
        }

        private CreditRequest Find(string requestId)
        {
            foreach (CreditRequest r in data.requests)
            {
                if (r != null && r.requestId == requestId) return r;
            }
            return null;
        }

        private bool LedgerHasApproval(string requestId)
        {
            return !string.IsNullOrEmpty(FindLedgerTransactionId(requestId));
        }

        private string FindLedgerTransactionId(string requestId)
        {
            IReadOnlyList<WalletTransaction> ledger = walletManager.Transactions;
            for (int i = ledger.Count - 1; i >= 0; i--)
            {
                WalletTransaction tx = ledger[i];
                if (tx != null && tx.type == TransactionType.TestCreditApproved && tx.gameRoundId == requestId)
                {
                    return tx.transactionId;
                }
            }
            return string.Empty;
        }

        private void MarkApprovedFromLedger(CreditRequest r)
        {
            r.status = CreditRequestStatus.Approved;
            r.transactionId = FindLedgerTransactionId(r.requestId);
            if (string.IsNullOrEmpty(r.resolvedUtc)) r.resolvedUtc = DateTime.UtcNow.ToString("o");
        }

        // ---------- Temporary test tools (until the admin panel exists in Step 3) ----------

        [ContextMenu("TEST: Approve oldest pending request")]
        private void TestApproveOldest()
        {
            foreach (CreditRequest r in data.requests)
            {
                if (r.status != CreditRequestStatus.Pending) continue;
                bool ok = ApproveRequest(r.requestId, out string error);
                Debug.Log(ok ? $"[Credits] Approved {r.requestId}." : $"[Credits] Not approved: {error}");
                return;
            }
            Debug.Log("[Credits] No pending requests.");
        }

        [ContextMenu("TEST: Reject oldest pending request")]
        private void TestRejectOldest()
        {
            foreach (CreditRequest r in data.requests)
            {
                if (r.status != CreditRequestStatus.Pending) continue;
                bool ok = RejectRequest(r.requestId, "Test rejection", out string error);
                Debug.Log(ok ? $"[Credits] Rejected {r.requestId}." : $"[Credits] Not rejected: {error}");
                return;
            }
            Debug.Log("[Credits] No pending requests.");
        }
    }
}