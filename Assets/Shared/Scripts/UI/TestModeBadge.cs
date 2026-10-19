using UnityEngine;
using Shared.Save;

namespace Shared.UI
{
    /// <summary>
    /// Shows a warning banner whenever Test Credits mode is active, and hides it in Virtual mode.
    /// Put this on an always-active empty object, and drag the banner object into Badge.
    /// </summary>
    public class TestModeBadge : MonoBehaviour
    {
        [SerializeField] private GameObject badge;

        private void Awake()
        {
            if (badge == null)
            {
                Debug.LogError("[TestModeBadge] Badge is not assigned in the Inspector.", this);
                return;
            }

            badge.SetActive(WalletModeSettings.IsTest);
        }
    }
}