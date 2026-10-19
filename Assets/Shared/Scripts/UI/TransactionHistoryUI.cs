using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Shared.Audio;
using Shared.Credits;
using Shared.Wallet;

namespace Shared.UI
{
    /// <summary>
    /// The WALLET history screen: wallet ledger entries merged with credit requests, newest first.
    /// Approved requests appear as their ledger entry ("APPROVED TEST CREDIT"). Pending and rejected requests
    /// appear as requests and are never shown as completed deposits. Display only.
    /// Works in both wallet modes (in Virtual mode there are simply no requests).
    /// Put this on an always-active object, NOT on the panel.
    /// </summary>
    public class TransactionHistoryUI : MonoBehaviour, IBackNavigable
    {
        private const int MaxRows = 200;

        private class Entry
        {
            public DateTime time;
            public string text;
            public Color tint;
        }

        [Header("Required")]
        [SerializeField] private VirtualWalletManager walletManager;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject emptyMessage;

        [Header("Optional")]
        [Tooltip("Leave empty if you do not use credit requests.")]
        [SerializeField] private CreditRequestManager requestManager;
        [SerializeField] private AudioManager audioManager;

        [Header("Look")]
        [SerializeField] private Color neutralTint = new Color(0.23f, 0.16f, 0.33f, 1f);
        [SerializeField] private Color gainTint = new Color(0.17f, 0.33f, 0.22f, 1f);
        [SerializeField] private Color lossTint = new Color(0.33f, 0.18f, 0.20f, 1f);
        [SerializeField] private Color pendingTint = new Color(0.35f, 0.28f, 0.10f, 1f);

        private readonly List<Image> rowBackgrounds = new List<Image>();
        private readonly List<TMP_Text> rowTexts = new List<TMP_Text>();
        private bool isReady;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (walletManager == null || openButton == null || panel == null || closeButton == null ||
                rowPrefab == null || rowContainer == null || scrollRect == null || emptyMessage == null)
            {
                Debug.LogError("[TransactionHistoryUI] A reference is missing in the Inspector.", this);
                return;
            }

            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);

