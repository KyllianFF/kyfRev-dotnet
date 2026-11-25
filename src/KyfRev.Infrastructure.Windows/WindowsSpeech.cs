using System.Globalization;
using System.Speech.Synthesis;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>Les voix de Windows, hors ligne : rien à installer, rien ne quitte le poste.</summary>
public sealed class WindowsSpeech : ISpeech, IDisposable
{
    /// <summary>La plus grande valeur que la synthèse accepte : elle vaut trois fois la vitesse normale.</summary>
    private const int FastestRate = 10;

    private const double RateBase = 3;

    private static readonly string[] FrenchCultures = ["fr-FR", "fr-CA", "fr-BE", "fr-CH"];

    private readonly Lazy<SpeechSynthesizer> _synthesizer;
    private TaskCompletionSource<Outcome<Done>>? _speaking;
    private Prompt? _prompt;

    /// <summary>Les voix, sur la sortie audio de Windows.</summary>
    public WindowsSpeech()
        : this(synthesizer => synthesizer.SetOutputToDefaultAudioDevice())
    {
    }

    /// <remarks>
    /// La synthèse se prépare au premier usage. Si elle échoue (aucune sortie audio, synthèse absente du système), rien ne
    /// s'arrête : la lecture est refusée avec la raison, aucune voix n'est proposée, et l'usage suivant réessaie, une sortie
    /// audio ayant pu être branchée entre-temps.
    /// </remarks>
    internal WindowsSpeech(Action<SpeechSynthesizer> output)
    {
        _synthesizer = new Lazy<SpeechSynthesizer>(() =>
        {
            var synthesizer = new SpeechSynthesizer();
            output(synthesizer);
            synthesizer.SpeakCompleted += OnCompleted;
            return synthesizer;
        }, LazyThreadSafetyMode.PublicationOnly);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> FrenchVoices()
    {
        try
        {
            return
            [
                .. FrenchCultures
                    .SelectMany(culture => _synthesizer.Value.GetInstalledVoices(CultureInfo.GetCultureInfo(culture)))
                    .Where(voice => voice.Enabled)
                    .Select(voice => voice.VoiceInfo.Name)
                    .Order(StringComparer.Ordinal),
            ];
        }
        catch (Exception problem) when (IsSpeechProblem(problem))
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public Task<Outcome<Done>> SpeakAsync(string text, string? voice, double rate, CancellationToken cancellation)
    {
        try
        {
            var synthesizer = _synthesizer.Value;
            Interrupt();
            if (voice is not null)
            {
                synthesizer.SelectVoice(voice);
            }
            synthesizer.Rate = RateOf(rate);
            var speaking = new TaskCompletionSource<Outcome<Done>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _speaking = speaking;
            _prompt = synthesizer.SpeakAsync(text);
            cancellation.Register(Interrupt);
            return speaking.Task;
        }
        catch (Exception problem) when (IsSpeechProblem(problem))
        {
            return Task.FromResult(Outcome.Refused<Done>(problem.Message));
        }
    }

    /// <summary>Ce qui peut empêcher la synthèse de parler : une voix inconnue, aucune sortie audio, la synthèse absente.</summary>
    private static bool IsSpeechProblem(Exception problem) =>
        problem is ArgumentException or InvalidOperationException or PlatformNotSupportedException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException or IOException;

    /// <inheritdoc/>
    public void PauseSpeaking()
    {
        if (_synthesizer.IsValueCreated)
        {
            _synthesizer.Value.Pause();
        }
    }

    /// <inheritdoc/>
    public void ResumeSpeaking()
    {
        if (_synthesizer.IsValueCreated)
        {
            _synthesizer.Value.Resume();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_synthesizer.IsValueCreated)
        {
            Interrupt();
            _synthesizer.Value.Dispose();
        }
    }

    /// <summary>Une vitesse en valeur de synthèse : celle-ci est logarithmique, de −10 à 10.</summary>
    internal static int RateOf(double rate) =>
        Math.Clamp((int)Math.Round(FastestRate * Math.Log(rate, RateBase)), -FastestRate, FastestRate);

    /// <summary>Arrête ce qui se dit. Une voix en pause doit reprendre pour que la suivante parle.</summary>
    private void Interrupt()
    {
        var (speaking, prompt) = (_speaking, _prompt);
        _speaking = null;
        _prompt = null;
        if (!_synthesizer.IsValueCreated)
        {
            speaking?.TrySetResult(Outcome.Done());
            return;
        }
        var synthesizer = _synthesizer.Value;
        if (prompt is not null)
        {
            synthesizer.SpeakAsyncCancel(prompt);
        }
        if (synthesizer.State == SynthesizerState.Paused)
        {
            synthesizer.Resume();
        }
        speaking?.TrySetResult(Outcome.Done());
    }

    private void OnCompleted(object? sender, SpeakCompletedEventArgs e)
    {
        if (!ReferenceEquals(e.Prompt, _prompt) || _speaking is not { } speaking)
        {
            return;
        }
        _speaking = null;
        _prompt = null;
        speaking.TrySetResult(e.Error is { } problem ? Outcome.Refused<Done>(problem.Message) : Outcome.Done());
    }
}
