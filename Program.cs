using System.Net.NetworkInformation;
using System.Text.Json;

namespace IpInfo;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    static readonly string[] Fields = { "ip", "hostname", "city", "region", "country", "org" };

    readonly Dictionary<string, Label> values = new();
    readonly Button refreshButton = new();
    readonly Label status = new();

    CancellationTokenSource? cts;
    int networkChangeId;

    public MainForm()
    {
        Text = "IP Info";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        ClientSize = new Size(350, 270);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var table = new TableLayoutPanel
        {
            Location = new Point(10, 10),
            Size = new Size(330, 190),
            ColumnCount = 2,
            RowCount = Fields.Length
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var boldFont = new Font("Arial", 10, FontStyle.Bold);
        var normalFont = new Font("Arial", 10);

        for (int i = 0; i < Fields.Length; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            var name = new Label
            {
                Text = char.ToUpper(Fields[i][0]) + Fields[i][1..] + ":",
                Font = boldFont,
                AutoSize = true,
                Margin = new Padding(3, 5, 3, 5)
            };
            var value = new Label
            {
                Text = "...",
                Font = normalFont,
                AutoSize = true,
                MaximumSize = new Size(220, 0),
                Margin = new Padding(3, 5, 3, 5)
            };
            values[Fields[i]] = value;
            table.Controls.Add(name, 0, i);
            table.Controls.Add(value, 1, i);
        }

        refreshButton.Text = "Обновить";
        refreshButton.Location = new Point(10, 210);
        refreshButton.Size = new Size(110, 30);
        refreshButton.Click += async (_, _) => await LoadInfoAsync();

        status.Location = new Point(130, 217);
        status.AutoSize = true;
        status.ForeColor = Color.Gray;

        Controls.Add(table);
        Controls.Add(refreshButton);
        Controls.Add(status);

        Shown += async (_, _) => await LoadInfoAsync();

        // Автообновление при смене сети (включение/отключение VPN)
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        FormClosed += (_, _) =>
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            cts?.Cancel();
        };
    }

    void OnNetworkChanged(object? sender, EventArgs e)
    {
        // Событие приходит из фонового потока и может сработать несколько раз подряд
        if (!IsHandleCreated || IsDisposed) return;

        BeginInvoke(new Action(async () =>
        {
            int id = ++networkChangeId;
            await Task.Delay(2000);              // подождать, пока VPN поднимется
            if (id == networkChangeId && !IsDisposed)
                await LoadInfoAsync();           // обновить один раз, а не на каждое событие
        }));
    }

    async Task LoadInfoAsync()
    {
        cts?.Cancel();                           // отменить предыдущую незавершённую загрузку
        cts = new CancellationTokenSource();
        var token = cts.Token;

        status.ForeColor = Color.Gray;
        status.Text = "Загрузка...";

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                // Новый клиент на каждый запрос: нет старых соединений после смены VPN
                using var http = new HttpClient(new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(5)
                })
                { Timeout = TimeSpan.FromSeconds(8) };

                string json = await http.GetStringAsync("https://ipinfo.io/json", token);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                foreach (var field in Fields)
                {
                    string text = "N/A";
                    if (root.TryGetProperty(field, out var el) && el.ValueKind == JsonValueKind.String)
                        text = el.GetString() ?? "N/A";
                    values[field].Text = text;
                }

                status.Text = "Обновлено: " + DateTime.Now.ToLongTimeString();
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;                          // запущена более новая загрузка
            }
            catch (Exception ex)
            {
                if (attempt == 3)
                {
                    status.ForeColor = Color.Firebrick;
                    status.Text = "Ошибка: " + ex.Message;
                    return;
                }
                status.Text = $"Повтор ({attempt}/3)...";
                try { await Task.Delay(1500, token); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
