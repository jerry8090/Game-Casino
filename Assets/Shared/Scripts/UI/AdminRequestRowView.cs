using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    /// <summary>One row of the admin list. Display and clicks only: the manager decides everything.</summary>
    public class AdminRequestRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button approveButton;
        [SerializeField] private Button rejectButton;

        public event Action<string> OnApprove;
        public event Action<string> OnReject;

        private string requestId;

        private void Awake()
        {
            if (background == null || infoText == null || approveButton == null || rejectButton == null)
            {
                Debug.LogError("[AdminRequestRowView] A reference is missing on the AdminRequestRow prefab.", this);
                return;
            }

            approveButton.onClick.AddListener(() => OnApprove?.Invoke(requestId));
            rejectButton.onClick.AddListener(() => OnReject?.Invoke(requestId));
        }

        public void Bind(string id, string info, Color tint, bool isPending)
        {
            requestId = id;
            infoText.text = info;
            background.color = tint;
            approveButton.gameObject.SetActive(isPending);
            rejectButton.gameObject.SetActive(isPending);
        }
    }
}