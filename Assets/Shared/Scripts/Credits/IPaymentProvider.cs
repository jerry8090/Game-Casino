namespace Shared.Credits
{
    /// <summary>
    /// The seam for a FUTURE, separately reviewed payment integration.
    /// A real provider would live behind a backend and confirm payments server-side.
    /// It must never ask the player for a PIN, password or OTP inside Unity.
    /// </summary>
    public interface IPaymentProvider
    {
        string ProviderName { get; }
        bool IsSimulated { get; }

        /// <summary>Text shown to the player after a request is created.</summary>
        string GetInstructions(CreditRequest request);
    }

    /// <summary>Development provider. No payment is made and no receipt or confirmation is ever produced.</summary>
    public class MockPaymentProvider : IPaymentProvider
    {
        public string ProviderName => "Mock (simulated)";
        public bool IsSimulated => true;

        public string GetInstructions(CreditRequest request)
        {
            return "REQUEST CREATED (PENDING)\n" +
                   request.requestId + "\n" +
                   request.credits.ToString("N0") + " TEST CREDITS\n" +
                   "SIMULATION: no payment is made. Show this ID to the test admin to approve it.";
        }
    }
}