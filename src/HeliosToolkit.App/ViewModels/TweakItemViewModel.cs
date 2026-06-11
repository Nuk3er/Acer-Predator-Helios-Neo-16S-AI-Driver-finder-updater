using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Tweaks;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace HeliosToolkit.App.ViewModels;

/// <summary>One row on a tweak page: wraps an <see cref="ITweak"/> with UI state.</summary>
public partial class TweakItemViewModel : ObservableObject
{
    private readonly ITweak _tweak;
    private readonly TweakEngine _engine;
    private readonly IContentDialogService _dialogs;
    private readonly ISnackbarService _snackbar;
    private bool _suppressToggle;

    public TweakItemViewModel(
        ITweak tweak, TweakEngine engine, IContentDialogService dialogs, ISnackbarService snackbar)
    {
        _tweak = tweak;
        _engine = engine;
        _dialogs = dialogs;
        _snackbar = snackbar;
    }

    public TweakMetadata Meta => _tweak.Meta;
    public string Name => Meta.Name;
    public string Category => Meta.Category;
    public string Description => Meta.Description;
    public string? Warning => Meta.Warning;
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
    public bool IsInfo => Meta.Risk == RiskLevel.Info;

    public string RiskLabel => Meta.Risk switch
    {
        RiskLevel.Safe => "SAFE",
        RiskLevel.Situational => "SITUATIONAL",
        RiskLevel.Risky => "RISKY",
        _ => "INFO",
    };

    public Brush RiskBrush => FindBrush(Meta.Risk switch
    {
        RiskLevel.Safe => "RiskSafeBrush",
        RiskLevel.Situational => "RiskSituationalBrush",
        RiskLevel.Risky => "RiskRiskyBrush",
        _ => "RiskInfoBrush",
    });

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(IsToggleEnabled))]
    [NotifyPropertyChangedFor(nameof(ShowInfoLink))]
    private TweakState _state = TweakState.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToggleEnabled))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isOn;

    public bool IsToggleEnabled => !IsBusy && !IsInfo && State != TweakState.NotApplicable;

    public bool ShowRebootHint => Meta.RequiresReboot || Meta.RebootNote is not null;
    public string RebootHint => Meta.RebootNote ?? "Reboot required";

    public bool ShowInfoLink => IsInfo && _tweak is IInfoTweak { LinkUrl: not null };

    public string StateText => State switch
    {
        TweakState.Applied => "Applied",
        TweakState.NotApplied => "Not applied",
        TweakState.Mixed => "Partially applied",
        TweakState.NotApplicable => "Not applicable on this system",
        _ => "—",
    };

    public async Task RefreshAsync()
    {
        State = await _engine.DetectAsync(Meta.Id);
        SetToggleSilently(State == TweakState.Applied);
    }

    /// <summary>Bound to the ToggleSwitch. Confirms risky changes, applies/reverts, re-detects.</summary>
    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (_suppressToggle || IsInfo)
        {
            return;
        }

        bool turningOn = IsOn;

        if (turningOn && Meta.Risk == RiskLevel.Risky && !await ConfirmRiskyAsync())
        {
            SetToggleSilently(false);
            return;
        }

        try
        {
            IsBusy = true;
            TweakActionResult result = turningOn
                ? await _engine.ApplyAsync(Meta.Id)
                : await _engine.RevertAsync(Meta.Id);

            State = result.NewState;
            SetToggleSilently(State == TweakState.Applied);

            if (result.Outcome == ApplyOutcome.Failed)
            {
                _snackbar.Show("Tweak failed", $"{Name}: {result.Error}",
                    ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(8));
            }
            else if (ShowRebootHint)
            {
                _snackbar.Show(turningOn ? "Applied" : "Reverted", $"{Name} — {RebootHint} to take full effect.",
                    ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ArrowSync24), TimeSpan.FromSeconds(6));
            }
            else
            {
                _snackbar.Show(turningOn ? "Applied" : "Reverted", Name,
                    ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(4));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenLink()
    {
        if (_tweak is IInfoTweak { LinkUrl: { } url })
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // ignored — best-effort
            }
        }
    }

    private async Task<bool> ConfirmRiskyAsync()
    {
        var result = await _dialogs.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = $"Apply risky tweak: {Name}?",
            Content = (Warning ?? Description) + "\n\nA System Restore point and a value backup are kept, so you can revert.",
            PrimaryButtonText = "Apply anyway",
            CloseButtonText = "Cancel",
        });

        return result == Wpf.Ui.Controls.ContentDialogResult.Primary;
    }

    private void SetToggleSilently(bool value)
    {
        _suppressToggle = true;
        IsOn = value;
        _suppressToggle = false;
    }

    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
}
