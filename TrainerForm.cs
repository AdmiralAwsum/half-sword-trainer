using System.Drawing;

namespace HalfSwordTrainer;

internal sealed class TrainerForm : Form
{
    private readonly Button attach = new ActionButton { Text = "LINK TO GAME", AccessibleName = "Attach to running game", AutoSize = true, Primary = true };
    private readonly ActionButton forceWinButton = new()
    {
        Text = "UNSTICK MATCH",
        AccessibleName = "Force Win / Unstick Match",
        AccessibleDescription = "Resets stuck death flags, finishes hostile characters, and marks the active match won. This cannot be undone.",
        AutoSize = false,
        Enabled = false,
        Danger = true
    };
    private readonly Button stop = new ActionButton { Text = "DISARM ALL", AccessibleName = "STOP / Restore", AutoSize = true, Enabled = false };
    private readonly Label connection = new() { Text = "Choose your options. Waiting for the game...", AutoSize = true };
    private readonly Label values = new() { Text = "Local session  /  Half Sword 0.7 Hotfix 4  /  Steam build 26820056", AutoSize = true };
    private readonly Label badge = new() { Text = "DISCONNECTED", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
    private readonly VitalDisplay healthDisplay = new() { Text = "Health", Accent = Theme.Red };
    private readonly VitalDisplay staminaDisplay = new() { Text = "Stamina", Accent = Theme.Green };
    private readonly VitalDisplay consciousnessDisplay = new() { Text = "Consciousness", Accent = Theme.Gold };
    private readonly CheckBox invulnerable = Toggle("Invulnerability", "Enables the game's built-in protection.");
    private readonly CheckBox health = Toggle("Refill Health", "Refills body health and clears bleeding.");
    private readonly CheckBox stamina = Toggle("Refill Stamina", "Keeps stamina at 100.");
    private readonly CheckBox consciousness = Toggle("Stay Conscious", "Refills consciousness and its limits.");
    private readonly CheckBox pain = Toggle("Clear Groin / Liver Pain", "Clears groin and liver pain.");
    private readonly CheckBox easyWin = Toggle("Weaken Enemies", "Reduces hostile health, stamina and consciousness.");
    private readonly CheckBox exhaustion = Toggle("No Exhaustion", "Keeps exhaustion at zero.");
    private readonly CheckBox temperature = Toggle("Normal Temperature", "Keeps body temperature at the game's neutral value.");
    private readonly CheckBox speed = Toggle("Game Speed", "Slows or speeds up the whole game.");
    private readonly CheckBox strength = Toggle("Super Strength", "Body, swing & grip force (x).");
    private readonly CheckBox strongLegs = Toggle("Strong Leg Joints", "Blocks leg dislocation & reinforces legs.");
    private readonly CheckBox reputation = Toggle("Minimum Rank Reputation", "Raises current rank reputation; never lowers it.");
    private readonly NumericUpDown speedValue = Number(0.1m, 5, 1, 2, 0.1m);
    private readonly NumericUpDown strengthValue = Number(1, 50, 3, 1, 0.5m);
    private readonly NumericUpDown fundsValue = Number(0, 1000000000, 0, 0, 100);
    private readonly NumericUpDown reputationValue = Number(0, 1000000000, 1500, 0, 50);
    private readonly NumericUpDown physiqueValue = Number(0, 10, 0.5m, 2, 0.25m);
    private readonly Button fundsButton = new ActionButton { Text = "SET", AccessibleName = "Set Funds", AutoSize = false, Enabled = false };
    private readonly Button physiqueButton = new ActionButton
    {
        Text = "SET", AccessibleName = "Set career body build for next spawn", AutoSize = false, Enabled = false,
        AccessibleDescription = "Apply outside a fight in career mode. Changes body build and mass on the next spawn and may persist in normal saves."
    };
    private readonly Label physiqueHint = new()
    {
        Text = "Career body build unavailable. Link to the game and leave the fight to apply.",
        Dock = DockStyle.Top, Height = 28, ForeColor = Theme.Muted, Padding = new Padding(0, 5, 0, 0)
    };
    private readonly Button repairSaveButton = new ActionButton
    {
        Text = "REPAIR SAVE BLOAT", AccessibleName = "Repair save bloat (game closed)",
        AccessibleDescription = "Backs up GameProgress.sav and clears stored dead-opponent history only. Requires Half Sword to be fully closed.",
        AutoSize = false
    };
    private readonly Label repairSaveHint = new()
    {
        Text = "Close Half Sword first. Clears stored dead-opponent history only; a fresh backup is required.",
        Dock = DockStyle.Fill, ForeColor = Theme.Muted, Padding = new Padding(0, 3, 0, 0)
    };
    private readonly Panel controls = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly ToolTip optionTips = new();
    private volatile TrainerOptions options = new();
    private int pendingFunds = -1;
    private int pendingForceWin;
    private double pendingPhysique = -1;
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private bool closing;
    private bool repairingSave;

    public TrainerForm()
    {
        SuspendLayout();
        Text = "Half Sword | The Fool's Favour";
        using (var stream = typeof(TrainerForm).Assembly.GetManifestResourceStream("HalfSwordTrainer.Assets.FoolsFavour.ico")
            ?? throw new InvalidOperationException("Application icon resource is missing."))
        using (var icon = new Icon(stream)) Icon = (Icon)icon.Clone();
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
        arsenal.Controls.Add(Heading("VITAL SIGNS", "LIVE FROM THE ARENA", forceWinButton));
        var vitals = new TableLayoutPanel { Dock = DockStyle.Top, Height = 88, ColumnCount = 3, Margin = new Padding(0, 0, 0, 10) };
        foreach (var display in new[] { healthDisplay, staminaDisplay, consciousnessDisplay })
        {
            vitals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            vitals.Controls.Add(display);
        }
        arsenal.Controls.Add(vitals);
        var features = Stack();
        features.Controls.Add(Heading("SURVIVAL AND COMBAT", "APPLIES TO THE CURRENT FIGHT"));
        var toggles = new TableLayoutPanel { Dock = DockStyle.Top, Height = 450, ColumnCount = 2, RowCount = 5, Margin = Padding.Empty };
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 5; i++) toggles.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        foreach (var toggle in new[] { invulnerable, health, stamina, consciousness, pain, easyWin, exhaustion, temperature })
            toggles.Controls.Add(toggle);
        toggles.Controls.Add(strongLegs, 0, 4);
        toggles.SetColumnSpan(strongLegs, 2);
        features.Controls.Add(toggles);
        features.Controls.Add(Heading("GAME AND PLAYER", "CHANGE VALUES WHILE PLAYING"));
        var modifiers = new TableLayoutPanel { Dock = DockStyle.Top, Height = 165, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        for (int i = 0; i < 3; i++) modifiers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        modifiers.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        modifiers.Controls.Add(ValueCard(speed, speedValue));
        modifiers.Controls.Add(ValueCard(strength, strengthValue));
        modifiers.Controls.Add(ValueCard(null, fundsValue, fundsButton));
        features.Controls.Add(modifiers);
        features.Controls.Add(Heading("CAREER BODY BUILD", "SET IN MENU / NEXT SPAWN"));
        var physique = new TableLayoutPanel { Dock = DockStyle.Top, Height = 135, ColumnCount = 1, Margin = Padding.Empty };
        physique.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        physique.Controls.Add(ValueCard(null, physiqueValue, physiqueButton, "Body build",
            "0 lean / 0.5 default / 1 max normal / 2-10 overpowered. Changes mass & strength."));
        features.Controls.Add(physique);
        features.Controls.Add(physiqueHint);
        features.Controls.Add(Heading("CAREER REPUTATION", "MINIMUM FOR THE CURRENT RANK"));
        var progression = new TableLayoutPanel { Dock = DockStyle.Top, Height = 165, ColumnCount = 1, Margin = Padding.Empty };
        progression.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progression.Controls.Add(ValueCard(reputation, reputationValue));
        features.Controls.Add(progression);
        features.Controls.Add(Heading("SAVE MAINTENANCE", "GAME CLOSED / BACKUP REQUIRED"));
        var maintenance = new SurfacePanel
        {
            Dock = DockStyle.Top, Height = 116, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(12)
        };
        var maintenanceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
        };
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        maintenanceLayout.Controls.Add(repairSaveHint, 0, 0);
        repairSaveButton.Dock = DockStyle.Right;
        repairSaveButton.Width = 210;
        repairSaveButton.Margin = Padding.Empty;
        maintenanceLayout.Controls.Add(repairSaveButton, 0, 1);
        maintenance.Controls.Add(maintenanceLayout);
        features.Controls.Add(maintenance);
        controls.Controls.Add(features);
        controls.Dock = DockStyle.Top;
        arsenal.Controls.Add(controls);
        scroll.Controls.Add(arsenal);
        body.Controls.Add(scroll, 1, 0);
        root.Controls.Add(body, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);
        foreach (var button in new[] { attach, stop, fundsButton, forceWinButton, physiqueButton, repairSaveButton })
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
        physiqueButton.ForeColor = Theme.Gold;
        physiqueButton.Padding = Padding.Empty;
        speedValue.AccessibleName = "Game Speed Value";
        strengthValue.AccessibleName = "Super Strength Multiplier";
        strengthValue.AccessibleDescription = "Multiplies physical-animation joint stiffness, angular drive, max linear/angular force, and grab force. Default 3x (range 1-50x). Extreme values may destabilize physics.";
        fundsValue.AccessibleName = "Funds Value";
        reputationValue.AccessibleName = "Minimum Rank Reputation Value";
        physiqueValue.AccessibleName = "Career body build value";
        foreach (var feature in FeatureToggles.OfType<FeatureToggle>())
            optionTips.SetToolTip(feature, feature.AccessibleDescription);
        SetFeatureHelp(invulnerable, "Continuously enables the game's built-in invulnerability for your fighter.");
        SetFeatureHelp(health, "Continuously refills general and body-part health and clears bleeding. Cannot resurrect or reattach limbs.");
        SetFeatureHelp(consciousness, "Continuously refills consciousness, its cap, and leg consciousness to 100.");
        SetFeatureHelp(pain, "Continuously clears the game's groin and liver pain values.");
        SetFeatureHelp(strongLegs, "Sets player-only Bone Snapping to 0 to prevent hit-triggered leg/joint breaks (enemies unaffected), clears leg dislocation state, strain, and leg pain, and boosts leg joint stiffness (2.5x tonus). Enable before taking damage; cannot repair an already-broken joint.");
        SetFeatureHelp(easyWin, "Weakens living hostile fighters to at most 1 health and consciousness and 0 stamina. Finish them normally. Does not grant victory directly.");
        SetFeatureHelp(temperature, "Holds body temperature at 1, the game's neutral default. Does not change equipment insulation.");
        SetFeatureHelp(speed, "Holds overall game speed at the selected multiplier. Extreme values can destabilize physics.");
        SetFeatureHelp(strength, "Multiplies physical-animation orientation/angular drive, max linear/angular force, and grab strength across your body, and removes armor weight penalties. Default 3x (1-50x). Extreme values can destabilize physics.");
        SetFeatureHelp(reputation, "Raises current rank reputation to at least the selected value; never lowers it. Events may need to refresh normally.");
        optionTips.SetToolTip(forceWinButton, forceWinButton.AccessibleDescription);
        optionTips.SetToolTip(repairSaveButton, repairSaveButton.AccessibleDescription);
        optionTips.SetToolTip(physiqueButton, physiqueButton.AccessibleDescription);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        SetFeatureAvailability(false);
        ResumeLayout(true);
        foreach (var toggle in FeatureToggles)
            toggle.CheckedChanged += (_, _) => UpdateOptions();
        speedValue.ValueChanged += (_, _) => UpdateOptions();
        strengthValue.ValueChanged += (_, _) => UpdateOptions();
        reputationValue.ValueChanged += (_, _) => UpdateOptions();
        attach.Click += async (_, _) => await AttachAsync();
        stop.Click += async (_, _) => await StopAsync();
        repairSaveButton.Click += async (_, _) => await RepairSaveAsync();
        fundsButton.Click += (_, _) =>
        {
            Interlocked.Exchange(ref pendingFunds, decimal.ToInt32(fundsValue.Value));
            SetConnection("Funds write queued...", "APPLYING", Theme.Gold);
        };
        physiqueButton.Click += (_, _) =>
        {
            Interlocked.Exchange(ref pendingPhysique, (double)physiqueValue.Value);
            physiqueButton.Enabled = false;
            physiqueHint.Text = "Career body build write queued. Waiting for confirmation...";
        };
        forceWinButton.Click += (_, _) =>
        {
            Interlocked.Exchange(ref pendingForceWin, 1);
            forceWinButton.Enabled = false;
            SetConnection("Unsticking the current match...", "APPLYING", Theme.Gold);
        };
        FormClosing += OnClosing;
        Shown += async (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            if (Height > area.Height) { Height = area.Height; Top = area.Top; }
            if (Width > area.Width) { Width = area.Width; Left = area.Left; }
            await AttachAsync();
        };
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.DarkTitleBar(Handle); }

