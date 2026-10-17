using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shared.Credits
{
    /// <summary>One purchasable package of TEST credits. Credits are NOT pesos: set any number you like.</summary>
    [Serializable]
    public class CreditPackage
    {
        public string packageId = "TEST_100";
        [Tooltip("Optional. Leave empty to show 'N TEST CREDITS'.")]
        public string displayName;
        public long credits = 100;

        public string DisplayLabel =>
            string.IsNullOrWhiteSpace(displayName)
                ? credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " TEST CREDITS"
                : displayName;
    }

    /// <summary>
    /// The list of test credit packages. Create it with: right-click in Project >
    /// Create > Shared > Credit Package Config. Approval re-checks every request against this list.
    /// </summary>
    [CreateAssetMenu(fileName = "CreditPackageConfig", menuName = "Shared/Credit Package Config")]
    public class CreditPackageConfig : ScriptableObject
    {
        [SerializeField] private List<CreditPackage> packages = new List<CreditPackage>
        {
            new CreditPackage { packageId = "TEST_100",  credits = 100 },
            new CreditPackage { packageId = "TEST_500",  credits = 500 },
            new CreditPackage { packageId = "TEST_1000", credits = 1000 }
        };

        public IReadOnlyList<CreditPackage> Packages => packages;

        public bool TryGetPackage(string packageId, out CreditPackage package)
        {
            for (int i = 0; i < packages.Count; i++)
            {
                if (packages[i] != null && packages[i].packageId == packageId)
                {
                    package = packages[i];
                    return true;
                }
            }
            package = null;
            return false;
        }

        private void OnValidate()
        {
            foreach (CreditPackage p in packages)
            {
                if (p == null) continue;
                p.credits = Math.Max(1, p.credits);
            }
        }
    }
}