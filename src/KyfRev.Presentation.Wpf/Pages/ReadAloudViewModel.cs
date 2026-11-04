using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une vitesse de lecture.</summary>
/// <param name="Value">Sa valeur : 1 pour la normale.</param>
/// <param name="Label">Ce qui s'affiche.</param>
public sealed record ReadingRate(double Value, string Label)
{
    /// <summary>Ce que dit le lecteur d'écran.</summary>
    public override string ToString() => Label;
}

/// <summary>
/// Écouter une fiche : les voix de Windows la lisent bloc par bloc, le bloc en cours mis en avant. On peut mettre
/// en pause, passer au bloc voisin, changer de vitesse ou de voix : la lecture reprend alors au bloc en cours.
/// </summary>
public sealed partial class ReadAloudViewModel : ObservableObject
{
    private readonly ISpeech _speech;
    private readonly MachineSettings? _machine;
    private IReadOnlyList<string> _blocks = [];
    private CancellationTokenSource? _reading;

    /// <summary>Le lecteur, sur ces voix ; <paramref name="machine"/> retient la voix et la vitesse choisies sur ce poste.</summary>
    public ReadAloudViewModel(ISpeech speech, MachineSettings? machine = null)
    {
        ArgumentNullException.ThrowIfNull(speech);
        _speech = speech;
        _machine = machine;
        Rate = Rates.FirstOrDefault(rate => rate.Value == machine?.Current.Rate) ?? Rates[1];
        Voice = machine?.Current.Voice;
    }

    /// <summary>Les vitesses proposées.</summary>
    public static IReadOnlyList<ReadingRate> Rates { get; } =
    [
        new(0.8, ReadingText.RateSlow),
        new(1, ReadingText.RateNormal),
        new(1.2, ReadingText.RateFaster),
        new(1.5, ReadingText.RateFast),
    ];

    /// <summary>Les voix françaises installées.</summary>
    public IReadOnlyList<string> Voices { get; private set; } = [];

    /// <summary>Le lecteur est ouvert.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Une lecture est en cours, même en pause.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel), nameof(PlayGlyph))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand), nameof(PreviousCommand), nameof(NextCommand))]
    public partial bool IsPlaying { get; private set; }

    /// <summary>La lecture est en pause.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel), nameof(PlayGlyph))]
    public partial bool IsPaused { get; private set; }

    /// <summary>La place du bloc en cours ; rien hors lecture.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand), nameof(NextCommand))]
    public partial int? Current { get; private set; }

    /// <summary>Ce qu'il faut savoir : pas de voix, rien à lire, une voix qui échoue.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>La vitesse choisie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RateLabel))]
    public partial ReadingRate Rate { get; set; }

    /// <summary>La voix choisie ; rien sans voix française.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceLabel))]
    public partial string? Voice { get; set; }

    /// <summary>Il y a un message.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>« 3 / 12 » : la place du bloc en cours.</summary>
    public string Progress => string.Create(CultureInfo.InvariantCulture, $"{(Current is { } index ? (index + 1).ToString(CultureInfo.InvariantCulture) : "–")} / {_blocks.Count}");

    /// <summary>Ce que fera le bouton principal.</summary>
    public string PlayLabel => !IsPlaying ? ReadingText.ListenPlay : IsPaused ? ReadingText.ListenResume : ReadingText.ListenPause;

    /// <summary>L'icône du bouton principal.</summary>
    public string PlayGlyph => !IsPlaying || IsPaused ? Icons.Play : Icons.Pause;

    /// <summary>« Vitesse : Normale ».</summary>
    public string RateLabel => ReadingText.RateOf(Rate.Label);

    /// <summary>« Voix : Hortense ».</summary>
    public string VoiceLabel => ReadingText.VoiceOf(Voice is { } voice ? ReadingText.ShortVoice(voice) : ReadingText.NoVoice);

    /// <summary>Ouvre le lecteur sur ces blocs, sans lancer la lecture.</summary>
    public void Open(IReadOnlyList<string> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        _blocks = blocks;
        Voices = _speech.FrenchVoices();
        OnPropertyChanged(nameof(Voices));
        if (Voice is null || !Voices.Contains(Voice))
        {
            Voice = Voices.Count > 0 ? Voices[0] : null;
        }
        Message = blocks.Count == 0 ? ReadingText.NothingToListen : Voices.Count == 0 ? ReadingText.NoFrenchVoice : null;
        OnPropertyChanged(nameof(Progress));
        IsOpen = true;
    }

    /// <summary>Lit à partir de ce bloc, s'il y a de quoi lire et une voix pour le faire.</summary>
    public void PlayFrom(int index)
    {
        if (_blocks.Count > 0 && Voice is not null)
        {
            Read(Math.Clamp(index, 0, _blocks.Count - 1));
        }
    }

    /// <summary>Lit, met en pause ou reprend.</summary>
    [RelayCommand]
    private void PlayPause()
    {
        if (!IsPlaying)
        {
            if (_blocks.Count > 0 && Voice is not null)
            {
                Read(0);
            }
        }
        else if (IsPaused)
        {
            IsPaused = false;
            _speech.ResumeSpeaking();
        }
        else
        {
            IsPaused = true;
            _speech.PauseSpeaking();
        }
    }

    /// <summary>Arrête la lecture.</summary>
    [RelayCommand(CanExecute = nameof(IsPlaying))]
    private void Stop()
    {
        Interrupt();
        IsPlaying = false;
        IsPaused = false;
        Current = null;
    }

    private bool HasPrevious() => IsPlaying && Current > 0;

    /// <summary>Passe au bloc précédent.</summary>
    [RelayCommand(CanExecute = nameof(HasPrevious))]
    private void Previous() => Read(Current!.Value - 1);

    private bool HasNext() => IsPlaying && Current < _blocks.Count - 1;

    /// <summary>Passe au bloc suivant.</summary>
    [RelayCommand(CanExecute = nameof(HasNext))]
    private void Next() => Read(Current!.Value + 1);

    /// <summary>Ferme le lecteur, et arrête la lecture.</summary>
    [RelayCommand]
    private void Close()
    {
        Stop();
        IsOpen = false;
    }

    partial void OnRateChanged(ReadingRate value)
    {
        _machine?.Update(preferences => preferences with { Rate = value.Value });
        ReadAgain();
    }

    partial void OnVoiceChanged(string? value)
    {
        if (value is not null)
        {
            _machine?.Update(preferences => preferences with { Voice = value });
        }
        ReadAgain();
    }

    /// <summary>Un réglage change : le bloc en cours repart avec lui.</summary>
    private void ReadAgain()
    {
        if (IsPlaying && Current is { } index)
        {
            Read(index);
        }
    }

    private void Interrupt()
    {
        _reading?.Cancel();
        _reading?.Dispose();
        _reading = null;
    }

    private async void Read(int start)
    {
        Interrupt();
        var reading = new CancellationTokenSource();
        var token = reading.Token;
        _reading = reading;
        IsPlaying = true;
        IsPaused = false;
        Message = null;
        for (var index = start; index < _blocks.Count; index++)
        {
            Current = index;
            var said = await _speech.SpeakAsync(_blocks[index], Voice, Rate.Value, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (!said.Succeeded)
            {
                Stop();
                Message = ReadingText.VoiceFailed(said.Error);
                return;
            }
        }
        Stop();
    }
}
