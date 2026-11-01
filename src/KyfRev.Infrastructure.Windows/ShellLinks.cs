using System.ComponentModel;
using System.Diagnostics;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>Ouvre les adresses par le système. Rien d'autre qu'une adresse web ou de courriel ne part d'ici.</summary>
public sealed class ShellLinks : IExternalLinks
{
    private readonly Func<ProcessStartInfo, bool> _start;

    /// <summary>Par l'application associée à chaque adresse.</summary>
    public ShellLinks()
        : this(information =>
        {
            using var process = Process.Start(information);
            return true;
        })
    {
    }

    internal ShellLinks(Func<ProcessStartInfo, bool> start)
    {
        _start = start;
    }

    /// <inheritdoc/>
    public bool Open(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var isAllowed = address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
        if (!isAllowed)
        {
            return false;
        }
        try
        {
            return _start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
