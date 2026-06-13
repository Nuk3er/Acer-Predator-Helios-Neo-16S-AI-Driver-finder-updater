using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Boost;
using HeliosToolkit.App.Services.System;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace HeliosToolkit.App.ViewModels;

/// <summary>
/// Drives Game Boost from the Dashboard tile and the tray. Owns the autopilot
/// wiring: when enabled, a watched game starting boosts automatically and the
/// last one exiting restores. A manual un-boost suspends autopilot until the
/// next game start.
/// </summary>
public sealed partial class BoostViewModel : ObservableObject
{
    private readonly BoostController _boost;
    private readonly GameWatchService _watcher;
    private readonly BoostConfigStore _store;
    private readonly CpuTopologyService _topology;
    private readonly ISnackbarService _snackbar;
    private bool _manualOverride;

    public BoostViewModel(
        BoostController boost,
        GameWatchService watcher,
        BoostConfigStore store,
        CpuTopologyService topology,
        ISnackbarService snackbar)
    {
        _boost = boost;
        _watcher = watcher;
        _store = store;
        _topology = topology;
        _snackbar = snackbar;

        _boost.StateChanged += OnBoostStateChanged;
        _watcher.FirstGameStarted += OnFirstGameStarted;
        _watcher.LastGameStopped += OnLastGameStopped;

        IsAutopilotOn = store.LoadConfig().AutopilotEnabled;
        if (IsAutopilotOn)
        {
            _watcher.Start();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoostLabel))]
    [NotifyPropertyChangedFor(nameof(StateLine))]
    private bool _isBoosted;

    [ObservableProperty]
    private bool _isAutopilotOn;

    public string BoostLabel => IsBoosted ? "Un-Boost" : "Game Boost";

    public string StateLine
    {
        get
        {
            BoostConfig c = _store.LoadConfig();
            if (IsBoosted)
            {
                var parts = new List<string>();
                if (c.HoldTimer) parts.Add("timer held");
                if (c.UseUltimatePlan) parts.Add("ultimate plan");
                if (c.EnableDnd) parts.Add("DND on");
                if (c.KillList.Count > 0) parts.Add($"{c.KillList.Count} app(s) closed");
                return "Boosted — " + string.Join(", ", parts) + ".";
            }

            string topo = _topology.IsHybrid ? $"{_topology.PCoreCount}P+{_topology.ECoreCount}E" : "ready";
            return $"Not boosted ({topo}). One click: timer + power plan + Do Not Disturb + your kill-list.";
        }
    }

    [RelayCommand]
    private async Task ToggleBoostAsync()
    {
        try
        {
            if (IsBoosted)
            {
                _manualOverride = true; // suspend autopilot restore until next game start
                await _boost.UnboostAsync();
            }
            else
            {
                _manualOverride = false;
                await _boost.BoostAsync("manual");
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Boost toggle failed");
            _snackbar.Show("Boost failed", e.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(7));
        }
    }

    partial void OnIsAutopilotOnChanged(bool value)
    {
        BoostConfig config = _store.LoadConfig() with { AutopilotEnabled = value };
        _store.SaveConfig(config);
        if (value)
        {
            _watcher.Start();
            _snackbar.Show("Autopilot on", "Helios will Boost automatically when a watched game starts.",
                ControlAppearance.Success, new SymbolIcon(SymbolRegular.Rocket24), TimeSpan.FromSeconds(5));
        }
        else
        {
            _watcher.Stop();
        }
    }

    private async void OnFirstGameStarted(string exe)
    {
        _manualOverride = false;
        if (!IsBoosted)
        {
            await RunOnUiAsync(() => _boost.BoostAsync($"auto:{exe}"));
            _snackbar.Show("Game Boost", $"{exe} started — boosted automatically.",
                ControlAppearance.Success, new SymbolIcon(SymbolRegular.Rocket24), TimeSpan.FromSeconds(5));
        }

        TryPinWatchedGame(exe);
    }

    private async void OnLastGameStopped()
    {
        if (IsBoosted && !_manualOverride)
        {
            await RunOnUiAsync(() => _boost.UnboostAsync());
            _snackbar.Show("Boost off", "Game closed — system restored.",
                ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(5));
        }
    }

    private void TryPinWatchedGame(string exe)
    {
        WatchedGame? game = _store.LoadConfig().WatchedGames
            .FirstOrDefault(g => g.ExeName.Equals(exe, StringComparison.OrdinalIgnoreCase));
        if (game is not { PinToPCores: true })
        {
            return;
        }

        foreach (System.Diagnostics.Process p in
                 System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            using (p)
            {
                if (!_boost.PinGame(p))
                {
                    _snackbar.Show("Pinning skipped", $"{exe} is a protected process — couldn't pin to P-cores.",
                        ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(5));
                }
            }
        }
    }

    private void OnBoostStateChanged(bool active) =>
        RunOnUi(() =>
        {
            IsBoosted = active;
            OnPropertyChanged(nameof(StateLine));
        });

    private static void RunOnUi(Action action) =>
        Application.Current?.Dispatcher.Invoke(action);

    private static Task RunOnUiAsync(Func<Task> action) =>
        Application.Current?.Dispatcher.Invoke(action) ?? action();
}
