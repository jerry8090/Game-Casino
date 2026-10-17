using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Shared.Audio;
using Shared.Credits;
using Shared.Save;

namespace Shared.UI
{
    /// <summary>
    /// The BUY CREDITS screen of the Main Menu (Test Credits mode only).
    /// The player picks a package and confirms: a PENDING request is created and the balance does not change.
    /// Put this on an always-active object, NOT on the panel. Implements IBackNavigable so Back closes it.
    /// </summary>
    public class BuyCreditsUI : MonoBehaviour, IBackNavigable
    {
        [Header("Required")]
        [SerializeField] private CreditRequestManager requestManager;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button packageButtonPrefab;
        [SerializeField] private Transform packageContainer;
        [SerializeField] private TMP_Text selectedText;
        [SerializeField] private TMP_Text infoText;

        [Header("Look")]
        [SerializeField] private float selectedScale = 1.06f;
        [SerializeField] private Color errorColor = new Color(1f, 0.48f, 0.48f, 1f);
        [SerializeField] private Color normalInfoColor = new Color(0.82f, 0.78f, 0.9f, 1f);

        [Header("Optional")]
        [SerializeField] private AudioManager audioManager;

        private readonly List<Button> packageButtons = new List<Button>();
        private readonly List<string> packageIds = new List<string>();
        private readonly IPaymentProvider paymentProvider = new MockPaymentProvider();
        private string selectedId;
        private bool isReady;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (requestManager == null || openButton == null || panel == null || closeButton == null ||
                confirmButton == null || packageButtonPrefab == null || packageContainer == null ||
                selectedText == null || infoText == null)
            {
                Debug.LogError("[BuyCreditsUI] A reference is missing in the Inspector.", this);
                return;
            }

            // The button only exists in Test Credits mode.
            openButton.gameObject.SetActive(WalletModeSettings.IsTest);

            BuildPackageButtons();

            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(HandleConfirm);

            panel.SetActive(false);
            isReady = true;
        }

        private void BuildPackageButtons()
        {
            CreditPackageConfig config = requestManager.Packages;
            if (config == null) return;

            foreach (CreditPackage package in config.Packages)
            {
                if (package == null) continue;

                Button button = Instantiate(packageButtonPrefab, packageContainer);
                button.name = "Package_" + package.packageId;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = package.DisplayLabel + "\n<size=55%>TEST CREDITS - NO REAL MONEY</size>";

                string id = package.packageId;
                button.onClick.AddListener(() => Select(id));

                packageButtons.Add(button);
                packageIds.Add(id);
            }
        }

        public void Open()
        {
            if (!isReady || IsOpen || !WalletModeSettings.IsTest) return;
            Click();

            selectedId = null;
            RefreshSelection();
            SetInfo(requestManager.PendingCount > 0
                ? $"Pick a package. You have {requestManager.PendingCount} pending request(s)."
                : "Pick a package.", false);

            panel.SetActive(true);
        }

        public void Close()
        {
            if (!isReady || !IsOpen) return;
            Click();
            panel.SetActive(false);
        }

        private void Select(string packageId)
        {
            selectedId = packageId;
            Click();
            RefreshSelection();
            SetInfo("Press CONFIRM REQUEST to create a pending request.", false);
        }

        private void HandleConfirm()
        {
            if (string.IsNullOrEmpty(selectedId)) return;

            if (requestManager.CreateRequest(selectedId, out CreditRequest request, out string error))
            {
                SetInfo(paymentProvider.GetInstructions(request), false);
                selectedId = null;
                RefreshSelection();
                selectedText.text = "REQUEST CREATED";
            }
            else
            {
                SetInfo(error, true);
            }
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < packageButtons.Count; i++)
            {
                bool isSelected = packageIds[i] == selectedId;
                packageButtons[i].transform.localScale = Vector3.one * (isSelected ? selectedScale : 1f);
            }

            confirmButton.interactable = !string.IsNullOrEmpty(selectedId);

            if (!string.IsNullOrEmpty(selectedId) &&
                requestManager.Packages.TryGetPackage(selectedId, out CreditPackage package))
            {
                selectedText.text = "SELECTED: " + package.DisplayLabel;
            }
            else
            {
                selectedText.text = "SELECTED: NONE";
            }
        }

        private void SetInfo(string message, bool isError)
        {
            infoText.text = message;
            infoText.color = isError ? errorColor : normalInfoColor;
        }

        private void Click()
        {
            if (audioManager != null) audioManager.PlaySfx(SfxType.ButtonClick);
        }
    }
}