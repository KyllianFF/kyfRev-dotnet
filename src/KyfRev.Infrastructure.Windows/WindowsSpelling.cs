using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Le correcteur d'orthographe de Windows, par son interface COM. Ses appels se font sur les fils de travail,
/// qui partagent le même appartement : l'objet y est créé à la première demande.
/// </summary>
public sealed class WindowsSpelling : ISpelling
{
    private const int MaximumSuggestions = 5;
    private const int MaximumWord = 100;
    private const string SpellCheckerFactoryClass = "7AB36653-1796-484B-BDFA-E74F1DB7C1DC";

    private readonly Lazy<ISpellChecker?> _checker;
    private readonly Func<bool> _untilClosing;

    /// <summary>Le correcteur français ; <paramref name="untilClosing"/> dit si le coffre ouvert refuse qu'un mot aille dans le dictionnaire de Windows.</summary>
    public WindowsSpelling(Func<bool> untilClosing)
        : this("fr-FR", untilClosing)
    {
    }

    internal WindowsSpelling(string language, Func<bool> untilClosing)
    {
        ArgumentNullException.ThrowIfNull(untilClosing);
        _untilClosing = untilClosing;
        _checker = new Lazy<ISpellChecker?>(() => Create(language));
    }

    private enum CorrectiveAction
    {
        None = 0,
        GetSuggestions = 1,
        Replace = 2,
        Delete = 3,
    }

    [ComImport]
    [Guid("8E018A9D-2415-4677-BF08-794EA61F94BB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerFactory
    {
        IEnumString SupportedLanguages { get; }

        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag);

        ISpellChecker CreateSpellChecker([MarshalAs(UnmanagedType.LPWStr)] string languageTag);
    }

    /// <summary>Le début de l'interface, jusqu'à ce qui sert : l'ordre des méthodes est celui de Windows.</summary>
    [ComImport]
    [Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellChecker
    {
        string LanguageTag
        {
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
        }

        IEnumSpellingError Check([MarshalAs(UnmanagedType.LPWStr)] string text);

        IEnumString Suggest([MarshalAs(UnmanagedType.LPWStr)] string word);

        void Add([MarshalAs(UnmanagedType.LPWStr)] string word);

        void Ignore([MarshalAs(UnmanagedType.LPWStr)] string word);
    }

    [ComImport]
    [Guid("803E3BD4-2828-4410-8290-418D1D73C762")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumSpellingError
    {
        [PreserveSig]
        int Next(out ISpellingError? value);
    }

    [ComImport]
    [Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellingError
    {
        uint StartIndex { get; }

        uint Length { get; }

        CorrectiveAction CorrectiveAction { get; }

        string Replacement
        {
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
        }
    }

    /// <inheritdoc/>
    public bool AddsUntilClosing => _untilClosing();

    /// <inheritdoc/>
    public IReadOnlyList<SpellingMistake> Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Trim().Length == 0 ? [] : Guarded<IReadOnlyList<SpellingMistake>>(checker => [.. Errors(checker, text)
            .Where(error => error.CorrectiveAction is CorrectiveAction.GetSuggestions or CorrectiveAction.Replace)
            .Select(error => new SpellingMistake((int)error.StartIndex, (int)error.Length))], []);
    }

    /// <inheritdoc/>
    public SpellingCorrection? Correct(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return !IsWordLike(word) ? null : Guarded(checker =>
        {
            if (Errors(checker, word).FirstOrDefault() is not { } error)
            {
                return null;
            }
            var (start, length) = ((int)error.StartIndex, (int)error.Length);
            return error.CorrectiveAction switch
            {
                CorrectiveAction.Replace => new SpellingCorrection(start, length, [error.Replacement]),
                CorrectiveAction.GetSuggestions => new SpellingCorrection(start, length, Suggestions(checker.Suggest(word.Substring(start, length)))),
                _ => null,
            };
        }, default(SpellingCorrection));
    }

    /// <inheritdoc/>
    public bool Add(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return IsWordLike(word) && !word.Any(char.IsWhiteSpace) && Guarded(checker =>
        {
            // « Add » écrit dans %APPDATA%\Microsoft\Spelling, en clair ; « Ignore » ne dure que le temps de la session.
            if (AddsUntilClosing)
            {
                checker.Ignore(word);
            }
            else
            {
                checker.Add(word);
            }
            return true;
        }, false);
    }

    private static bool IsWordLike(string word) => word.Trim().Length > 0 && word.Length <= MaximumWord && !word.Any(char.IsControl);

    private static ISpellChecker? Create(string language)
    {
        try
        {
            var factory = (ISpellCheckerFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(SpellCheckerFactoryClass), throwOnError: true)!)!;
            return factory.IsSupported(language) ? factory.CreateSpellChecker(language) : null;
        }
        catch (Exception exception) when (exception is COMException or ArgumentException or InvalidCastException)
        {
            return null;
        }
    }

    private static IEnumerable<ISpellingError> Errors(ISpellChecker checker, string text)
    {
        var errors = checker.Check(text);
        while (errors.Next(out var error) == 0 && error is not null)
        {
            yield return error;
        }
    }

    private static List<string> Suggestions(IEnumString list)
    {
        var found = new List<string>();
        var next = new string[1];
        while (found.Count < MaximumSuggestions && list.Next(1, next, IntPtr.Zero) == 0)
        {
            found.Add(next[0]);
        }
        return found;
    }

    private T Guarded<T>(Func<ISpellChecker, T> call, T fallback)
    {
        if (_checker.Value is not { } checker)
        {
            return fallback;
        }
        try
        {
            return call(checker);
        }
        catch (Exception exception) when (exception is COMException or ArgumentException or InvalidCastException)
        {
            return fallback;
        }
    }
}
