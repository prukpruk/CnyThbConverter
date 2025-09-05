using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CnyThbConverter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Button starts disabled (also set in XAML)
            convertButton.IsEnabled = !string.IsNullOrWhiteSpace(inputTextBox.Text);
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            convertButton.IsEnabled = !string.IsNullOrWhiteSpace(inputTextBox.Text);
        }

        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                convertButton.IsEnabled = false;

                var raw = inputTextBox.Text?.Trim() ?? "";
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    MessageBox.Show("Please enter a valid number (e.g., 1234.56).");
                    return;
                }

                var rate = await GetCnyThbRateAsync(); // CNY -> THB
                var output = amount * rate;
                outputTextBox.Text = output.ToString("N2", CultureInfo.InvariantCulture);

                lastUpdatedText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                // Re-evaluate state after work finishes
                convertButton.IsEnabled = !string.IsNullOrWhiteSpace(inputTextBox.Text);
            }
        }

        private static async Task<decimal> GetCnyThbRateAsync()
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = "https://api.frankfurter.app/latest?from=CNY&to=THB";
            var resp = await http.GetFromJsonAsync<FrankResponse>(url);
            if (resp == null || !resp.rates.TryGetValue("THB", out var thb))
                throw new Exception("Could not retrieve CNY→THB rate.");
            return thb;
        }

        private sealed class FrankResponse
        {
            public Dictionary<string, decimal> rates { get; set; } = new();
        }
    }
}
