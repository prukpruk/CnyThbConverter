using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CnyThbConverter
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient http = new HttpClient();
        private readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public MainWindow()
        {
            InitializeComponent();
            FromCode.ItemsSource = new[] { "CNY", "THB" };
            ToCode.ItemsSource   = new[] { "THB", "CNY" };
            FromCode.SelectedIndex = 0; // CNY
            ToCode.SelectedIndex   = 0; // THB (we’ll auto-correct below)
            EnsureDifferentSides();
        }

        private void EnsureDifferentSides()
        {
            if (FromCode.SelectedItem?.ToString() == ToCode.SelectedItem?.ToString())
            {
                ToCode.SelectedItem = (FromCode.SelectedItem?.ToString() == "CNY") ? "THB" : "CNY";
            }
        }

        private async Task ConvertAsync(CancellationToken ct = default)
        {
            ResultText.Text = "";
            StatusText.Text = "Fetching rate…";

            if (!decimal.TryParse(InputAmount.Text, out var amount) || amount < 0)
            {
                StatusText.Text = "Enter a valid amount.";
                return;
            }

            var from = FromCode.SelectedItem?.ToString() ?? "CNY";
            var to   = ToCode.SelectedItem?.ToString()   ?? "THB";
            if (from == to)
            {
                StatusText.Text = "Currencies must be different.";
                return;
            }

            try
            {
                // Frankfurter endpoint: https://api.frankfurter.app/latest?from=CNY&to=THB
                var url = $"https://api.frankfurter.app/latest?from={from}&to={to}";
                using var resp = await http.GetAsync(url, ct);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<FrankfurterLatest>(json, jsonOpts);
                if (data?.Rates == null || !data.Rates.TryGetValue(to, out var rate))
                    throw new InvalidOperationException("No rate returned.");

                var converted = amount * (decimal)rate;
                ResultText.Text = $"{converted:0.####} {to}";
                StatusText.Text = $"1 {from} = {rate:0.####} {to} • {data.Date}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to fetch rate. Check internet or try again.";
                // Optionally log ex.Message
            }
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
            => await ConvertAsync();

        private void Swap_Click(object sender, RoutedEventArgs e)
        {
            var from = FromCode.SelectedItem?.ToString();
            FromCode.SelectedItem = ToCode.SelectedItem;
            ToCode.SelectedItem = from;
            _ = ConvertAsync(); // auto-recalculate
        }

        // Live update while typing (optional)
        private async void InputAmount_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => await ConvertAsync();

        // Allow only numeric and dot
        private void NumericOnly(object sender, System.Windows.Input.TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.]$");
    }

    public sealed class FrankfurterLatest
    {
        public string Base { get; set; } = "";
        public string Date { get; set; } = "";
        public System.Collections.Generic.Dictionary<string, double> Rates { get; set; }
            = new System.Collections.Generic.Dictionary<string, double>();
    }
}
