using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;

namespace CnyThbConverter
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _http = new HttpClient();
        private string _from = "CNY";
        private string _to = "THB";

        public MainWindow()
        {
            InitializeComponent();
            UpdateLabels();
        }

        private async Task<decimal?> FetchRateAsync(string from, string to)
        {
            var url = $"https://api.exchangerate.host/latest?base={Uri.EscapeDataString(from)}&symbols={Uri.EscapeDataString(to)}";

            try
            {
                using var resp = await _http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();

                var data = JsonSerializer.Deserialize<ExchangeResponse>(json);
                if (data?.Rates is not null && data.Rates.TryGetValue(to, out var rate))
                {
                    return rate;
                }
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch rate.\n{ex.Message}", "Network Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDecimal(FromAmountBox.Text, out var amount))
            {
                MessageBox.Show("Please enter a valid number for the amount.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConvertButton.IsEnabled = false;
            ConvertButton.Content = "Converting…";

            var rate = await FetchRateAsync(_from, _to);
            if (rate is null)
            {
                ConvertButton.IsEnabled = true;
                ConvertButton.Content = "Convert";
                return;
            }

            var result = amount * rate.Value;
            ToAmountBox.Text = result.ToString("0.########", CultureInfo.InvariantCulture);

            ConvertButton.IsEnabled = true;
            ConvertButton.Content = "Convert";
        }

        private void SwitchButton_Click(object sender, RoutedEventArgs e)
        {
            (_from, _to) = (_to, _from);
            UpdateLabels();

            if (decimal.TryParse(ToAmountBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var toVal))
            {
                FromAmountBox.Text = toVal.ToString("0.########", CultureInfo.InvariantCulture);
                ToAmountBox.Text = string.Empty;
            }
        }

        private void UpdateLabels()
        {
            FromLabel.Text = $"From ({_from})";
            ToLabel.Text = $"To ({_to})";
            Title = $"{_from} ⇄ {_to} Converter";
        }

        private static bool TryParseDecimal(string? s, out decimal value)
        {
            if (s is null) { value = 0; return false; }
            s = s.Trim().Replace(',', '.');
            return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var ch = e.Text;
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(ch, @"^[0-9\.\,]+$");
        }
        private void NumberOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var text = (string)e.DataObject.GetData(typeof(string))!;
                if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9\.\,]+$"))
                    e.CancelCommand();
            }
            else e.CancelCommand();
        }
    }

    public sealed class ExchangeResponse
    {
        [JsonPropertyName("base")]
        public string? Base { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
