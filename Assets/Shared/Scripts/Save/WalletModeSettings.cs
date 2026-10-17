using UnityEngine;

namespace Shared.Save
{
    /// <summary>Which wallet the game uses. Virtual is the default and the original behavior.</summary>
    public enum WalletMode
    {
        Virtual = 0,
        Test = 1
    }

    /// <summary>
    /// Remembers the chosen mode on this device (PlayerPrefs). It holds no game data itself:
    /// SaveManager reads it to pick a separate save file for each mode.
    /// Only change the mode from the Main Menu, never during a round.
    /// </summary>
    public static class WalletModeSettings
    {
        private const string Key = "Shared.WalletMode";

        public static WalletMode Current =>
            PlayerPrefs.GetInt(Key, (int)WalletMode.Virtual) == (int)WalletMode.Test
                ? WalletMode.Test
                : WalletMode.Virtual;

        public static bool IsTest => Current == WalletMode.Test;

        public static void Set(WalletMode mode)
        {
            PlayerPrefs.SetInt(Key, (int)mode);
            PlayerPrefs.Save();
        }
    }
}