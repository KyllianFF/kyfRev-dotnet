namespace KyfRev.Application.Ports;

/// <summary>Les voix de Windows, pour écouter une fiche.</summary>
public interface ISpeech
{
    /// <summary>Les voix françaises installées ; vide s'il n'y en a pas.</summary>
    IReadOnlyList<string> FrenchVoices();

    /// <summary>Dit un texte, et se termine quand il est dit ou que <paramref name="cancellation"/> l'interrompt.</summary>
    /// <param name="text">Le texte.</param>
    /// <param name="voice">La voix ; rien pour celle de Windows.</param>
    /// <param name="rate">La vitesse : 1 pour la normale.</param>
    /// <param name="cancellation">Ce qui l'interrompt.</param>
    Task<Outcome<Done>> SpeakAsync(string text, string? voice, double rate, CancellationToken cancellation);

    /// <summary>Met la voix en pause.</summary>
    void PauseSpeaking();

    /// <summary>Reprend là où la voix s'était arrêtée.</summary>
    void ResumeSpeaking();
}
