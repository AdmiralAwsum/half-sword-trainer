using System.Drawing;

namespace HalfSwordTrainer;

internal sealed class TrainerForm : Form
{
    private readonly Button attach = new ActionButton { Text = "LINK TO GAME", AccessibleName = "Attach to running game", AutoSize = true, Primary = true };
    private readonly Button stop = new ActionButton { Text = "DISARM ALL", AccessibleName = "STOP / Restore", AutoSize = true, Enabled = false };
    private readonly Label connection = new() { Text = "Not attached. Start a playable scene, then attach.", AutoSize = true };
    private readonly Label values = new() { Text = "Local session  /  Game build 0.7.2", AutoSize = true };
    private readonly Label badge = new() { Text = "DISCONNECTED", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
    private readonly VitalDisplay healthDisplay = new() { Text = "Health", Accent = Theme.Red };
    private readonly VitalDisplay staminaDisplay = new() { Text = "Stamina", Accent = Theme.Green };
    private readonly VitalDisplay consciousnessDisplay = new() { Text = "Consciousness", Accent = Theme.Gold };
    private readonly CheckBox invulnerable = Toggle("Invulnerable", "Let their blades find nothing.");
    private readonly CheckBox health = Toggle("Unlimited Health", "Keep flesh and bone at full health.");
    private readonly CheckBox stamina = Toggle("Unlimited Stamina", "An endless appetite for battle.");
    private readonly CheckBox consciousness = Toggle("Unlimited Consciousness", "Keep your wits. And your feet.");
    private readonly CheckBox pain = Toggle("No Ball Or Liver Pain", "Some lessons need not be learned.");
    private readonly CheckBox easyWin = Toggle("Easy Win", "Weaken foes. Deliver the final insult.", "Easy Win (weaken hostile NPCs)");
    private readonly CheckBox speed = Toggle("Game Speed", "Set the world's pace.");
    private readonly CheckBox muscle = Toggle("Muscle Power", "A less subtle argument.", "Muscle Power Value");
    private readonly NumericUpDown speedValue = Number(0.1m, 5, 1, 2, 0.1m);
    private readonly NumericUpDown muscleValue = Number(0, 10000, 35, 2, 5);
    private readonly NumericUpDown fundsValue = Number(0, 1000000000, 0, 0, 100);
    private readonly Button fundsButton = new ActionButton { Text = "SET", AccessibleName = "Set Funds", AutoSize = false, Enabled = false };
    private readonly Panel controls = new() { AutoSize = true, Dock = DockStyle.Fill };
    private volatile TrainerOptions options = new();
    private int pendingFunds = -1;
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private bool closing;
    private readonly bool autoAttach;

    public TrainerForm(bool autoAttach)
    {
        SuspendLayout();
        this.autoAttach = autoAttach;
        Text = "Half Sword | The Fool's Favour";
        Font = new Font("Segoe UI", 9);
        ClientSize = new Size(1160, 850);
        MinimumSize = new Size(1040, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Parchment;
        DoubleBuffered = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 18, 18, 10), ColumnCount = 1, RowCount = 3 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.Controls.Add(BuildHeader(), 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(new PortraitPanel(), 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var arsenal = Stack();
        arsenal.Controls.Add(Heading("VITAL SIGNS", "LIVE FROM THE ARENA"));
        var vitals = new TableLayoutPanel { Dock = DockStyle.Top, Height = 88, ColumnCount = 3, Margin = new Padding(0, 0, 0, 10) };
        foreach (var display in new[] { healthDisplay, staminaDisplay, consciousnessDisplay })
        {
            vitals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            vitals.Controls.Add(display);
        }
        arsenal.Controls.Add(vitals);
        var features = Stack();
        features.Controls.Add(Heading("SURVIVAL", "THE BODY IS BUT A SUGGESTION"));
        var toggles = new TableLayoutPanel { Dock = DockStyle.Top, Height = 222, ColumnCount = 2, RowCount = 3, Margin = Padding.Empty };
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++) toggles.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        foreach (var toggle in new[] { invulnerable, health, stamina, consciousness, pain, easyWin })
            toggles.Controls.Add(toggle);
        features.Controls.Add(toggles);
        features.Controls.Add(Heading("TIP THE SCALES", "A LITTLE OUTSIDE ASSISTANCE"));
        var modifiers = new TableLayoutPanel { Dock = DockStyle.Top, Height = 135, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        for (int i = 0; i < 3; i++) modifiers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        modifiers.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        modifiers.Controls.Add(ValueCard(speed, speedValue));
        modifiers.Controls.Add(ValueCard(muscle, muscleValue));
        modifiers.Controls.Add(ValueCard(null, fundsValue, fundsButton));
        features.Controls.Add(modifiers);
        controls.Controls.Add(features);
        controls.Dock = DockStyle.Top;
        arsenal.Controls.Add(controls);
        scroll.Controls.Add(arsenal);
        body.Controls.Add(scroll, 1, 0);
        root.Controls.Add(body, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);
        foreach (var button in new[] { attach, stop, fundsButton })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Theme.Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 65, 44);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(77, 80, 52);
            button.BackColor = Theme.Surface;
            button.ForeColor = Theme.Parchment;
            button.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            button.Padding = new Padding(12, 6, 12, 6);
            button.Cursor = Cursors.Hand;
        }
        attach.BackColor = Theme.Gold;
        attach.ForeColor = Theme.Background;
        attach.FlatAppearance.BorderColor = Theme.Gold;
        attach.FlatAppearance.MouseOverBackColor = Color.FromArgb(223, 195, 132);
        fundsButton.ForeColor = Theme.Gold;
        fundsButton.Padding = Padding.Empty;
        speedValue.AccessibleName = "Game Speed Value";
        muscleValue.AccessibleName = "Muscle Power Value";
        fundsValue.AccessibleName = "Funds Value";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        SetFeatureAvailability(false);
        ResumeLayout(true);
        foreach (var toggle in new[] { invulnerable, health, stamina, consciousness, pain, easyWin, speed, muscle })
            toggle.CheckedChanged += (_, _) => UpdateOptions();
        speedValue.ValueChanged += (_, _) => UpdateOptions();
        muscleValue.ValueChanged += (_, _) => UpdateOptions();
        attach.Click += async (_, _) => await AttachAsync();
        stop.Click += async (_, _) => await StopAsync();
        fundsButton.Click += (_, _) =>
        {
            Interlocked.Exchange(ref pendingFunds, decimal.ToInt32(fundsValue.Value));
            SetConnection("Funds write queued...", "APPLYING", Theme.Gold);
        };
        FormClosing += OnClosing;
        Shown += async (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            if (Height > area.Height) { Height = area.Height; Top = area.Top; }
            if (Width > area.Width) { Width = area.Width; Left = area.Left; }
            if (this.autoAttach) await AttachAsync();
        };
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.DarkTitleBar(Handle); }

    private static FeatureToggle Toggle(string text, string description, string? accessibleName = null) =>
        new() { Text = text, Description = description, AccessibleName = accessibleName ?? text };

    private static TableLayoutPanel Stack() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 1,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    private static Control Heading(string title, string detail)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 35, ColumnCount = 2, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Theme.Gold,
            Margin = Padding.Empty
        });
        panel.Controls.Add(new Label
        {
            Text = detail,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 0, 8, 0)
        });
        return panel;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        var brand = new Panel { Dock = DockStyle.Fill };
        brand.Controls.Add(new Label
        {
            Text = "THE FOOL'S FAVOUR  /  TRAINER",
            Location = new Point(1, 51),
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = Theme.Gold
        });
        brand.Controls.Add(new Label
        {
            Text = "HALF SWORD",
            Location = new Point(-3, 0),
            AutoSize = true,
            Font = new Font("Palatino Linotype", 28),
            ForeColor = Theme.Parchment
        });
        header.Controls.Add(brand, 0, 0);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        stop.Margin = new Padding(8, 17, 8, 0);
        attach.Margin = new Padding(8, 17, 0, 0);
        badge.Size = new Size(132, 28);
        badge.Margin = new Padding(0, 24, 8, 0);
        badge.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        badge.ForeColor = Theme.Muted;
        badge.BackColor = Theme.Surface;
        actions.Controls.AddRange([stop, attach, badge]);
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private static Control ValueCard(CheckBox? toggle, NumericUpDown number, Button? button = null)
    {
        var card = new SurfacePanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(10, 4, 10, 12) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        if (toggle is FeatureToggle feature)
        {
            feature.Compact = true;
            feature.Margin = Padding.Empty;
            layout.Controls.Add(feature, 0, 0);
        }
        else
        {
            var labels = new Panel { Dock = DockStyle.Fill };
            labels.Controls.Add(new Label { Text = "Funds", Location = new Point(5, 12), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) });
            labels.Controls.Add(new Label { Text = "Coin makes a fine argument.", Location = new Point(5, 38), AutoSize = true, Font = new Font("Segoe UI", 8.25f), ForeColor = Theme.Muted });
            layout.Controls.Add(labels, 0, 0);
        }
        var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = button == null ? 1 : 2, Margin = Padding.Empty };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        number.Dock = DockStyle.Fill;
        number.Margin = Padding.Empty;
        number.BackColor = Theme.Background;
        number.ForeColor = Theme.Parchment;
        number.BorderStyle = BorderStyle.FixedSingle;
        number.Font = new Font("Segoe UI", 11);
        number.TextAlign = HorizontalAlignment.Left;
        input.Controls.Add(number, 0, 0);
        if (button != null)
        {
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(5, 0, 0, 0);
            input.Controls.Add(button, 1, 0);
        }
        layout.Controls.Add(input, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 14, 8, 0) };
        footer.Paint += (_, e) => { using var pen = new Pen(Theme.Border); e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };
        connection.AutoSize = false;
        connection.Dock = DockStyle.Top;
        connection.Height = 25;
        connection.Padding = new Padding(0, 7, 0, 0);
        connection.Font = new Font("Segoe UI", 8.25f);
        connection.ForeColor = Theme.Muted;
        values.Dock = DockStyle.Bottom;
        values.Font = new Font("Segoe UI", 8.25f);
        values.ForeColor = Theme.Gold;
        footer.Controls.Add(values);
        footer.Controls.Add(connection);
        return footer;
    }

    private void SetConnection(string text, string state, Color color)
    {
        connection.Text = text;
        badge.Text = state;
        badge.ForeColor = color;
    }

    private void SetFeatureAvailability(bool available)
    {
        foreach (var toggle in new[] { invulnerable, health, stamina, consciousness, pain, easyWin, speed, muscle })
            toggle.Enabled = available;
        speedValue.Enabled = muscleValue.Enabled = fundsValue.Enabled = available;
        fundsButton.Enabled = available;
    }

    private void ShowStatus(TrainerStatus status)
    {
        SetConnection(status.Message + "  /  Match recovery active", "CONNECTED", Theme.Green);
        healthDisplay.Value = status.Health;
        staminaDisplay.Value = status.Stamina;
        consciousnessDisplay.Value = status.Consciousness;
        values.Text = $"Funds  {status.Funds:N0}     /     Muscle  {status.Muscle:F2}     /     World speed  {status.Speed:F2}x     /     Hostile targets  {status.Opponents}";
    }
    private static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals, decimal step) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        DecimalPlaces = decimals,
        Increment = step,
        ThousandsSeparator = true,
        Width = 165,
        Margin = new Padding(3, 5, 3, 5)
    };

    private void UpdateOptions() => options = new(invulnerable.Checked, health.Checked, stamina.Checked,
        consciousness.Checked, pain.Checked, easyWin.Checked, speed.Checked, (double)speedValue.Value,
        muscle.Checked, (double)muscleValue.Value);

    private async Task AttachAsync()
    {
        await StopAsync();
        attach.Enabled = false;
        SetConnection("Resolving and validating runtime properties...", "LINKING", Theme.Gold);
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        stop.Enabled = true;
        worker = Task.Run(async () =>
        {
            Trainer? trainer = null;
            string? backupPath = null;
            bool initialized = false;
            bool waiting = false;
            try
            {
                DateTime nextStatus = DateTime.MinValue;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (trainer == null)
                        {
                            trainer = new Trainer(Program.Log, backupPath);
                            var initial = trainer.Status();
                            bool firstAttachment = !initialized;
                            Post(() =>
                            {
                                SetFeatureAvailability(true);
                                if (firstAttachment)
                                {
                                    fundsValue.Value = Math.Clamp(initial.Funds, 0, 1000000000);
                                    muscleValue.Value = (decimal)Math.Clamp(initial.Muscle, 0, 10000);
                                    speedValue.Value = (decimal)Math.Clamp(initial.Speed, 0.1, 5);
                                }
                            });
                            initialized = true;
                            if (waiting) Program.Log($"Automatically reattached: {initial.Message}");
                            waiting = false;
                            nextStatus = DateTime.MinValue;
                        }
                        token.ThrowIfCancellationRequested();
                        trainer.Apply(options);
                        int funds = Interlocked.Exchange(ref pendingFunds, -1);
                        if (funds >= 0) trainer.SetFunds(funds);
                        if (DateTime.UtcNow >= nextStatus)
                        {
                            var status = trainer.Status();
                            Post(() =>
                            {
                                ShowStatus(status);
                            });
                            nextStatus = DateTime.UtcNow.AddMilliseconds(250);
                        }
                        await Task.Delay(15, token);
                    }
                    catch (GameTransitionException ex)
                    {
                        if (!waiting)
                        {
                            Program.Log($"Loading; automatic reattachment pending: {ex.Message}");
                            Post(() =>
                            {
                                SetConnection("Waiting for the next match. Selected features resume automatically.", "BETWEEN BOUTS", Theme.Gold);
                                values.Text = "Player unavailable  /  Game writes paused  /  Your selections are retained";
                                healthDisplay.Value = staminaDisplay.Value = consciousnessDisplay.Value = null;
                                fundsButton.Enabled = false;
                            });
                        }
                        waiting = true;
                        Interlocked.Exchange(ref pendingFunds, -1);
                        if (trainer != null)
                        {
                            backupPath = trainer.BackupPath;
                            try { trainer.Stop(); }
                            finally { trainer.Dispose(); trainer = null; }
                        }
                        await Task.Delay(500, token);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { Report(ex); }
            finally
            {
                if (trainer != null)
                {
                    try { trainer.Stop(); }
                    catch (Exception ex) { Report(ex); }
                    finally { trainer.Dispose(); }
                }
                Post(() => { SetFeatureAvailability(false); stop.Enabled = false; attach.Enabled = true; });
            }
        });
    }

    private async Task StopAsync()
    {
        cancellation?.Cancel();
        if (worker != null) await worker;
        cancellation?.Dispose();
        cancellation = null;
        worker = null;
        pendingFunds = -1;
        foreach (var toggle in new[] { invulnerable, health, stamina, consciousness, pain, easyWin, speed, muscle })
            toggle.Checked = false;
        SetConnection("All features off. Link to the game when you are ready.", "DISARMED", Theme.Muted);
        healthDisplay.Value = staminaDisplay.Value = consciousnessDisplay.Value = null;
        values.Text = "Local session  /  Game build 0.7.2  /  Saves backed up before the first write";
    }

    private void Post(Action action)
    {
        if (!IsDisposed && IsHandleCreated) BeginInvoke(action);
    }

    private void Report(Exception ex)
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "trainer-errors.log");
        string message = ex.Message;
        try { File.AppendAllText(logPath, $"{DateTime.Now:O} {ex}\n"); }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
        { message += $"\nCould not write error log: {logError.Message}"; }
        Post(() =>
        {
            SetConnection("Stopped: " + ex.Message, "LINK LOST", Theme.Red);
            MessageBox.Show(this, message + $"\n\nDetails: {logPath}", "Trainer stopped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        });
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        closing = true;
        Enabled = false;
        await StopAsync();
        Close();
    }
}
