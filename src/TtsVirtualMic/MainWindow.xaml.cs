using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TtsVirtualMic.Models;
using TtsVirtualMic.Services;
using TtsVirtualMic.Services.Engines;

namespace TtsVirtualMic;

public partial class MainWindow : Window
{
    readonly AppSettings _s = AppSettings.Load();
    readonly AudioService _audio = new();
    readonly EngineHub _hub = new();
    readonly SpeechController _speech;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    System.Windows.Forms.NotifyIcon? _tray;
    List<VoiceInfo> _voices = new();
    bool _loading = true, _exit, _dirty, _trayHintShown;
    volatile float _level;
    int _tick;

    public MainWindow()
    {
        InitializeComponent();
        _speech = new SpeechController(_audio, _hub, _s);
        _speech.TransmittingChanged += t => Dispatcher.BeginInvoke(new Action(() => SetTransmitting(t)));
        _speech.Level += v => { if (v > _level) _level = v; };
        _speech.Error += m => Dispatcher.BeginInvoke(new Action(() => ShowError(m)));

        LoadVoices();
        SpeedSlider.Value = _s.Speed;
        PitchSlider.Value = _s.Pitch;
        VolumeSlider.Value = _s.Volume;
        PauseSlider.Value = _s.PauseMs;
        MonitorCheck.IsChecked = _s.Monitor;
        TrayCheck.IsChecked = _s.MinimizeToTray;
        UpdateLabels();
        RefreshHistory();
        InitTray();
        RefreshStatus();

        _timer.Tick += Timer_Tick;
        _timer.Start();
        _loading = false;
    }

    // ---------- Voces ----------
    void LoadVoices()
    {
        _voices = _hub.AllVoices();
        VoiceBox.ItemsSource = _voices;
        var sel = _voices.FirstOrDefault(v => v.Id == _s.VoiceId) ?? _voices.FirstOrDefault();
        VoiceBox.SelectedItem = sel;
        if (_voices.Count == 0)
            ShowError("No hay voces disponibles. Instalá una voz de Windows o ejecutá scripts\\fetch-tools.ps1 para añadir Piper y eSpeak.");
    }

    void Voice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || VoiceBox.SelectedItem is not VoiceInfo v) return;
        _s.VoiceId = v.Id; _dirty = true;
    }

    // ---------- Controles ----------
    void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _s.Speed = Math.Round(SpeedSlider.Value, 2);
        _s.Pitch = Math.Round(PitchSlider.Value, 1);
        _s.Volume = Math.Round(VolumeSlider.Value);
        _s.PauseMs = (int)Math.Round(PauseSlider.Value / 50) * 50;
        UpdateLabels();
        _dirty = true;
    }

    void UpdateLabels()
    {
        SpeedLabel.Text = $"{SpeedSlider.Value:0.00}x";
        PitchLabel.Text = $"{PitchSlider.Value:+0.0;-0.0;0} st";
        VolumeLabel.Text = $"{VolumeSlider.Value:0}%";
        PauseLabel.Text = $"{(int)Math.Round(PauseSlider.Value / 50) * 50} ms";
    }

    void Options_Click(object sender, RoutedEventArgs e)
    {
        _s.Monitor = MonitorCheck.IsChecked == true;
        _s.MinimizeToTray = TrayCheck.IsChecked == true;
        _dirty = true;
    }

    // ---------- Acciones ----------
    async void Play_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text.Trim();
        if (text.Length == 0 || VoiceBox.SelectedItem is not VoiceInfo v) return;
        ErrorText.Visibility = Visibility.Collapsed;
        AddHistory(text);
        await _speech.SpeakAsync(text, v, toCable: true);
    }

    async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (VoiceBox.SelectedItem is not VoiceInfo v) return;
        ErrorText.Visibility = Visibility.Collapsed;
        var text = InputBox.Text.Trim();
        if (text.Length == 0) text = "Hola, esta es una prueba de la voz seleccionada.";
        await _speech.SpeakAsync(text, v, toCable: false);
    }

    void Stop_Click(object sender, RoutedEventArgs e) => _speech.Stop();

    void Paste_Click(object sender, RoutedEventArgs e)
    {
        try { if (Clipboard.ContainsText()) InputBox.SelectedText = Clipboard.GetText(); }
        catch { ShowError("No se pudo leer el portapapeles."); }
        InputBox.Focus();
    }

    void Clear_Click(object sender, RoutedEventArgs e) { InputBox.Clear(); InputBox.Focus(); }

    void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Play_Click(sender, e);
            e.Handled = true;
        }
    }

    void Diag_Click(object sender, RoutedEventArgs e) =>
        new DiagnosticsWindow(_audio) { Owner = this }.ShowDialog();

    // ---------- Historial ----------
    void AddHistory(string text)
    {
        _s.History.RemoveAll(h => h == text);
        _s.History.Insert(0, text);
        if (_s.History.Count > 30) _s.History.RemoveRange(30, _s.History.Count - 30);
        _dirty = true;
        RefreshHistory();
    }

    void RefreshHistory() => HistoryList.ItemsSource = _s.History.ToList();

    void History_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is string t) { InputBox.Text = t; InputBox.Focus(); }
    }

    void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _s.History.Clear(); _dirty = true; RefreshHistory();
    }

    // ---------- Estado ----------
    void SetTransmitting(bool t)
    {
        Led.Fill = (Brush)FindResource(t ? "Danger" : "Muted");
        LiveText.Text = t ? "TRANSMITIENDO" : "En espera";
        LiveText.Foreground = (Brush)FindResource(t ? "Danger" : "Muted");
        if (!t) _level = 0;
    }

    void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    void RefreshStatus()
    {
        bool ok;
        using (var i = _audio.FindCableInput()) using (var o = _audio.FindCableOutput())
            ok = i != null && o != null;
        StatusDot.Fill = (Brush)FindResource(ok ? "Ok" : "Danger");
        StatusText.Text = ok ? "Micrófono virtual listo" : "VB-CABLE no detectado";
    }

    void Timer_Tick(object? sender, EventArgs e)
    {
        LevelBar.Value = _level;
        _level *= 0.8f;
        if (++_tick % 33 == 0)   // ~2 s
        {
            RefreshStatus();
            if (_dirty) { _dirty = false; _s.Save(); }
        }
    }

    // ---------- Bandeja ----------
    void InitTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "TTS Virtual Mic",
            Visible = true,
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Mostrar", null, (_, _) => ShowFromTray());
        menu.Items.Add("Detener audio", null, (_, _) => _speech.Stop());
        menu.Items.Add("Salir", null, (_, _) => { _exit = true; Close(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowFromTray();
    }

    void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && _s.MinimizeToTray) Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_s.MinimizeToTray && !_exit)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray?.ShowBalloonTip(2000, "TTS Virtual Mic", "Sigue activo en la bandeja del sistema.", System.Windows.Forms.ToolTipIcon.Info);
            }
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _speech.Stop();
        _timer.Stop();
        _s.Save();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _audio.Dispose();
        base.OnClosed(e);
    }
}
