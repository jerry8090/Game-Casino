using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Shared.Audio;
using Shared.Save;

namespace Shared.UI
{
    /// <summary>
    /// The MODE button in the Main Menu settings. Toggles Virtual Credits and Test Credits.
    /// It saves the current wallet, changes the mode, then reloads this scene so every manager
    /// loads the other save file from scratch. It lives only in the Main Menu, so a round can never be running.
    /// Put this on an always-active object, NOT on the button or the settings panel.
    /// </summary>
    public class WalletModeSelectorUI : MonoBehaviour
    {
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        [Header("Optional")]
        [SerializeField] private AudioManager audioManager;

        private bool isReady;
        private bool isSwitching;

        private void Awake()
        {
            if (saveManager == null || button == null || label == null)
            {
                Debug.LogError("[WalletModeSelectorUI] A reference is missing in the Inspector " +
                               "(Save Manager, Button, Label).", this);
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClicked);
            isReady = true;
            Refresh();
        }

        private void HandleClicked()
        {
            if (!isReady || isSwitching) return;
            isSwitching = true;

            if (audioManager != null) audioManager.PlaySfx(SfxType.ButtonClick);

            // Write the current mode's wallet to its own file before leaving it.
            if (saveManager.IsReady) saveManager.SaveGame();

            WalletMode next = WalletModeSettings.IsTest ? WalletMode.Virtual : WalletMode.Test;
            WalletModeSettings.Set(next);
            Debug.Log("[WalletModeSelectorUI] Mode changed to " + next + ". Reloading the scene.");

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void Refresh()
        {
            label.text = WalletModeSettings.IsTest ? "MODE: TEST CREDITS" : "MODE: VIRTUAL CREDITS";
        }
    }
}