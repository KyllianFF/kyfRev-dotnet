namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// Les voix de Windows, sur une sortie muette : un poste peut n'en avoir aucune, comme un serveur.
/// L'application n'a qu'une synthèse à la fois : ces tests aussi. Le premier démarrage du moteur peut
/// prendre longtemps sur un poste neuf, d'où la patience.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class WindowsSpeechTests
{
    private static readonly TimeSpan EnginePatience = TimeSpan.FromMinutes(2);

    [TestMethod]
    [DataRow(1.0, 0)]
    [DataRow(0.8, -2)]
    [DataRow(1.2, 2)]
    [DataRow(1.5, 4)]
    [DataRow(3.0, 10)]
    [DataRow(100.0, 10)]
    [DataRow(0.01, -10)]
    public void A_rate_becomes_its_value_for_the_synthesis(double rate, int expected) =>
        Assert.AreEqual(expected, WindowsSpeech.RateOf(rate));

    [TestMethod]
    public void The_voices_are_french_and_sorted()
    {
        using var speech = Mute();

        var voices = speech.FrenchVoices();

        CollectionAssert.AreEqual(voices.Order(StringComparer.Ordinal).ToArray(), voices.ToArray());
    }

    [TestMethod]
    public async Task A_text_is_said_or_refused_but_never_left_waiting()
    {
        using var speech = Mute();

        var said = await speech.SpeakAsync("Un.", null, 1.5, CancellationToken.None).WaitAsync(EnginePatience);

        Assert.IsNotNull(said);
    }

    [TestMethod]
    public async Task An_unknown_voice_is_refused()
    {
        using var speech = Mute();

        var said = await speech.SpeakAsync("Un.", "Une voix qui n'existe pas", 1, CancellationToken.None).WaitAsync(EnginePatience);

        Assert.IsFalse(said.Succeeded);
        Assert.IsNotNull(said.Error);
    }

    [TestMethod]
    public async Task Cancelling_ends_at_once()
    {
        using var speech = Mute();
        using var cancellation = new CancellationTokenSource();
        var long_text = string.Join(' ', Enumerable.Repeat("Un texte assez long pour durer un moment.", 200));

        var saying = speech.SpeakAsync(long_text, null, 1, cancellation.Token);
        await cancellation.CancelAsync();

        await saying.WaitAsync(EnginePatience);
    }

    [TestMethod]
    public async Task A_new_text_ends_the_one_before_even_in_pause()
    {
        using var speech = Mute();
        var long_text = string.Join(' ', Enumerable.Repeat("Un texte assez long pour durer un moment.", 200));

        var first = speech.SpeakAsync(long_text, null, 1, CancellationToken.None);
        speech.PauseSpeaking();
        var second = speech.SpeakAsync("Deux.", null, 1, CancellationToken.None);

        await first.WaitAsync(EnginePatience);
        await second.WaitAsync(EnginePatience);
        speech.ResumeSpeaking();
    }

    [TestMethod]
    public void Disposing_without_having_spoken_does_nothing()
    {
        var speech = new WindowsSpeech();
        speech.Dispose();
    }

    private static WindowsSpeech Mute() => new(synthesizer => synthesizer.SetOutputToNull());
}
