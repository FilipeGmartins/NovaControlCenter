using System.Drawing;
using System.IO;
using System.Windows.Threading;
using NovaControlCenter.Infrastructure.Ds4;
using Forms = System.Windows.Forms;

namespace NovaControlCenter.App.Services;

/// <summary>
/// Ícone na área de notificação (ao lado do relógio): abrir, trocar o modo do PS4 e sair.
/// A dica mostra o estado dos dois controles. Usa o NotifyIcon do Windows Forms (o WPF não tem um).
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly IDs4Controller _ds4;
    private readonly Forms.ToolStripMenuItem _modePlayStation;
    private readonly Forms.ToolStripMenuItem _modeXbox;
    private readonly DispatcherTimer _tooltipTimer;
    private bool _balloonShown;

    public TrayIcon(IDs4Controller ds4, Func<string> statusText, Action open, Action exit, Action<Ds4RecognitionMode> setMode)
    {
        _ds4 = ds4 ?? throw new ArgumentNullException(nameof(ds4));
        ArgumentNullException.ThrowIfNull(statusText);

        _modePlayStation = new Forms.ToolStripMenuItem("PS4 como PlayStation", null, (_, _) => setMode(Ds4RecognitionMode.PlayStation));
        _modeXbox = new Forms.ToolStripMenuItem("PS4 como Xbox (XInput)", null, (_, _) => setMode(Ds4RecognitionMode.Xbox));

        var menu = new Forms.ContextMenuStrip { ShowImageMargin = false, Renderer = new DarkRenderer() };
        var openItem = new Forms.ToolStripMenuItem("Abrir Nova Control Center", null, (_, _) => open()) { Font = new Font(Forms.Control.DefaultFont, FontStyle.Bold) };
        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_modePlayStation);
        menu.Items.Add(_modeXbox);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Sair", null, (_, _) => exit()));
        menu.Opening += (_, _) => RefreshMode();

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Nova Control Center",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) open(); };

        // A dica é atualizada devagar (a cada 3 s): custo desprezível em segundo plano.
        _tooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _tooltipTimer.Tick += (_, _) => _icon.Text = Fit(statusText());
        _tooltipTimer.Start();
        _icon.Text = Fit(statusText());
    }

    /// <summary>Avisa uma única vez por sessão que o app continua na bandeja.</summary>
    public void NotifyMinimized()
    {
        if (_balloonShown) return;
        _balloonShown = true;
        _icon.ShowBalloonTip(4000, "Nova Control Center", "Continua rodando na bandeja. Clique no ícone para abrir.", Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        _tooltipTimer.Stop();
        _icon.Visible = false;
        _icon.Dispose();
    }

    /// <summary>A dica do ícone aceita no máximo 63 caracteres.</summary>
    internal static string Fit(string text) => text.Length <= 63 ? text : text[..62] + "…";

    private void RefreshMode()
    {
        var mode = _ds4.Mode;
        _modePlayStation.Checked = mode == Ds4RecognitionMode.PlayStation;
        _modeXbox.Checked = mode == Ds4RecognitionMode.Xbox;
    }

    private static Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/NovaControlCenter.ico"));
        if (resource is null) return SystemIcons.Application;
        using Stream stream = resource.Stream;
        return new Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    /// <summary>Menu escuro, coerente com o tema do app.</summary>
    private sealed class DarkRenderer : Forms.ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(0xEE, 0xF0, 0xF4);
            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkColors : Forms.ProfessionalColorTable
    {
        private static readonly Color Background = Color.FromArgb(0x13, 0x16, 0x1D);
        private static readonly Color Hover = Color.FromArgb(0x24, 0x29, 0x36);
        public override Color ToolStripDropDownBackground => Background;
        public override Color MenuBorder => Color.FromArgb(0x26, 0x2B, 0x37);
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color SeparatorDark => Color.FromArgb(0x26, 0x2B, 0x37);
        public override Color SeparatorLight => Background;
        public override Color CheckBackground => Hover;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
    }
}
