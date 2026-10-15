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
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly string[] Fields = { "ip", "hostname", "city", "region", "country", "org" };

    readonly Dictionary<string, Label> values = new();
    readonly Button refreshButton = new();
    readonly Label status = new();

    public MainForm()
    {
        Text = "IP Info";
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
    }

    async Task LoadInfoAsync()
    {
        refreshButton.Enabled = false;
        status.ForeColor = Color.Gray;
        status.Text = "Загрузка...";

        try
        {
            string json = await Http.GetStringAsync("https://ipinfo.io/json");
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
        }
        catch (Exception ex)
        {
            status.ForeColor = Color.Firebrick;
            status.Text = "Ошибка: " + ex.Message;
        }
        finally
        {
            refreshButton.Enabled = true;
        }
    }
}
