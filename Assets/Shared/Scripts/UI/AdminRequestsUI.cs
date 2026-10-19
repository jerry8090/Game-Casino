using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Shared.Audio;
using Shared.Credits;
using Shared.Save;
using Shared.Wallet;

namespace Shared.UI
{
    /// <summary>
    /// SIMULATED ADMIN panel (development only). Lists credit requests and lets the tester approve or reject them.
    /// It is NOT secure: anyone who can edit the local save can bypass it. All rules live in CreditRequestManager.
    /// Only available in Test Credits mode AND in the Editor or a Development Build.
    /// Put this on an always-active object, NOT on the panel.
    /// </summary>
    public class AdminRequestsUI : MonoBehaviour, IBackNavigable
    {
        private enum Filter { Pending, Approved, Rejected, All }

        [Header("Required")]
        [SerializeField] private CreditRequestManager requestManager;
        [SerializeField] private VirtualWalletManager walletManager;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button filterButton;
        [SerializeField] private TMP_Text filterLabel;
        [SerializeField] private TMP_InputField reasonInput;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private AdminRequestRowView rowPrefab;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject emptyMessage;

        [Header("Look")]
        [SerializeField] private Color pendingTint = new Color(0.35f, 0.28f, 0.10f, 1f);
        [SerializeField] private Color approvedTint = new Color(0.17f, 0.33f, 0.22f, 1f);
        [SerializeField] private Color rejectedTint = new Color(0.33f, 0.18f, 0.20f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.48f, 0.48f, 1f);
        [SerializeField] private Color okColor = new Color(0.49f, 0.89f, 0.55f, 1f);

        [Header("Optional")]
        [SerializeField] private AudioManager audioManager;

        private readonly List<AdminRequestRowView> rows = new List<AdminRequestRowView>();
        private Filter filter = Filter.Pending;
        private bool isReady;

        public bool IsOpen => panel != null && panel.activeSelf;

        private static bool AdminAllowed => WalletModeSettings.IsTest && Debug.isDebugBuild;

        private void Awake()
        {
            if (requestManager == null || walletManager == null || openButton == null || panel == null ||
                closeButton == null || filterButton == null || filterLabel == null || reasonInput == null ||
                statusText == null || rowPrefab == null || rowContainer == null || scrollRect == null ||
                emptyMessage == null)
            {
                Debug.LogError("[AdminRequestsUI] A reference is missing in the Inspector.", this);
                return;
            }

            openButton.gameObject.SetActive(AdminAllowed);

            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
            filterButton.onClick.RemoveAllListeners();
            filterButton.onClick.AddListener(CycleFilter);

            panel.SetActive(false);
            isReady = true;
        }

        private void OnEnable()
        {
            if (isReady) requestManager.OnRequestsChanged += HandleRequestsChanged;
        }

        private void OnDisable()
        {
            if (requestManager != null) requestManager.OnRequestsChanged -= HandleRequestsChanged;
        }

        public void Open()
        {
            if (!isReady || IsOpen || !AdminAllowed) return;
            Click();
            statusText.text = "SIMULATED ADMIN - NOT SECURE";
            statusText.color = errorColor;
            panel.SetActive(true);
            Refresh();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            if (!isReady || !IsOpen) return;
            Click();
            panel.SetActive(false);
        }

        private void CycleFilter()
        {
            Click();
            filter = (Filter)(((int)filter + 1) % 4);
            Refresh();
        }

        private void HandleRequestsChanged()
        {
            if (IsOpen) Refresh();
        }

        // ---------- Actions (the manager does all the checking) ----------

        private void HandleApprove(string requestId)
        {
            if (requestManager.ApproveRequest(requestId, out string error))
            {
                Say($"APPROVED {requestId}. Credits added.", false);
            }
            else
            {
                Say(error, true);
            }
        }

        private void HandleReject(string requestId)
        {
            if (requestManager.RejectRequest(requestId, reasonInput.text, out string error))
            {
                reasonInput.text = string.Empty;
                Say($"REJECTED {requestId}.", false);
            }
            else
            {
                Say(error, true);
            }
        }

        private void Say(string message, bool isError)
        {
            statusText.text = message;
            statusText.color = isError ? errorColor : okColor;
        }

        // ---------- Drawing ----------

        private void Refresh()
        {
            filterLabel.text = "SHOW: " + filter.ToString().ToUpperInvariant();

            List<CreditRequest> shown = new List<CreditRequest>();
            IReadOnlyList<CreditRequest> all = requestManager.Requests;
            for (int i = all.Count - 1; i >= 0; i--) // newest first
            {
                CreditRequest r = all[i];
                if (r != null && Matches(r)) shown.Add(r);
            }

            while (rows.Count < shown.Count)
            {
                AdminRequestRowView row = Instantiate(rowPrefab, rowContainer);
                row.OnApprove += HandleApprove;
                row.OnReject += HandleReject;
                rows.Add(row);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (i < shown.Count)
                {
                    CreditRequest r = shown[i];
                    rows[i].gameObject.SetActive(true);
                    rows[i].Bind(r.requestId, BuildInfo(r), TintFor(r), r.status == CreditRequestStatus.Pending);
                }
                else
                {
                    rows[i].gameObject.SetActive(false);
                }
            }

            emptyMessage.SetActive(shown.Count == 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowContainer);
        }

        private bool Matches(CreditRequest r)
        {
            switch (filter)
            {
                case Filter.Pending: return r.status == CreditRequestStatus.Pending;
                case Filter.Approved: return r.status == CreditRequestStatus.Approved;
                case Filter.Rejected: return r.status == CreditRequestStatus.Rejected;
                default: return true;
            }
        }

        private Color TintFor(CreditRequest r)
        {
            switch (r.status)
            {
                case CreditRequestStatus.Approved: return approvedTint;
                case CreditRequestStatus.Rejected: return rejectedTint;
                default: return pendingTint;
            }
        }

        private string BuildInfo(CreditRequest r)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>{r.requestId}</b>   {r.status.ToString().ToUpperInvariant()}");
            sb.AppendLine($"<size=75%>Account: {r.accountId}");
            sb.AppendLine($"{r.credits.ToString("N0", CultureInfo.InvariantCulture)} TEST CREDITS  ({r.packageId})");
            sb.AppendLine("Requested: " + FormatTime(r.createdUtc));

            if (r.status == CreditRequestStatus.Rejected)
            {
                sb.AppendLine($"Rejected: {FormatTime(r.resolvedUtc)}   Reason: {r.rejectReason}");
            }
            else if (r.status == CreditRequestStatus.Approved)
            {
                sb.Append($"Approved: {FormatTime(r.resolvedUtc)}   {LedgerText(r.transactionId)}");
            }

            return sb.ToString();
        }

        // Shows the linked wallet transaction (balance before and after).
        private string LedgerText(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return "No transaction linked";

            foreach (WalletTransaction tx in walletManager.Transactions)
            {
                if (tx != null && tx.transactionId == transactionId)
                {
                    return $"{tx.transactionId}: {tx.balanceBefore.ToString("N0", CultureInfo.InvariantCulture)} -> " +
                           tx.balanceAfter.ToString("N0", CultureInfo.InvariantCulture);
                }
            }
            return transactionId + " (older than the stored ledger)";
        }

        private static string FormatTime(string isoUtc)
        {
            if (DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime t))
            {
                return t.ToLocalTime().ToString("MMM d, HH:mm:ss", CultureInfo.InvariantCulture);
            }
            return "-";
        }

        private void Click()
        {
            if (audioManager != null) audioManager.PlaySfx(SfxType.ButtonClick);
        }
    }
}