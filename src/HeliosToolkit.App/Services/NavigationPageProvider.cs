using Wpf.Ui.Abstractions;

namespace HeliosToolkit.App.Services;

/// <summary>Resolves NavigationView target pages from the DI container.</summary>
public sealed class NavigationPageProvider(IServiceProvider serviceProvider) : INavigationViewPageProvider
{
    public object? GetPage(Type pageType) => serviceProvider.GetService(pageType);
}
