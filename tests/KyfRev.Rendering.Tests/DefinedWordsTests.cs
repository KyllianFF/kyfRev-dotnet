namespace KyfRev.Rendering.Tests;

/// <summary>Les mots définis du coffre, cherchés dans le texte affiché d'une fiche.</summary>
[TestClass]
public sealed class DefinedWordsTests
{
    private static readonly (int Start, int End)[] Nothing = [];

    private static readonly string[] LongestFirst = ["d:état de\u00a0droit", "j:Aujourd’hui", "e:état", "j:Aujourd'hui"];

    private static readonly string[] NoAddedPlural = ["corps", "nez"];

    [TestMethod]
    public void A_term_is_found_as_a_whole_word_whatever_its_case_and_in_the_plural()
    {
        var map = Map("Une autorité naît. Les Autorités se suivent. Autoritaire n'en est pas une.");

        var found = DefinedWords.Find(map, [new DefinedTerm("a", "autorité")], Nothing);

        Assert.AreEqual("autorité", Text(map, found.Single()), "une fois par partie, et jamais dans « Autoritaire »");
        var everywhere = DefinedWords.Find(Map("Le pouvoir.\n\n## Suite\n\nDes Pouvoirs, pas de pouvoirsx."), [new DefinedTerm("p", "pouvoir")], Nothing);
        Assert.HasCount(2, everywhere);
        Assert.AreEqual("Pouvoirs", Text(Map("Le pouvoir.\n\n## Suite\n\nDes Pouvoirs, pas de pouvoirsx."), everywhere[1]));
    }

    [TestMethod]
    public void The_longest_term_wins_and_spaces_or_apostrophes_may_differ()
    {
        var map = Map("L'état de\u00a0droit protège. Aujourd’hui, l'état change.\n\n## Fin\n\nAujourd'hui encore.");
        DefinedTerm[] terms = [new("e", "état"), new("d", "État de droit"), new("j", "aujourd'hui")];

        var found = DefinedWords.Find(map, terms, Nothing);

        CollectionAssert.AreEqual(
            LongestFirst,
            found.Select(occurrence => $"{occurrence.Id}:{Text(map, occurrence)}").ToArray());
    }

    [TestMethod]
    public void Headings_code_links_formulas_and_taken_passages_are_left_alone()
    {
        var map = Map("## Le noyau\n\nUn `noyau` et [le noyau](https://exemple.fr) puis $noyau$.\n\nLe noyau surligné.\n\nEnfin, le noyau.");
        var taken = map.Text.IndexOf("noyau surligné", StringComparison.Ordinal);

        var found = DefinedWords.Find(map, [new DefinedTerm("n", "noyau")], [(taken, taken + 5)]);

        Assert.AreEqual(map.Text.LastIndexOf("noyau", StringComparison.Ordinal), found.Single().Start);
    }

    [TestMethod]
    public void A_term_defined_twice_keeps_its_first_definition_and_a_too_short_term_is_not_searched()
    {
        var map = Map("Un noyau, puis a, puis 42.");

        var found = DefinedWords.Find(map, [new DefinedTerm("ici", " Noyau "), new DefinedTerm("ailleurs", "noyau"), new DefinedTerm("court", "a"), new DefinedTerm("nombre", "42")], Nothing);

        Assert.AreEqual("ici", found.Single().Id);
        Assert.IsEmpty(DefinedWords.Find(map, [], Nothing));
        Assert.IsEmpty(DefinedWords.Find(Map(string.Empty), [new DefinedTerm("n", "noyau")], Nothing));
    }

    [TestMethod]
    public void A_term_ending_with_s_x_or_z_has_no_added_plural()
    {
        var map = Map("Le corps et les corpss ; le nez.");

        var found = DefinedWords.Find(map, [new DefinedTerm("c", "corps"), new DefinedTerm("n", "nez")], Nothing);

        CollectionAssert.AreEqual(NoAddedPlural, found.Select(occurrence => Text(map, occurrence)).ToArray());
    }

    [TestMethod]
    public void The_arguments_are_required()
    {
        var map = Map("Texte");
        Assert.ThrowsExactly<ArgumentNullException>(() => DefinedWords.Find(null!, [], Nothing));
        Assert.ThrowsExactly<ArgumentNullException>(() => DefinedWords.Find(map, null!, Nothing));
        Assert.ThrowsExactly<ArgumentNullException>(() => DefinedWords.Find(map, [], null!));
    }

    private static TextMap Map(string body) => TextMap.Of(SheetRenderer.Render(body, "Fiche"));

    private static string Text(TextMap map, TermOccurrence occurrence) => map.Text[occurrence.Start..occurrence.End];
}