    private static FeatureToggle Toggle(string text, string description) =>
        new() { Text = text, Description = description, AccessibleName = text, AccessibleDescription = description };

    private void SetFeatureHelp(CheckBox feature, string description)
    {
        feature.AccessibleDescription = description;
        optionTips.SetToolTip(feature, description);
    }

    private static TableLayoutPanel Stack() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 1,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    private static Control Heading(string title, string detail, Control? action = null)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = action == null ? 35 : 42,
            ColumnCount = action == null ? 2 : 3,
            Margin = Padding.Empty,
            AccessibleName = action == null ? null : "Combat match actions"
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, action == null ? 45 : 38));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, action == null ? 55 : 40));
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
        if (action != null)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            action.Dock = DockStyle.Fill;
            action.Margin = new Padding(8, 4, 8, 6);
            panel.Controls.Add(action, 2, 0);
        }
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

    private static Control ValueCard(CheckBox? toggle, NumericUpDown number, Button? button = null,
        string title = "Funds", string description = "Set your current money.")
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
            labels.Controls.Add(new Label { Text = title, Location = new Point(5, 12), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) });
            labels.Controls.Add(new Label { Text = description, Location = new Point(5, 38), AutoSize = true, Font = new Font("Segoe UI", 8.25f), ForeColor = Theme.Muted });
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
        fundsButton.Enabled = available;
        if (!available)
        {
            forceWinButton.Enabled = false;
            physiqueButton.Enabled = false;
            physiqueHint.Text = "Career body build unavailable. Link to the game and leave the fight to apply.";
        }
    }

    private void ShowWaitingState(bool absent, string state)
    {
        SetConnection(absent ? "Waiting for Half Sword. Your selections will resume when you play." :
            "Loading the next scene. Selected features resume automatically.", state, Theme.Gold);
        values.Text = "Player unavailable  /  Game writes paused  /  Your selections are retained";
        healthDisplay.Value = staminaDisplay.Value = consciousnessDisplay.Value = null;
        SetFeatureAvailability(false);
    }

    private CheckBox[] FeatureToggles => [invulnerable, health, stamina, consciousness, pain, strongLegs, easyWin,
        speed, strength, exhaustion, temperature, reputation];

    private void ShowStatus(TrainerStatus status)
    {
        SetConnection(status.Message, status.CombatReady ? "IN THE ARENA" : "OPTIONS ARMED", Theme.Green);
        forceWinButton.Enabled = status.CombatReady;
        physiqueButton.Enabled = status.PhysiqueEditable && !status.CombatReady;
        physiqueHint.Text = $"Career build: {Format(status.CareerPhysique)}. " +
            (physiqueButton.Enabled ? "Set here, then spawn into a new match." : "Apply from a career menu outside a fight.");
        healthDisplay.Value = status.Health;
        staminaDisplay.Value = status.Stamina;
        consciousnessDisplay.Value = status.Consciousness;
        values.Text = $"Funds {status.Funds:N0}  /  Rank rep {status.RankReputation:N0}  /  Exhaustion {Format(status.Exhaustion)}  /  Temp {Format(status.Temperature)}  /  Speed {Format(status.Speed)}x";
    }
    private static string Format(double? value) => value?.ToString("F2") ?? "--";
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
        strength.Checked, (double)strengthValue.Value, strongLegs.Checked, exhaustion.Checked, temperature.Checked,
        reputation.Checked, decimal.ToInt32(reputationValue.Value));

    private async Task RepairSaveAsync()
    {
        if (repairingSave) return;
        repairingSave = true;
        repairSaveButton.Enabled = false;
        try
        {
            await StopAsync(false);
            attach.Enabled = false;
            SaveRepair.EnsureGameClosed();
            var info = await Task.Run(() => SaveRepair.Inspect(SaveRepair.SavePath));
            if (info.DeadOpponents == 0)
            {
                repairSaveHint.Text = $"Save {info.FileBytes / 1048576d:F2} MiB. Dead Opponents is already empty; no repair needed.";
                MessageBox.Show(this, repairSaveHint.Text, "Save repair", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this,
                $"GameProgress.sav: {info.FileBytes / 1048576d:F2} MiB\n" +
                $"Stored dead opponents: {info.DeadOpponents:N0}\n" +
                $"Expected repaired size: {info.RepairedBytes / 1048576d:F2} MiB\n\n" +
                "This removes the stored dead-opponent history. Game systems using that history may be affected.\n" +
                "All other saved data will be preserved byte-for-byte, with a fresh verified backup before replacement.\n\n" +
                "Keep Half Sword closed until repair finishes. Continue?",
                "Repair save bloat", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            repairSaveHint.Text = "Backing up, repairing and verifying the save. Keep Half Sword closed...";
            var result = await Task.Run(SaveRepair.Repair);
            repairSaveHint.Text = result.BackupPath == null ? "Dead Opponents is already empty; no file was changed." :
                $"Removed {result.Before.DeadOpponents:N0} stored opponents. " +
                $"{result.Before.FileBytes / 1048576d:F2} -> {result.Before.RepairedBytes / 1048576d:F2} MiB. Verified.";
            MessageBox.Show(this, repairSaveHint.Text +
                (result.BackupPath == null ? "" : $"\n\nBackup:\n{result.BackupPath}") +
                "\n\nYou may now start Half Sword. LINK TO GAME resumes your staged trainer options.",
                "Save repair complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            repairSaveHint.Text = "Save repair failed. See the error details before retrying.";
            Report(ex);
        }
        finally
        {
            repairingSave = false;
            repairSaveButton.Enabled = true;
            attach.Enabled = cancellation == null;
        }
    }

    private async Task AttachAsync()
    {
        await StopAsync(false);
        attach.Enabled = false;
        SetConnection("Resolving and validating runtime properties...", "LINKING", Theme.Gold);
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        stop.Enabled = true;
        worker = Task.Run(async () =>
        {
            Trainer? trainer = null;
            string? backupPath = null;
            string? waitingState = null;
            GameProcessIdentity? previousProcess = null;
            try
            {
                DateTime nextStatus = DateTime.MinValue;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (trainer == null)
                        {
                            trainer = new Trainer(Program.Log, backupPath, previousProcess);
                            if (previousProcess.HasValue && previousProcess != trainer.ProcessIdentity)
                            {
                                trainer.Dispose();
                                trainer = null;
                                backupPath = null;
                                trainer = new Trainer(Program.Log);
                            }
                            var initial = trainer.Status();
                            previousProcess = trainer.ProcessIdentity;
                            Post(() => SetFeatureAvailability(true));
                            if (waitingState != null) Program.Log($"Automatically reattached: {initial.Message}");
                            waitingState = null;
                            nextStatus = DateTime.MinValue;
                        }
                        token.ThrowIfCancellationRequested();
                        trainer.Apply(options);
                        int funds = Interlocked.Exchange(ref pendingFunds, -1);
                        if (funds >= 0) trainer.SetFunds(funds);
                        double physique = Interlocked.Exchange(ref pendingPhysique, -1);
                        if (physique >= 0)
                        {
                            trainer.SetCareerPhysique(physique);
                            nextStatus = DateTime.MinValue;
                        }
                        if (Interlocked.Exchange(ref pendingForceWin, 0) == 1)
                        {
                            trainer.ForceWin();
                            nextStatus = DateTime.MinValue;
                        }
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
                    catch (Exception ex) when (ex is GameTransitionException or GameUnavailableException)
                    {
                        bool absent = ex is GameUnavailableException;
                        string state = absent ? "WAITING FOR GAME" : "BETWEEN BOUTS";
                        if (waitingState != state)
                        {
                            Program.Log($"Automatic reattachment pending: {ex.Message}");
                            Post(() => ShowWaitingState(absent, state));
                        }
                        waitingState = state;
                        Interlocked.Exchange(ref pendingFunds, -1);
                        Interlocked.Exchange(ref pendingForceWin, 0);
                        if (Interlocked.Exchange(ref pendingPhysique, -1) >= 0)
                            Program.Log("Queued career physique cancelled by a scene transition. Apply again from a career menu.");
                        if (trainer != null)
                        {
                            backupPath = trainer.BackupPath;
                            try { trainer.Stop(); }
                            finally { trainer.Dispose(); trainer = null; }
                        }
                        if (absent) backupPath = null;
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
                Post(() => { SetFeatureAvailability(false); stop.Enabled = false; attach.Enabled = !repairingSave; });
            }
        });
    }

    private async Task StopAsync(bool disarm = true)
    {
        cancellation?.Cancel();
        if (worker != null) await worker;
        cancellation?.Dispose();
        cancellation = null;
        worker = null;
        pendingFunds = -1;
        pendingForceWin = 0;
        pendingPhysique = -1;
        SetFeatureAvailability(false);
        if (disarm)
            foreach (var toggle in FeatureToggles) toggle.Checked = false;
        SetConnection("All features off. Link to the game when you are ready.", "DISARMED", Theme.Muted);
        healthDisplay.Value = staminaDisplay.Value = consciousnessDisplay.Value = null;
        values.Text = "Local session  /  Half Sword 0.7 Hotfix 4  /  Steam build 26820056  /  Saves backed up before the first write";
    }

    internal static async Task VerifyStaging(TextWriter log)
    {
        using var form = new TrainerForm();
        form.SetFeatureAvailability(false);
        if (!form.repairSaveButton.Enabled)
            throw new InvalidOperationException("Offline save repair is unavailable without a game session.");
        if (form.FeatureToggles.Any(toggle => !toggle.Enabled) || !form.speedValue.Enabled ||
            !form.strengthValue.Enabled || !form.reputationValue.Enabled || !form.physiqueValue.Enabled ||
            form.fundsButton.Enabled || form.physiqueButton.Enabled)
            throw new InvalidOperationException("Pre-game options or session-only funds availability are incorrect.");
        form.ShowStatus(new TrainerStatus("test", 100, 100, 100, 1000, 0, 1, 0, false, 0, 1, 0));
        if (form.forceWinButton.Enabled) throw new InvalidOperationException("Force Win is enabled without an active fighter.");
        if (form.forceWinButton.Parent?.AccessibleName != "Combat match actions" || !form.forceWinButton.Danger)
            throw new InvalidOperationException("Force Win is not presented as a danger action beside live combat status.");
        form.CreateControl();
        var originalSize = form.Size;
        foreach (var size in new[] { originalSize, form.MinimumSize })
        {
            form.Size = size;
            form.PerformLayout();
            var button = form.forceWinButton;
            var row = button.Parent!;
            var labelSize = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine);
            int gap = Math.Max(1, (int)Math.Floor(4 * Theme.Scale(form)));
            if (button.Width < labelSize.Width + 2 * gap || button.Height < labelSize.Height ||
                button.Left < gap || button.Top < 0 ||
                button.Right > row.ClientSize.Width - gap || button.Bottom > row.ClientSize.Height - gap)
                throw new InvalidOperationException($"Force Win layout is clipped or crowded at {size}.");
            var repair = form.repairSaveButton;
            var repairRow = repair.Parent!;
            var repairLabelSize = TextRenderer.MeasureText(repair.Text, repair.Font, Size.Empty, TextFormatFlags.SingleLine);
            var hintSize = TextRenderer.MeasureText(form.repairSaveHint.Text, form.repairSaveHint.Font,
                new Size(form.repairSaveHint.ClientSize.Width, int.MaxValue), TextFormatFlags.WordBreak);
            if (repair.Width < repairLabelSize.Width + 2 * gap || repair.Height < repairLabelSize.Height ||
                repair.Left < 0 || repair.Top < 0 ||
                repair.Right > repairRow.ClientSize.Width || repair.Bottom > repairRow.ClientSize.Height ||
                form.repairSaveHint.Height < hintSize.Height + form.repairSaveHint.Padding.Vertical)
                throw new InvalidOperationException($"Offline save-repair button or explanation is clipped at {size}.");
            foreach (var feature in form.FeatureToggles.OfType<FeatureToggle>())
                if (!feature.TextFits())
                    throw new InvalidOperationException($"Option '{feature.Text}' title or description is clipped at {size}.");
        }
        form.Size = originalSize;
        log.WriteLine("PASS Force Win label and spacing at default and minimum window sizes.");
        log.WriteLine("PASS offline save-repair button and explanation fit at default and minimum window sizes.");
        log.WriteLine("PASS option titles and descriptions fit at default and minimum window sizes.");
        form.ShowStatus(new TrainerStatus("test", 100, 100, 100, 1000, 0, 1, 0, false, 0, 1, 0, 4.035, true));
        if (!form.physiqueButton.Enabled || form.physiqueValue.Value != 0.5m ||
            !form.physiqueHint.Text.Contains(Format(4.035)))
            throw new InvalidOperationException("Menu physique editing or unclamped saved-value readback is incorrect.");
        form.ShowStatus(new TrainerStatus("test", 100, 100, 100, 1000, 0, 1, 0, false, 0, 1, 0, 0.5, false));
        if (form.physiqueButton.Enabled) throw new InvalidOperationException("Physique editing is enabled outside a supported career menu.");
        form.ShowStatus(new TrainerStatus("test", 100, 100, 100, 1000, 0, 1, 0, true, 0, 1, 0, 0.5, true));
        if (form.physiqueButton.Enabled) throw new InvalidOperationException("Physique editing is enabled with an active fighter.");
        foreach (double value in new[] { 0d, 0.5, 1d, 4d, 10d }) Trainer.ValidateCareerPhysique(value);
        foreach (double value in new[] { -0.01, 10.01, double.NaN, double.PositiveInfinity })
        {
            try { Trainer.ValidateCareerPhysique(value); }
            catch (ArgumentOutOfRangeException) { continue; }
            throw new InvalidOperationException("An invalid career physique was accepted.");
        }
        log.WriteLine("PASS physique limits, saved-value readback, and menu-only action availability.");
        var character = new GameProperty("Player Character", "StructProperty", 0x260, 0x240, 1);
        var weight = new GameProperty("Weight_Test", "DoubleProperty", 0x20, 8, 2);
        if (Trainer.ComposeCareerPhysiqueProperty(character, weight, 0xD00).Offset != 0x280)
            throw new InvalidOperationException("Nested physique offset is incorrect.");
        foreach (var (container, member) in new[]
        {
            (character, weight with { Offset = -1 }),
            (character, weight with { Offset = 0x239 }),
            (character, weight with { Type = "FloatProperty", Size = 4 }),
            (character with { Offset = int.MaxValue - 1 }, weight),
            (character with { Size = 0 }, weight)
        })
        {
            try { Trainer.ComposeCareerPhysiqueProperty(container, member, 0xD00); }
            catch (InvalidOperationException) { continue; }
            throw new InvalidOperationException("An invalid nested physique layout was accepted.");
        }
        log.WriteLine("PASS nested physique type, struct bounds, and containing-object bounds.");
        form.ShowStatus(new TrainerStatus("test", 100, 100, 100, 1000, 0, 1, 0, true, 0, 1, 0));
        if (!form.forceWinButton.Enabled) throw new InvalidOperationException("Force Win is disabled with an active fighter.");
        form.SetFeatureAvailability(false);
        if (form.forceWinButton.Enabled) throw new InvalidOperationException("Force Win remains enabled after losing the game session.");
        foreach (var (absent, state) in new[] { (false, "BETWEEN BOUTS"), (true, "WAITING FOR GAME") })
        {
            form.SetFeatureAvailability(true);
            form.ShowStatus(new TrainerStatus("test", 100, 100, 100, 1000, 0, 1, 0, true, 0, 1, 0));
            form.ShowWaitingState(absent, state);
            if (form.forceWinButton.Enabled || form.fundsButton.Enabled || form.physiqueButton.Enabled || form.badge.Text != state)
                throw new InvalidOperationException($"Session actions remain available while {state}.");
        }
        log.WriteLine("PASS session actions are disabled during scene changes and game exit.");
        foreach (var toggle in form.FeatureToggles) toggle.Checked = true;
        form.speedValue.Value = 1.75m;
        form.strengthValue.Value = 8.5m;
        form.reputationValue.Value = 2300;
        var staged = form.options;
        form.pendingPhysique = 0.75;
        await form.StopAsync(false);
        if (form.pendingPhysique != -1 || form.physiqueButton.Enabled)
            throw new InvalidOperationException("Relinking retained a one-shot physique request.");
        if (form.options != staged || !form.options.NoExhaustion || !form.options.Temperature ||
            !form.options.RankReputation || !form.options.SuperStrength || !form.options.StrongLegJoints ||
            form.options.ReputationValue != 2300 ||
            form.options.SpeedValue != 1.75 || form.options.StrengthMultiplier != 8.5)
            throw new InvalidOperationException("Linking discarded preselected options or modifier values.");
        form.SetFeatureAvailability(true);
        form.SetFeatureAvailability(false);
        if (form.options != staged || form.FeatureToggles.Any(toggle => !toggle.Enabled) || form.fundsButton.Enabled)
            throw new InvalidOperationException("A lost game session discarded or disabled staged options.");
        log.WriteLine("PASS pre-game selection, linking without reset, and lost-session option retention.");
        await form.StopAsync();
        if (form.FeatureToggles.Any(toggle => toggle.Checked) || form.options != new TrainerOptions(
            SpeedValue: 1.75, StrengthMultiplier: 8.5, ReputationValue: 2300))
            throw new InvalidOperationException("Disarm did not switch off every selected feature.");
        log.WriteLine("PASS Disarm clears every feature, including fitness and reputation, without changing input values.");
        foreach (double value in new[] { 1d, 3d, 50d })
            new TrainerOptions(SuperStrength: true, StrengthMultiplier: value).Validate();
        foreach (double value in new[] { 0.99, 50.01, double.NaN, double.PositiveInfinity })
        {
            try { new TrainerOptions(SuperStrength: true, StrengthMultiplier: value).Validate(); }
            catch (ArgumentOutOfRangeException) { continue; }
            throw new InvalidOperationException("An invalid strength multiplier was accepted.");
        }
        log.WriteLine("PASS Super Strength multiplier limits.");
        foreach (int value in new[] { -1, 1000000001 })
        {
            try { new TrainerOptions(ReputationValue: value).Validate(); }
            catch (ArgumentOutOfRangeException) { continue; }
            throw new InvalidOperationException("Out-of-range reputation was accepted.");
        }
        var reputationOptions = new TrainerOptions(RankReputation: true, ReputationValue: 1500);
        if (reputationOptions.ReputationTarget(45) != 1500 || reputationOptions.ReputationTarget(2400) != 2400)
            throw new InvalidOperationException("Reputation floor would lower earned progress or fail to accelerate a low rank.");
        log.WriteLine("PASS reputation rejects out-of-range input and never reduces earned progress.");
        log.WriteLine("PASS Force Win button is enabled only for a live combat-ready fighter.");
    }

    private void Post(Action action)
    {
        if (!IsDisposed && IsHandleCreated) BeginInvoke(action);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) optionTips.Dispose();
        base.Dispose(disposing);
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
        if (repairingSave)
        {
            e.Cancel = true;
            return;
        }
        if (closing) return;
        e.Cancel = true;
        closing = true;
        Enabled = false;
        await StopAsync();
        Close();
    }
}
