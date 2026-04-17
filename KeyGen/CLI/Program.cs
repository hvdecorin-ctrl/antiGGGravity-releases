using System;
using System.Windows.Forms;
using antiGGGravity.Utilities;

namespace antiGGGravity.KeyGen
{
    /// <summary>
    /// Private key generator for antiGGGravity licensing.
    /// This app runs on YOUR machine only — never distribute it.
    ///
    /// Usage:
    ///   KeyGen.exe [HWID] [days]           Generate a key (default: 365 days)
    ///   KeyGen.exe --validate [HWID] [KEY]  Validate an existing key
    ///
    /// If HWID is omitted, it reads from clipboard.
    /// Generated keys are automatically copied to clipboard.
    /// </summary>
    class Program
    {
        private const int DefaultDays = 365;

        [STAThread]
        static int Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║   antiGGGravity — License Key Generator  ║");
            Console.WriteLine("║   PRIVATE — Do NOT distribute this app   ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            if (args.Length > 0 && args[0].Equals("--validate", StringComparison.OrdinalIgnoreCase))
            {
                return HandleValidate(args);
            }

            return HandleGenerate(args);
        }

        static int HandleGenerate(string[] args)
        {
            // Get HWID from args or clipboard
            string hwid;
            int days = DefaultDays;

            if (args.Length >= 1)
            {
                hwid = args[0].Trim();
                if (args.Length >= 2 && int.TryParse(args[1], out var d))
                    days = d;
            }
            else
            {
                // Try clipboard
                try
                {
                    hwid = Clipboard.GetText()?.Trim();
                }
                catch
                {
                    hwid = null;
                }

                if (string.IsNullOrEmpty(hwid))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Usage:  KeyGen.exe <HWID> [days=365]");
                    Console.WriteLine("        KeyGen.exe --validate <HWID> <KEY>");
                    Console.WriteLine();
                    Console.WriteLine("Tip: Copy the user's HWID to clipboard first, then just run KeyGen.exe");
                    Console.ResetColor();
                    return 1;
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"(HWID read from clipboard)");
                Console.ResetColor();

                // Ask for days
                Console.Write($"License duration in days [default: {DefaultDays}]: ");
                var input = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(input) && int.TryParse(input, out var d))
                    days = d;
            }

            // Validate HWID looks reasonable (should be 64-char hex from SHA256)
            if (hwid.Length < 10)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"WARNING: HWID seems too short ({hwid.Length} chars). Expected 64-char SHA256 hash.");
                Console.ResetColor();
                Console.Write("Continue anyway? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() != "y")
                    return 1;
            }

            var expiryDate = DateTime.UtcNow.AddDays(days);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("┌─── Input ───────────────────────────────────────────────────────────┐");
            Console.WriteLine($"│ HWID:    {Truncate(hwid, 60)}");
            Console.WriteLine($"│ Days:    {days}");
            Console.WriteLine($"│ Expires: {expiryDate:yyyy-MM-dd} UTC");
            Console.WriteLine("└─────────────────────────────────────────────────────────────────────┘");
            Console.ResetColor();

            // Generate the key
            var key = LicenseCrypto.GenerateActivationKey(hwid, expiryDate);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("┌─── Activation Key ──────────────────────────────────────────────────┐");
            Console.WriteLine($"│                                                                     │");
            Console.WriteLine($"│  {key,-67} │");
            Console.WriteLine($"│                                                                     │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────────────┘");
            Console.ResetColor();

            // Copy to clipboard
            try
            {
                Clipboard.SetText(key);
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("  ✓ Key copied to clipboard — paste it into an email to the user.");
                Console.ResetColor();
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ⚠ Could not copy to clipboard. Please copy manually.");
                Console.ResetColor();
            }

            // Verify round-trip
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Verifying key... ");
            var verify = LicenseCrypto.ValidateActivationKey(key, hwid);
            if (verify.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Valid (expires {verify.ExpiryDate:yyyy-MM-dd})");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ FAILED: {verify.Message}");
            }
            Console.ResetColor();

            Console.WriteLine();
            return 0;
        }

        static int HandleValidate(string[] args)
        {
            if (args.Length < 3)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Usage:  KeyGen.exe --validate <HWID> <KEY>");
                Console.ResetColor();
                return 1;
            }

            var hwid = args[1].Trim();
            var key = args[2].Trim();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"HWID: {Truncate(hwid, 60)}");
            Console.WriteLine($"Key:  {key}");
            Console.ResetColor();
            Console.WriteLine();

            var result = LicenseCrypto.ValidateActivationKey(key, hwid);

            if (result.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ VALID — Expires: {result.ExpiryDate:yyyy-MM-dd}");
            }
            else if (result.IsExpired)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ⚠ EXPIRED — Was valid until: {result.ExpiryDate:yyyy-MM-dd}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ INVALID — {result.Message}");
            }
            Console.ResetColor();

            Console.WriteLine();
            return result.IsValid ? 0 : 1;
        }

        static string Truncate(string s, int max)
        {
            if (s.Length <= max) return s;
            return s.Substring(0, max - 3) + "...";
        }
    }
}
