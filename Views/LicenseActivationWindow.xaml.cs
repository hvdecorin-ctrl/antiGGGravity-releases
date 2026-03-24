using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using antiGGGravity.Utilities;

namespace antiGGGravity.Views
{
    public partial class LicenseActivationWindow : Window
    {
        public LicenseActivationWindow()
        {
            InitializeComponent();
            UI_Txt_HWID.Text = HardwareIdGenerator.GetHardwareId();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var result = LicenseValidator.GetCurrentStatus();

            if (result.IsValid && !result.IsTrial)
            {
                // Full License
                UI_Border_Status.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1E7DD"));
                UI_Border_Status.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BADBCC"));
                UI_Txt_StatusTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132"));
                UI_Txt_StatusMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#146C43"));
                
                UI_Txt_StatusTitle.Text = "License Active";
                UI_Txt_StatusMessage.Text = result.Message;
                
                UI_Btn_Activate.Content = "Update License";
            }
            else if (result.IsTrial)
            {
                // Trial Period
                UI_Border_Status.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3CD"));
                UI_Border_Status.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFECB5"));
                UI_Txt_StatusTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#664D03"));
                UI_Txt_StatusMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#856404"));
                
                UI_Txt_StatusTitle.Text = "Free Trial";
                UI_Txt_StatusMessage.Text = result.Message;
            }
            else
            {
                // Expired or Invalid
                UI_Border_Status.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8D7DA"));
                UI_Border_Status.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5C2C7"));
                UI_Txt_StatusTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#842029"));
                UI_Txt_StatusMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B02A37"));
                
                UI_Txt_StatusTitle.Text = "License Invalid or Expired";
                UI_Txt_StatusMessage.Text = result.Message;
            }
        }

        private void UI_Btn_CopyHwid_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(UI_Txt_HWID.Text);
                MessageBox.Show("Hardware ID copied to clipboard.\n\nPlease include this ID when requesting an activation key.", 
                    "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to copy to clipboard: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UI_Btn_Activate_Click(object sender, RoutedEventArgs e)
        {
            var key = UI_Txt_Key.Text.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Please enter an activation key.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hwid = UI_Txt_HWID.Text;
            var validation = LicenseCrypto.ValidateActivationKey(key, hwid);

            if (validation.IsValid)
            {
                LicenseStorage.SaveLicenseKey(key);
                LicenseValidator.InvalidateCache(); // clear cache so next check picks up new key
                UpdateStatus();
                
                MessageBox.Show($"Activation successful!\n\n{validation.Message}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            else
            {
                MessageBox.Show($"Activation failed:\n\n{validation.Message}", "Invalid Key", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UI_Txt_Key_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Optional: Auto-format XXXXX-XXXXX structure as they type (skipped for simplicity, but could add here)
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Allow dragging the window from the background
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}
