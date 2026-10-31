using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Tests;

/// <summary>Racine du dépôt : le dossier qui contient la solution.</summary>
internal static class Repository
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "KyfRev.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new InvalidOperationException("Solution introuvable.");
    }
}

/// <summary>Un élément posé dans un hôte qui porte les ressources du design system, mis en page sans fenêtre.</summary>
internal static class Host
{
    public static Border Of(DesignResources design, UIElement element, double width = 480, double height = 320)
    {
        var host = new Border { Resources = design.Dictionary, Child = element, Width = width, Height = height };
        Layout(host);
        return host;
    }

    public static void Layout(FrameworkElement host)
    {
        host.Measure(new Size(host.Width, host.Height));
        host.Arrange(new Rect(0, 0, host.Width, host.Height));
        host.UpdateLayout();
    }

    /// <summary>Exécute ce qui attend sur le fil de l'interface.</summary>
    public static void Flush() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });

    public static T Part<T>(Control control, string name)
        where T : class =>
        control.Template.FindName(name, control) as T ?? throw new InvalidOperationException($"« {name} » absent du modèle de {control.GetType().Name}.");
}
