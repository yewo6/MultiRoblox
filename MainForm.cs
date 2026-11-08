using System.Runtime.InteropServices;

namespace MultiRoblox;

public sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(18, 18, 18);
    private static readonly Color PanelBg = Color.FromArgb(28, 28, 28);
    private static readonly Color Border = Color.FromArgb(55, 55, 55);
    private static readonly Color TextPrimary = Color.FromArgb(230, 230, 230);
    private static readonly Color TextMuted = Color.FromArgb(150, 150, 150);
    private static readonly Color BtnBg = Color.FromArgb(40, 40, 40);
    private static readonly Color CloseHover = Color.FromArgb(180, 50, 50);

    private readonly SingletonHolder _holder;
    private readonly AntiAfkService _antiAfk;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _trayToggleItem;
    private readonly ToolStripMenuItem _trayAntiAfkItem;
    private readonly CheckBox _enabledCheck;
    private readonly CheckBox _antiAfkCheck;
    private readonly CheckBox _startWithWindowsCheck;
    private readonly CheckBox _startMinimizedCheck;
    private readonly Label _robloxCountLabel;
    private readonly Button _closeRobloxButton;
    private readonly Panel _titleBar;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private bool _reallyClose;
    private bool _suppressEvents;

    public MainForm(SingletonHolder holder, AntiAfkService antiAfk, bool startInTray)
    {
        _holder = holder;
        _antiAfk = antiAfk;

        Text = "MultiRoblox";
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 248);
        Font = new Font("Consolas", 9f);
        BackColor = Bg;
        ForeColor = TextPrimary;
        Icon = AppIcon.Create();
        ShowInTaskbar = !startInTray;
        DoubleBuffered = true;

        _titleBar = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(340, 32),
            BackColor = PanelBg,
            Cursor = Cursors.SizeAll,
        };
        _titleBar.MouseDown += TitleBar_MouseDown;
        _titleBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 0, 31, Width, 31);
        };

        var title = new Label
        {
            Text = "MULTI ROBLOX",
            Font = new Font("Consolas", 10f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 8),
            ForeColor = TextPrimary,
            BackColor = PanelBg,
            Cursor = Cursors.SizeAll,
        };
        title.MouseDown += TitleBar_MouseDown;
        _titleBar.Controls.Add(title);

        var btnMin = MakeCaptionButton("_", 340 - 64);
        btnMin.Click += (_, _) => HideToTray();
        _titleBar.Controls.Add(btnMin);

        var btnClose = MakeCaptionButton("X", 340 - 32);
        btnClose.MouseEnter += (_, _) => btnClose.BackColor = CloseHover;
        btnClose.MouseLeave += (_, _) => btnClose.BackColor = PanelBg;
        btnClose.Click += (_, _) => HideToTray();
        _titleBar.Controls.Add(btnClose);

        _enabledCheck = MakeCheck("ENABLE MULTI-INSTANCE", new Point(12, 44));
        _enabledCheck.Font = new Font("Consolas", 9.5f, FontStyle.Bold);
        _enabledCheck.CheckedChanged += EnabledCheck_CheckedChanged;

        _antiAfkCheck = MakeCheck("ENABLE ANTI-AFK", new Point(12, 72));
        _antiAfkCheck.Font = new Font("Consolas", 9.5f, FontStyle.Bold);
        _antiAfkCheck.CheckedChanged += AntiAfkCheck_CheckedChanged;

        _robloxCountLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 104),
            ForeColor = TextMuted,
            BackColor = Bg,
        };

        var rule = new Panel
        {
            Location = new Point(12, 128),
            Size = new Size(316, 1),
            BackColor = Border,
        };

        _closeRobloxButton = new Button
        {
            Text = "CLOSE ALL ROBLOX",
            Location = new Point(12, 144),
            Size = new Size(150, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = BtnBg,
            ForeColor = TextPrimary,
            Font = new Font("Consolas", 8f),
            Cursor = Cursors.Hand,
        };
        _closeRobloxButton.FlatAppearance.BorderColor = Border;
        _closeRobloxButton.FlatAppearance.BorderSize = 1;
        _closeRobloxButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 55);
        _closeRobloxButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 70);
        _closeRobloxButton.Click += CloseRobloxButton_Click;

        _startWithWindowsCheck = MakeCheck("START WITH WINDOWS", new Point(172, 148));
        _startWithWindowsCheck.CheckedChanged += OptionsChanged;

        _startMinimizedCheck = MakeCheck("START MINIMIZED TO TRAY", new Point(12, 186));
        _startMinimizedCheck.CheckedChanged += OptionsChanged;

        var edge = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
        };
        edge.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        Controls.Add(_titleBar);
        Controls.Add(_enabledCheck);
        Controls.Add(_antiAfkCheck);
        Controls.Add(_robloxCountLabel);
        Controls.Add(rule);
        Controls.Add(_closeRobloxButton);
        Controls.Add(_startWithWindowsCheck);
        Controls.Add(_startMinimizedCheck);
        Controls.Add(edge);
        edge.SendToBack();

        _trayMenu = new ContextMenuStrip
        {
            BackColor = PanelBg,
            ForeColor = TextPrimary,
            Font = new Font("Consolas", 9f),
            ShowImageMargin = false,
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = new DarkMenuRenderer(),
        };
        _trayMenu.Items.Add("Open", null, (_, _) => ShowFromTray());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayToggleItem = new ToolStripMenuItem("Enable multi-instance") { CheckOnClick = true };
        _trayToggleItem.CheckedChanged += (_, _) =>
        {
            if (_suppressEvents)
                return;
            _enabledCheck.Checked = _trayToggleItem.Checked;
        };
        _trayMenu.Items.Add(_trayToggleItem);
        _trayAntiAfkItem = new ToolStripMenuItem("Enable anti-AFK") { CheckOnClick = true };
        _trayAntiAfkItem.CheckedChanged += (_, _) =>
        {
            if (_suppressEvents)
                return;
            _antiAfkCheck.Checked = _trayAntiAfkItem.Checked;
        };
        _trayMenu.Items.Add(_trayAntiAfkItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon = Icon,
            Text = "MultiRoblox",
            Visible = true,
            ContextMenuStrip = _trayMenu,
        };
        _tray.DoubleClick += (_, _) => ShowFromTray();

        _pollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _pollTimer.Tick += (_, _) => RefreshStatus();
        _pollTimer.Start();

        _holder.StateChanged += (_, _) =>
        {
            if (IsDisposed)
                return;
            BeginInvoke(RefreshStatus);
        };

        _antiAfk.StateChanged += (_, _) =>
        {
            if (IsDisposed)
                return;
            BeginInvoke(RefreshStatus);
        };

        Load += (_, _) =>
        {
            _suppressEvents = true;
            _startWithWindowsCheck.Checked = AppSettings.StartWithWindows;
            _startMinimizedCheck.Checked = AppSettings.StartMinimizedToTray;
            _suppressEvents = false;

            if (AppSettings.StartEnabled)
                _holder.TryStart();

            if (AppSettings.AntiAfkEnabled)
                _antiAfk.Start();

            RefreshStatus();

            if (startInTray || AppSettings.StartMinimizedToTray)
                HideToTray();
        };

        FormClosing += MainForm_FormClosing;
        Resize += MainForm_Resize;
    }

    private static Button MakeCaptionButton(string text, int x)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, 0),
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = PanelBg,
            ForeColor = TextPrimary,
            Font = new Font("Consolas", 9f, FontStyle.Bold),
            TabStop = false,
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 50);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(65, 65, 65);
        return btn;
    }

    private static CheckBox MakeCheck(string text, Point location)
    {
        return new CheckBox
        {
            Text = text,
            AutoSize = true,
            Location = location,
            ForeColor = TextPrimary,
            BackColor = Bg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 8.5f),
        };
    }

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
    }

    private void EnabledCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
            return;

        if (_enabledCheck.Checked)
        {
            if (!_holder.TryStart())
            {
                _suppressEvents = true;
                _enabledCheck.Checked = false;
                _suppressEvents = false;

                if (SingletonHolder.IsRobloxRunning())
                {
                    var result = MessageBox.Show(
                        this,
                        "Roblox is already running and owns the singleton lock.\n\nClose all Roblox clients now so MultiRoblox can take it?",
                        "MultiRoblox",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        SingletonHolder.CloseAllRobloxProcesses();
                        if (_holder.TryStart())
                        {
                            _suppressEvents = true;
                            _enabledCheck.Checked = true;
                            _suppressEvents = false;
                        }
                    }
                }
            }
        }
        else
        {
            _holder.Stop();
        }

        AppSettings.StartEnabled = _enabledCheck.Checked;
        AppSettings.Save();
        RefreshStatus();
    }

    private void AntiAfkCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
            return;

        if (_antiAfkCheck.Checked)
            _antiAfk.Start();
        else
            _antiAfk.Stop();

        AppSettings.AntiAfkEnabled = _antiAfkCheck.Checked;
        AppSettings.Save();
        RefreshStatus();
    }

    private void CloseRobloxButton_Click(object? sender, EventArgs e)
    {
        var closed = SingletonHolder.CloseAllRobloxProcesses();
        RefreshStatus();
        _tray.ShowBalloonTip(2500, "MultiRoblox", closed > 0 ? $"Closed {closed} Roblox process(es)." : "No Roblox processes found.", ToolTipIcon.Info);
    }

    private void OptionsChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
            return;

        AppSettings.StartWithWindows = _startWithWindowsCheck.Checked;
        AppSettings.StartMinimizedToTray = _startMinimizedCheck.Checked;
        AppSettings.Save();
        AppSettings.ApplyStartupShortcut();
    }

    private void RefreshStatus()
    {
        if (IsDisposed)
            return;

        _suppressEvents = true;
        _enabledCheck.Checked = _holder.IsHolding;
        _trayToggleItem.Checked = _holder.IsHolding;
        _trayToggleItem.Text = _holder.IsHolding ? "Disable multi-instance" : "Enable multi-instance";
        _antiAfkCheck.Checked = _antiAfk.IsEnabled;
        _trayAntiAfkItem.Checked = _antiAfk.IsEnabled;
        _trayAntiAfkItem.Text = _antiAfk.IsEnabled ? "Disable anti-AFK" : "Enable anti-AFK";
        _suppressEvents = false;

        int count = 0;
        try
        {
            count = System.Diagnostics.Process.GetProcessesByName("RobloxPlayerBeta").Length
                + System.Diagnostics.Process.GetProcessesByName("Roblox").Length;
        }
        catch
        {
        }

        _robloxCountLabel.Text = $"CLIENTS: {count}";

        string afk = _antiAfk.IsEnabled ? "AFK" : "";
        _tray.Text = _holder.IsHolding
            ? $"MultiRoblox - ON ({count}){(afk.Length > 0 ? " + AFK" : "")}"
            : $"MultiRoblox - OFF{(afk.Length > 0 ? " + AFK" : "")}";
        _tray.Icon = _holder.IsHolding ? AppIcon.CreateActive() : AppIcon.Create();
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
            HideToTray();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_reallyClose)
            return;

        if (e.CloseReason is CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            _tray.ShowBalloonTip(2000, "MultiRoblox", "Still running in the tray. Right-click the icon to exit.", ToolTipIcon.Info);
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _reallyClose = true;
        _pollTimer.Stop();
        _tray.Visible = false;
        _antiAfk.Dispose();
        _holder.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Dispose();
            _tray.Dispose();
            _trayMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = TextPrimary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.ContentRectangle.Top + e.Item.ContentRectangle.Height / 2;
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, e.Item.ContentRectangle.Left + 4, y, e.Item.ContentRectangle.Right - 4, y);
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => Color.FromArgb(50, 50, 50);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(50, 50, 50);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(50, 50, 50);
        public override Color MenuStripGradientBegin => PanelBg;
        public override Color MenuStripGradientEnd => PanelBg;
        public override Color ToolStripDropDownBackground => PanelBg;
        public override Color ImageMarginGradientBegin => PanelBg;
        public override Color ImageMarginGradientMiddle => PanelBg;
        public override Color ImageMarginGradientEnd => PanelBg;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
