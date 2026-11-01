namespace KyfRev.Application.Ports;

/// <summary>Ouvre une adresse hors de kyfRev.</summary>
public interface IExternalLinks
{
    /// <summary>Ouvre une adresse web ou de courriel dans le navigateur du système ; rend faux si c'est impossible.</summary>
    bool Open(string address);
}
