using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using antiGGGravity.Utilities;

namespace antiGGGravity.KeyGenUI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            CheckClipboard();
            InitializeSecurityStatus();
        }

        private void InitializeSecurityStatus()
        {
            try
            {
                // Try to load the key to verify environment
                var envKey = Environment.GetEnvironmentVariable("AGG_PRIVATE_KEY");
                if (string.IsNullOrEmpty(envKey))
                {
                    StatusCircle.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")); // Red
                    StatusText.Text = "Security Error: AGG_PRIVATE_KEY environment variable not found.";
                    BtnGenerate.IsEnabled = false;
                }
                else
                {
                    StatusCircle.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754")); // Green
                    StatusText.Text = "Security Ready: Private key loaded from environment.";
                }
            }
            catch (Exception ex)
            {
                StatusCircle.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                StatusText.Text = "Security Error: " + ex.Message;
                BtnGenerate.IsEnabled = false;
            }
        }

        private void CheckClipboard()
        {
            // Auto paste if clipboard contains what looks like a HWID
            try
            {
                var text = Clipboard.GetText()?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 20 && !text.Contains("-"))
                {
                    TxtHWID.Text = text;
                }
            }
            catch { }
        }

        private void Inputs_Changed(object sender, TextChangedEventArgs e)
        {
            if (BtnGenerate != null)
            {
                bool hasHwid = !string.IsNullOrWhiteSpace(TxtHWID.Text);
                bool hasDays = int.TryParse(TxtDays.Text, out _);
                BtnGenerate.IsEnabled = hasHwid && hasDays;
            }
        }

        private void BtnQuickDays_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            if (btn.Name == "Btn30Days") TxtDays.Text = "30";
            else if (btn.Name == "Btn999Days") TxtDays.Text = "9999";
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string hwid = TxtHWID.Text.Trim();
                if (!int.TryParse(TxtDays.Text.Trim(), out int days))
                {
                    MessageBox.Show("Invalid number of days", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var expiryDate = DateTime.UtcNow.AddDays(days);
                var key = LicenseCrypto.GenerateActivationKey(hwid, expiryDate);

                // Try verification round-trip
                var verify = LicenseCrypto.ValidateActivationKey(key, hwid);
                if (verify.IsValid)
                {
                    TxtResult.Text = key;
                    TxtResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754"));
                    
                    try 
                    { 
                        Clipboard.SetText(key); 
                        BtnGenerate.Content = "✓ Copied to Clipboard";
                        
                        // Reset button text after 2 seconds
                        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                        timer.Tick += (s, args) =>
                        {
                            BtnGenerate.Content = "Generate & Copy Key";
                            timer.Stop();
                        };
                        timer.Start();
                    }
                    catch 
                    { 
                        MessageBox.Show("Key generated but couldn't auto-copy to clipboard. You can highlight and copy it manually.", 
                            "Key Generated", MessageBoxButton.OK, MessageBoxImage.Information); 
                    }
                }
                else
                {
                    TxtResult.Text = "ROUNDTRIP VERIFY FAILED: " + verify.Message;
                    TxtResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                }
            }
            catch (Exception ex)
            {
                TxtResult.Text = "ERROR: " + ex.Message;
                TxtResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
            }
        }
    }
}
