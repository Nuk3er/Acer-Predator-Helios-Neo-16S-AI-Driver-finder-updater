using CommunityToolkit.Mvvm.ComponentModel;
using HeliosToolkit.App.Services.Drivers;

namespace HeliosToolkit.App.ViewModels;

/// <summary>A checkable NVIDIA driver component in the debloat list.</summary>
public partial class NvidiaComponentViewModel : ObservableObject
{
    public NvidiaComponentViewModel(NvidiaComponent component)
    {
        Component = component;
        _isKept = component.KeepByDefault || component.Required;
    }

    public NvidiaComponent Component { get; }

    public string FolderName => Component.FolderName;
    public string Description => Component.Description;
    public bool Required => Component.Required;

    [ObservableProperty]
    private bool _isKept;

    // Required components can't be unchecked.
    public bool IsEnabled => !Required;
}