            panel.SetActive(false);
            isReady = true;
        }

        private void OnEnable()
        {
            if (!isReady) return;
            walletManager.OnBalanceChanged += HandleChanged;
            if (requestManager != null) requestManager.OnRequestsChanged += HandleChanged;
        }

        private void OnDisable()
        {
            if (walletManager != null) walletManager.OnBalanceChanged -= HandleChanged;
            if (requestManager != null) requestManager.OnRequestsChanged -= HandleChanged;
        }

        public void Open()
        {
            if (!isReady || IsOpen) return;
            Click();
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

        private void HandleChanged(long balance) { if (IsOpen) Refresh(); }
        private void HandleChanged() { if (IsOpen) Refresh(); }

        // ---------- Building the list ----------

        private void Refresh()
        {
            List<Entry> entries = new List<Entry>();

            foreach (WalletTransaction tx in walletManager.Transactions)
            {
                if (tx != null) entries.Add(FromTransaction(tx));
            }

            if (requestManager != null && requestManager.IsActive)
            {
                foreach (CreditRequest r in requestManager.Requests)
                {
                    // Approved requests are already shown through their ledger entry.
                    if (r != null && r.status != CreditRequestStatus.Approved) entries.Add(FromRequest(r));
                }
            }

            entries.Sort((a, b) => b.time.CompareTo(a.time)); // newest first
            int count = Math.Min(entries.Count, MaxRows);

            while (rowTexts.Count < count)
            {
                GameObject row = Instantiate(rowPrefab, rowContainer);
                rowBackgrounds.Add(row.GetComponent<Image>());
                rowTexts.Add(row.GetComponentInChildren<TMP_Text>(true));
            }

            for (int i = 0; i < rowTexts.Count; i++)
            {
                bool used = i < count;
                rowTexts[i].transform.parent.gameObject.SetActive(used);
                if (!used) continue;

                rowTexts[i].text = entries[i].text;
                if (rowBackgrounds[i] != null) rowBackgrounds[i].color = entries[i].tint;
            }

            emptyMessage.SetActive(count == 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowContainer);
        }

        private Entry FromTransaction(WalletTransaction tx)
        {
            string reference = tx.gameRoundId ?? string.Empty;
            bool isRequestRef = reference.StartsWith("REQ-", StringComparison.Ordinal);

            string refText = string.IsNullOrEmpty(reference)
                ? "-"
                : (isRequestRef ? "Request " : "Round ") + reference;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>{LabelFor(tx.type)}</b>   {FormatAmount(tx.amount)}");
            sb.AppendLine($"<size=70%>{tx.transactionId}  |  {refText}  |  COMPLETED");
            sb.AppendLine($"Balance: {Format(tx.balanceBefore)} -> {Format(tx.balanceAfter)}   |   {FormatTime(tx.timestampUtc)}");
            sb.Append(NoteFor(tx));

            return new Entry
            {
                time = ParseTime(tx.timestampUtc),
                text = sb.ToString(),
                tint = tx.amount > 0 ? gainTint : (tx.amount < 0 ? lossTint : neutralTint)
            };
        }

        private Entry FromRequest(CreditRequest r)
        {
            bool pending = r.status == CreditRequestStatus.Pending;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>{(pending ? "TEST CREDIT REQUEST" : "REJECTED REQUEST")}</b>   {Format(r.credits)} (not credited)");
            sb.AppendLine($"<size=70%>-  |  Request {r.requestId}  |  {r.status.ToString().ToUpperInvariant()}");
            sb.AppendLine($"Balance unchanged   |   {FormatTime(r.createdUtc)}");
            sb.Append(pending
                ? "Waiting for the test admin. No credits have been added."
                : $"Rejected {FormatTime(r.resolvedUtc)}: {r.rejectReason}");

            return new Entry
            {
                time = ParseTime(r.createdUtc),
                text = sb.ToString(),
                tint = pending ? pendingTint : lossTint
            };
        }

        private static string LabelFor(TransactionType type)
        {
            switch (type)
            {
                case TransactionType.StartingBalance: return "INITIAL BALANCE";
                case TransactionType.Bet: return "BET DEDUCTION";
                case TransactionType.Win: return "GAME WINNINGS";
                case TransactionType.Bonus: return "BONUS";
                case TransactionType.AdminTest: return "TEST ADJUSTMENT (DEV)";
                case TransactionType.Reset: return "BALANCE RESET";
                case TransactionType.Refund: return "BET REFUND";
                case TransactionType.TestCreditApproved: return "APPROVED TEST CREDIT";
                case TransactionType.TestAdjustment: return "TEST ADJUSTMENT";
                default: return type.ToString().ToUpperInvariant();
            }
        }

        private static string NoteFor(WalletTransaction tx)
        {
            switch (tx.type)
            {
                case TransactionType.Reset: return "Balance set back to the starting value. Earlier history is kept.";
                case TransactionType.TestCreditApproved: return "Credits added after the request was approved.";
                case TransactionType.Refund: return "Round cancelled: the bet was returned.";
                case TransactionType.StartingBalance: return "Starting test balance.";
                default: return " ";
            }
        }

        private static DateTime ParseTime(string isoUtc)
        {
            return DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime t)
                ? t
                : DateTime.MinValue;
        }

        private static string FormatTime(string isoUtc)
        {
            DateTime t = ParseTime(isoUtc);
            return t == DateTime.MinValue ? "-" : t.ToLocalTime().ToString("MMM d, HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private static string FormatAmount(long value) => value.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);

        private void Click()
        {
            if (audioManager != null) audioManager.PlaySfx(SfxType.ButtonClick);
        }
    }
}