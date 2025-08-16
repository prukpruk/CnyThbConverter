using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CnyThbConverter
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient http = new HttpClient();
        private readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private bool _uiReady; // becomes true after Loaded

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            FromCode.ItemsSource = new[] { "CNY", "THB" };
            ToCode.ItemsSource   = new[] { "THB", "CNY" };
            FromCode.SelectedIndex = 0;
            ToCode.SelectedIndex   = 0;
            EnsureDifferentSides();
            _uiReady = true;
        }

        private void EnsureDifferentSides()
        {
            var from = FromCode.SelectedItem?.ToString();
            var to   = ToCode.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(from) && from == to)
                ToCode.SelectedItem = (from == "CNY") ? "THB" : "CNY";
        }

        private async Task ConvertAsync(CancellationToken ct = default)
        {
            if (!_uiReady) return; // don't run before Loaded
            ResultText.Text = "";
            StatusText.Text = "Fetching rate…";

            var amountText = InputAmount.Text?.Trim();
            if (string.IsNullOrEmpty(amountText) || !decimal.TryParse(amountText, out var amount) || amount < 0)
            {
                StatusText.Text = "Enter a valid amount.";
                return;
            }

            var from = FromCode.SelectedItem?.ToString();
            var to   = ToCode.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to)
            {
                StatusText.Text = "Select two different currencies.";
                return;
            }

            try
            {
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
            catch
            {
                StatusText.Text = "Failed to fetch rate. Check internet or try again.";
            }
        }

        private async void Convert_Click(object sender, RoutedEventArgs e) => await ConvertAsync();

        private async void Swap_Click(object sender, RoutedEventArgs e)
        {
            var from = FromCode.SelectedItem?.ToString();
            FromCode.SelectedItem = ToCode.SelectedItem;
            ToCode.SelectedItem = from;
            EnsureDifferentSides();
            await ConvertAsync();
        }

        // ---- Input validation (typing, keys, paste) ----
        private static readonly Regex NumericRegex = new Regex(@"^[0-9.]$", RegexOptions.Compiled);

        private void NumericOnly(object sender, TextCompositionEventArgs e)
        {
            // allow only digits or dot, and only one dot overall
            if (!NumericRegex.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }
            if (e.Text == "." && ((sender as System.Windows.Controls.TextBox)?.Text.Contains(".") ?? false))
                e.Handled = true;
        }

        private void Amount_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Permit control keys explicitly
            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right
                || e.Key == Key.Tab || e.Key == Key.Home || e.Key == Key.End)
            {
                e.Handled = false;
            }
        }

        private void Amount_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = (string)e.DataObject.GetData(DataFormats.Text)!;
                if (!decimal.TryParse(text, out _))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }
    }

    public sealed class FrankfurterLatest
    {
        public string Base { get; set; } = "";
        public string Date { get; set; } = "";
        public System.Collections.Generic.Dictionary<string, double> Rates { get; set; }
            = new System.Collections.Generic.Dictionary<string, double>();
    }
}
