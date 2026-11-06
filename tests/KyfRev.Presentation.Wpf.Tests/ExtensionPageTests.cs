using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue d'une extension, en éléments natifs : ce qu'on voit, ce qu'on clique, sur le vrai moteur.</summary>
[TestClass]
public sealed class ExtensionPageTests
{
    private const string Showcase = """
        let etat = { actions: [] };
        kyfrev.action("noter", (valeur, donnees) => { etat.actions.push(`${valeur}|${donnees.valeur ?? ""}|${donnees.autre ?? ""}`); });
        kyfrev.vue((contexte) => `
          <h1>Titre <em>penché</em></h1>
          <h2>Sous-titre</h2>
          <h3>Niveau trois</h3>
          <p class="secondaire">Thème ${contexte.theme}</p>
          <p>Un <strong>mot fort</strong>, du <code>code</code>, <small>petit</small><br>à la ligne, <span class="pastille">pastille</span> et <a href="https://fuite.invalid">un lien</a>.</p>
          <p class="titre-extension">Titre d'extension</p>
          <div class="carte">
            <p class="question">Une question ?</p>
            <p class="reponse">Sa réponse</p>
          </div>
          <p class="actions">
            <button class="bouton principal" data-action="noter" data-valeur="principal" data-autre="x">Principal</button>
            <button class="bouton" data-action="noter" data-valeur="normal">Normal</button>
            <button class="bouton discret" data-action="noter" data-valeur="discret" aria-label="Le discret">Discret</button>
            <button class="bouton" disabled data-action="noter">Éteint</button>
            <button class="bouton">Sans action</button>
          </p>
          <p class="actions">
            <input class="champ" type="search" placeholder="Filtrer" aria-label="Le filtre" value="début" data-action="noter">
            <input type="text" placeholder="Sans étiquette" data-action="noter">
            <input type="checkbox" aria-label="La case" checked data-action="noter">
            <input type="radio" aria-label="Le choix" data-action="noter">
            <select aria-label="La liste" data-action="noter"><option value="un">Premier</option><option value="deux" selected>Second</option><option>Troisième</option></select>
            <textarea placeholder="Long" data-action="noter">du texte</textarea>
          </p>
          <ul><li>Point A</li><li>Point B <strong>fort</strong></li></ul>
          <ol><li>Premier point</li><li>Second point</li></ol>
          <ol class="frise"><li><time>1789</time><span>Bastille</span></li><li><time>1792</time><span>République</span></li></ol>
          <table><thead><tr><th>Dossier</th><th>Mots</th></tr></thead><tbody><tr><td>Histoire</td><td>12</td></tr><tr><td>Seul</td></tr></tbody></table>
          <details><summary>À ouvrir</summary><p>Caché</p></details>
          <details open><p>Déjà ouvert</p></details>
          <pre>  gardé
          tel quel</pre>
          <hr>
          <div data-action="noter" data-valeur="bloc">Un bloc à cliquer</div>
          <p>Un <span data-action="noter" data-valeur="mot">mot à cliquer</span>.</p>
          <p>Un bouton <button class="bouton" data-action="noter" data-valeur="en-ligne">En ligne</button> dans le texte.</p>
          <li>Hors de sa liste</li>
          <summary>Résumé seul</summary>
          ${etat.actions.length ? `<p>Actions : ${etat.actions.join(" ; ")}</p>` : ""}
        `);
        """;

    private static readonly string[] OneBlock = ["x"];

    private static string Published => Path.Combine(Repository.Root, "tests", "fixtures", "extensions");

    [EngineTest]
    public void The_report_is_drawn_in_native_elements()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Library.Install(Path.Combine(Published, "bilan")).Succeeded);
        scene.Data.Text = "Un texte de quelques mots.";
        using var ui = new UiThread();
        var page = scene.Open("bilan");

        var title = Tree<TextBlock>(page).First(text => TextOf(text) == "Bilan du coffre");
        Assert.AreEqual(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(title));
        Assert.IsTrue(Tree<Grid>(page.Body).Any(grid => grid.ColumnDefinitions.Count == 4), "Le tableau par dossier.");
        Assert.IsTrue(Tree<TextBlock>(page).Any(text => TextOf(text).Contains("1 fiche(s) active(s)", StringComparison.Ordinal)));
        Assert.IsEmpty(Tree<WebBrowser>(page.Body));

        var recount = Tree<Button>(page).Single(button => (button.Content as string) == "Recalculer");
        scene.Data.Text = "Un texte avec beaucoup plus de mots encore ici.";
        Click(recount);
        Until(scene, () => Tree<TextBlock>(page).Any(text => TextOf(text).Contains("9 mots", StringComparison.Ordinal)));
    }

    [EngineTest]
    public void Each_element_of_the_white_list_has_its_native_form()
    {
        using var scene = new Scene();
        scene.Install("vitrine", Showcase);
        using var ui = new UiThread();
        var page = scene.Open("vitrine");

        var texts = Tree<TextBlock>(page).Select(TextOf).ToList();
        foreach (var expected in new[] { "Titre penché", "Sous-titre", "Niveau trois", "Thème clair", "Titre d'extension", "Une question ?", "Sa réponse", "Point A", "Premier point", "1.", "2.", "•", "1789", "Bastille", "Dossier", "12", "Hors de sa liste", "Résumé seul", "Un bloc à cliquer" })
        {
            CollectionAssert.Contains(texts, expected);
        }
        Assert.IsTrue(texts.Any(text => text.Contains("mot fort", StringComparison.Ordinal) && text.Contains("un lien", StringComparison.Ordinal)));
        Assert.IsTrue(texts.Any(text => text.Contains("gardé", StringComparison.Ordinal)));
        Assert.AreEqual(1, Tree<Card>(page).Count);

        var headings = Tree<TextBlock>(page).Select(AutomationProperties.GetHeadingLevel).Where(level => level != AutomationHeadingLevel.None).ToList();
        CollectionAssert.AreEqual(new[] { AutomationHeadingLevel.Level1, AutomationHeadingLevel.Level2, AutomationHeadingLevel.Level3 }, headings);

        var buttons = Tree<Button>(page).Where(button => button.Content is string).ToDictionary(button => (string)button.Content);
        Assert.IsTrue(buttons["Principal"].IsEnabled);
        Assert.IsFalse(buttons["Éteint"].IsEnabled);
        Assert.IsFalse(buttons["Sans action"].IsEnabled);
        Assert.AreEqual("Le discret", AutomationProperties.GetName(buttons["Discret"]));
        Assert.IsFalse(Tree<FrameworkElement>(page.Body).Any(element => element.Margin.Left < -6 || element.ActualWidth > page.ActualWidth + 1), "Rien ne sort de la vue.");
        Assert.IsFalse(Inlines(page).OfType<Hyperlink>().Any(link => link.NavigateUri is not null), "Un lien d'extension n'ouvre rien.");
        Assert.HasCount(1, Inlines(page).OfType<Hyperlink>().ToList());
    }

    [EngineTest]
    public void Buttons_fields_and_lists_give_their_action_and_the_view_is_drawn_again()
    {
        using var scene = new Scene();
        scene.Install("vitrine", Showcase);
        using var ui = new UiThread();
        var page = scene.Open("vitrine");
        Button Named(string name) => Tree<Button>(page).Single(button => (button.Content as string) == name);

        Click(Named("Principal"));
        Until(scene, () => Says(page, "principal|principal|x"));

        Click(Tree<Button>(page).Single(button => button.Content is StackPanel));
        Until(scene, () => Says(page, "bloc|bloc|"));

        Inlines(page).OfType<Hyperlink>().Single().DoClick();
        Until(scene, () => Says(page, "mot|mot|"));

        Click(Named("En ligne"));
        Until(scene, () => Says(page, "en-ligne|en-ligne|"));

        var field = Tree<TextBox>(page).Single(box => AutomationProperties.GetName(box) == "Le filtre");
        Assert.AreEqual("début", field.Text);
        Assert.AreEqual("Filtrer", Field.GetPlaceholder(field));
        Assert.IsGreaterThanOrEqualTo(280, field.MinWidth);
        field.Text = "cherché";
        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(field)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        Until(scene, () => Says(page, "cherché||"));

        Tree<TextBox>(page).Single(box => AutomationProperties.GetName(box) == "Sans étiquette").Text = "x";
        var other = Tree<TextBox>(page).Single(box => AutomationProperties.GetName(box) == "Sans étiquette");
        other.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, other, null) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
        Until(scene, () => Says(page, "x||"));

        var area = Tree<TextBox>(page).Single(box => box.AcceptsReturn);
        Assert.AreEqual("du texte", area.Text);
        area.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(area)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });

        var box = Tree<CheckBox>(page).Single();
        Assert.IsTrue(box.IsChecked);
        Assert.AreEqual("La case", AutomationProperties.GetName(box));
        box.IsChecked = false;
        Until(scene, () => Says(page, "false||"));

        Tree<RadioButton>(page).Single().IsChecked = true;
        Until(scene, () => Says(page, "true||"));

        var list = Tree<ComboBox>(page).Single();
        Assert.AreEqual(1, list.SelectedIndex);
        Assert.AreEqual("La liste", AutomationProperties.GetName(list));
        list.SelectedIndex = 0;
        Until(scene, () => Says(page, "un||"));
        Tree<ComboBox>(page).Single().SelectedIndex = 2;
        Until(scene, () => Says(page, "Troisième||"));
    }

    [EngineTest]
    public void A_details_opens_by_itself_without_asking_the_extension()
    {
        using var scene = new Scene();
        scene.Install("vitrine", Showcase);
        using var ui = new UiThread();
        var page = scene.Open("vitrine");
        var toggles = Tree<ToggleButton>(page).Where(toggle => toggle is not CheckBox and not RadioButton && toggle.Content is DockPanel).ToList();
        Assert.HasCount(2, toggles);
        var hidden = (FrameworkElement)((StackPanel)toggles[0].Parent).Children[1];
        var open = (FrameworkElement)((StackPanel)toggles[1].Parent).Children[1];

        Assert.AreEqual("À ouvrir", AutomationProperties.GetName(toggles[0]));
        Assert.AreEqual("Détails", AutomationProperties.GetName(toggles[1]), "Sans résumé, le nom d'un navigateur.");
        Assert.AreEqual(Visibility.Collapsed, hidden.Visibility);
        Assert.AreEqual(Visibility.Visible, open.Visibility);

        toggles[0].IsChecked = true;
        Assert.AreEqual(Visibility.Visible, hidden.Visibility);
        Assert.AreEqual(Icons.ChevronDown, Tree<Icon>(toggles[0]).Single().Text);
        toggles[0].IsChecked = false;
        Assert.AreEqual(Visibility.Collapsed, hidden.Visibility);
        Assert.AreEqual(Icons.ChevronRight, Tree<Icon>(toggles[0]).Single().Text);
        Assert.IsFalse(scene.Page!.IsBusy);
    }

    [EngineTest]
    public void The_view_draws_in_the_dark_theme_when_asked()
    {
        using var scene = new Scene { IsDark = true };
        scene.Install("vitrine", Showcase);
        using var ui = new UiThread();
        var page = scene.Open("vitrine");

        CollectionAssert.Contains(Tree<TextBlock>(page).Select(TextOf).ToList(), "Thème sombre");
    }

    [EngineTest]
    public void A_refusal_is_said_and_what_was_shown_stays()
    {
        using var scene = new Scene();
        scene.Install("capricieuse", """
            kyfrev.vue(() => "affiché");
            kyfrev.action("casse", () => { throw new Error("elle casse"); });
            """);
        using var ui = new UiThread();
        var page = scene.Open("capricieuse");
        Assert.IsFalse(scene.Page!.HasError);

        var acting = scene.Page.ActAsync(new ExtensionAction("casse", null, new Dictionary<string, string>()));
        Until(scene, () => acting.IsCompleted);

        Assert.IsTrue(scene.Page.HasError);
        StringAssert.Contains(scene.Page.Error, "elle casse", StringComparison.Ordinal);
        Assert.IsTrue(page.ErrorMessage.IsOpen);
        CollectionAssert.Contains(Tree<TextBlock>(page).Select(TextOf).ToList(), "affiché");
    }

    [EngineTest]
    public void An_extension_asks_for_a_new_drawing_when_it_wants()
    {
        using var scene = new Scene();
        scene.Install("vivante", """
            let tours = 0;
            kyfrev.vue(() => `tour ${++tours}`);
            kyfrev.action("plus-tard", () => { setTimeout(() => kyfrev.rafraichir(), 10); });
            """);
        using var ui = new UiThread();
        var page = scene.Open("vivante");
        CollectionAssert.Contains(Tree<TextBlock>(page).Select(TextOf).ToList(), "tour 1");
        scene.Engine.RefreshAsked += (_, _) => _ = scene.Page!.RenderAsync();

        var acting = scene.Engine.ActAsync("vivante", "plus-tard", null, new Dictionary<string, string>());

        Until(scene, () => acting.IsCompleted && Tree<TextBlock>(page).Any(text => TextOf(text) == "tour 2"));
    }

    [EngineTest]
    public void One_action_at_a_time()
    {
        using var scene = new Scene();
        scene.Install("lente", """
            let fois = 0;
            kyfrev.vue(() => `fois ${fois}`);
            kyfrev.action("lent", () => new Promise((fin) => { fois += 1; setTimeout(fin, 200); }));
            """);
        using var ui = new UiThread();
        var page = scene.Open("lente");

        var first = scene.Page!.ActAsync(new ExtensionAction("lent", null, new Dictionary<string, string>()));
        var second = scene.Page.ActAsync(new ExtensionAction("lent", null, new Dictionary<string, string>()));

        Assert.IsTrue(second.IsCompleted);
        Until(scene, () => first.IsCompleted);
        CollectionAssert.Contains(Tree<TextBlock>(page).Select(TextOf).ToList(), "fois 1");
    }

    [EngineTest]
    public void The_focus_goes_back_where_the_action_was()
    {
        using var scene = new Scene();
        scene.Install("clavier", """
            let suite = false;
            kyfrev.vue(() => suite
              ? `<p class="actions"><button data-action="fin">Autre</button></p>`
              : `<p class="actions"><button data-action="garder">Garder</button><button data-action="changer">Changer</button></p>`);
            kyfrev.action("garder", () => {});
            kyfrev.action("changer", () => { suite = true; });
            kyfrev.action("fin", () => { suite = false; });
            """);
        using var ui = new UiThread();
        var page = scene.Open("clavier");
        scene.Window.Activate();
        Tree<Button>(page.Body).First().Focus();

        var keeping = scene.Page!.ActAsync(new ExtensionAction("garder", null, new Dictionary<string, string>()));
        Until(scene, () => keeping.IsCompleted);
        TestShell.Flush();
        Assert.AreEqual("garder", scene.Page.LastAction);

        var changing = scene.Page.ActAsync(new ExtensionAction("changer", null, new Dictionary<string, string>()));
        Until(scene, () => changing.IsCompleted);
        TestShell.Flush();
        Assert.AreEqual("Autre", Tree<Button>(page.Body).Single().Content);

        scene.Page.OnNavigatedTo();
        Assert.IsNull(scene.Page.LastAction);
        Assert.IsTrue(page.Scroller.Focusable, "Sans contrôle dans un rendu, le focus reste sur la vue.");
        Assert.IsFalse(page.Scroller.IsTabStop);
    }

    [TestMethod]
    public void The_view_needs_its_engine_and_its_theme()
    {
        var library = new ExtensionLibrary("x", TimeProvider.System);
        using var engine = new ExtensionEngine("x", library, new ExtensionRequests(library, new FakeData()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionPageViewModel("a", "A", null!, () => false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionPageViewModel("a", "A", engine, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionPages(null!, engine, () => false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionPages(library, null!, () => false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionPages(library, engine, null!));
    }

    [EngineTest]
    public void The_menu_has_an_entry_for_each_active_extension_with_a_view()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Library.Install(Path.Combine(Published, "bilan")).Succeeded);
        Assert.IsTrue(scene.Library.Install(Path.Combine(Published, "quiz")).Succeeded);
        scene.Install("sans-vue", """kyfrev.bloc("x", () => "");""", view: false);
        using var ui = new UiThread();
        var pages = new ExtensionPages(scene.Library, scene.Engine, () => false);

        var entries = pages.Entries();

        Assert.AreEqual("Bilan du coffre, Quiz", string.Join(", ", entries.Select(entry => entry.Label)));
        Assert.AreEqual("extension:bilan", entries[0].Id);
        Assert.AreEqual(Icons.Puzzle, entries[0].Glyph);
        Assert.IsLessThan(entries[1].Order, entries[0].Order);
        Assert.IsFalse(entries[0].IsSecondary);
        Assert.AreSame(entries[0].Page, pages.Entries()[0].Page, "Sa vue reste la même d'un menu à l'autre.");

        scene.Library.SetActive("bilan", false);
        Assert.AreEqual("Quiz", pages.Entries().Single().Label);
    }

    [EngineTest]
    public void A_refresh_reaches_the_page_of_its_extension_and_a_change_is_forgotten()
    {
        using var scene = new Scene();
        var source = scene.Install("suivie", """
            let tours = 0;
            kyfrev.vue(() => `tour ${++tours}`);
            kyfrev.action("encore", () => { kyfrev.rafraichir(); });
            """);
        scene.Install("autre", """
            kyfrev.vue(() => "autre");
            kyfrev.action("encore", () => { kyfrev.rafraichir(); });
            """, view: true);
        using var ui = new UiThread();
        var pages = new ExtensionPages(scene.Library, scene.Engine, () => false);
        var page = (ExtensionPageViewModel)pages.Entries().Single(entry => entry.Id == "extension:suivie").Page;
        page.OnNavigatedTo();
        Until(scene, () => page.Content?.Text == "tour 1");

        var acting = scene.Engine.ActAsync("suivie", "encore", null, new Dictionary<string, string>());
        Until(scene, () => acting.IsCompleted && page.Content?.Text == "tour 2");
        scene.Engine.ActAsync("autre", "encore", null, new Dictionary<string, string>());

        File.WriteAllText(Path.Combine(source, "extension.js"), """kyfrev.vue(() => "nouvelle");""");
        scene.Library.Install(source);
        pages.Forget("suivie");
        var rendering = page.RenderAsync();
        Until(scene, () => rendering.IsCompleted);
        Assert.AreEqual("nouvelle", page.Content!.Text);

        pages.VaultChanged();
        var again = page.RenderAsync();
        Until(scene, () => again.IsCompleted);
        Assert.AreEqual("nouvelle", page.Content!.Text);
    }

    [EngineTest]
    public void A_form_sends_its_named_fields_together_and_a_progress_shows_how_far()
    {
        using var scene = new Scene();
        scene.Install("formulaire", """
            let recu = "rien";
            kyfrev.action("creer", (valeur, donnees) => { recu = JSON.stringify(valeur) + "|" + (donnees.zone ?? ""); });
            kyfrev.action("annuler", () => { recu = "annulé"; });
            kyfrev.vue(() => `
              <p>Reçu : ${recu.replace(/</g, "")}</p>
              <progress value="3" max="12" aria-label="Avancement"></progress>
              <progress value="20"></progress>
              <form data-action="creer" data-zone="z">
                <label>Nom <input name="nom" required></label>
                <label>Étiquettes <input name="etiquettes" value="a, b" placeholder="séparées par des virgules"></label>
                <label>Thème <input name="theme" aria-label="Le thème" placeholder="facultatif"></label>
                <label><input type="checkbox" name="hasard" checked> Au hasard</label>
                <input type="radio" name="mode" value="fiche" checked aria-label="Dans la fiche">
                <input type="radio" name="mode" value="ici" aria-label="Ici">
                <input type="radio" name="vide" value="x" aria-label="Aucun">
                <select name="ordre" aria-label="Ordre"><option value="h">Au hasard</option><option selected>Dans l'ordre</option></select>
                <textarea name="note" aria-label="Note">n</textarea>
                <button type="button">Rien</button>
                <button data-action="annuler">Annuler</button>
                <button class="principal">Commencer</button>
              </form>
              <form><input name="seul" aria-label="Seul"><button>Sans action</button></form>`);
            """);
        using var ui = new UiThread();
        var page = scene.Open("formulaire");
        Button Named(string name) => Tree<Button>(page).Single(button => (button.Content as string) == name);
        TextBox Field(string name) => Tree<TextBox>(page).Single(box => AutomationProperties.GetName(box) == name);

        var bars = Tree<ProgressBar>(page);
        Assert.AreEqual((3d, 12d, "Avancement"), (bars[0].Value, bars[0].Maximum, AutomationProperties.GetName(bars[0])));
        Assert.AreEqual((1d, 1d, "1 / 1"), (bars[1].Value, bars[1].Maximum, AutomationProperties.GetName(bars[1])));
        Assert.IsTrue(AutomationProperties.GetIsRequiredForForm(Field("Nom")));
        Assert.AreEqual("séparées par des virgules", AutomationProperties.GetHelpText(Field("Étiquettes")), "Le libellé nomme le champ, l'indication reste une aide.");
        Assert.AreEqual("facultatif", KyfRev.DesignSystem.Controls.Field.GetPlaceholder(Field("Le thème")), "Un aria-label passe avant le libellé.");
        Assert.AreEqual("Au hasard", Tree<CheckBox>(page).Single().Content);
        Assert.IsFalse(Named("Rien").IsEnabled, "type=\"button\" n'envoie pas le formulaire.");
        Assert.IsFalse(Named("Sans action").IsEnabled, "Un formulaire sans action n'a rien à envoyer.");

        Click(Named("Commencer"));
        TestShell.Flush();
        Assert.IsNull(scene.Page!.LastAction, "Un champ obligatoire vide garde le formulaire.");

        Field("Nom").Text = "Autorité";
        Field("Étiquettes").RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Field("Étiquettes"))!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        Until(scene, () => Says(page, """Reçu : {"etiquettes":"a, b","hasard":true,"mode":"fiche","nom":"Autorité","note":"n","ordre":"Dans l'ordre","theme":"","vide":null}|z"""));

        Click(Named("Annuler"));
        Until(scene, () => Says(page, "Reçu : annulé"));

        Field("Nom").Text = "Autre";
        var radios = Tree<RadioButton>(page);
        radios.Single(radio => AutomationProperties.GetName(radio) == "Ici").IsChecked = true;
        Assert.IsFalse(radios.Single(radio => AutomationProperties.GetName(radio) == "Dans la fiche").IsChecked, "Les boutons d'un même nom vont ensemble.");
        Tree<CheckBox>(page).Single().IsChecked = false;
        Click(Named("Commencer"));
        Until(scene, () => Says(page, "\"hasard\":false,\"mode\":\"ici\",\"nom\":\"Autre\""));
        Field("Seul").RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Field("Seul"))!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    [EngineTest]
    public void An_extension_adds_its_entry_its_commands_its_menus_and_its_card()
    {
        using var scene = new Scene();
        scene.InstallWith(
            "quiz",
            """
            {"api":2,"id":"quiz","nom":"Quiz","version":"2","icone":"quiz.svg",
             "vue":{"titre":"Quiz","compteur":true},
             "commandes":[{"id":"ajouter","titre":"Ajouter"},{"id":"casser","titre":"Casser"},{"id":"ouvrir","titre":"Ouvrir"}],
             "menus":{"fiche":[{"commande":"ajouter","titre":"Ajouter ce quiz"}],"dossier":[{"commande":"ajouter","titre":"Quiz du dossier"}]},
             "encart":{"titre":"Quiz"}}
            """,
            """
            let n = 0;
            let dernier = "";
            kyfrev.vue(() => `${n} ${dernier}`);
            kyfrev.action("plus", () => { n += 1; });
            kyfrev.action("signaler", () => { n += 1; kyfrev.rafraichir(); });
            kyfrev.commande("ajouter", (c) => { n += 1; dernier = c.titre ?? c.dossier ?? "recherche"; });
            kyfrev.commande("casser", () => { throw new Error("elle casse"); });
            kyfrev.commande("ouvrir", () => kyfrev.ouvrirVue());
            kyfrev.compteur(() => n);
            kyfrev.encart(() => n > 0 ? { texte: `${n} à revoir`, detail: "aujourd'hui" } : null);
            """,
            ("quiz.svg", """<svg viewBox="0 0 24 24" fill="none"><path d="M4 4h16v16H4Z"/></svg>"""));
        scene.Install("simple", """kyfrev.vue(() => "simple");""");
        using var ui = new UiThread();
        var pages = new ExtensionPages(scene.Library, scene.Engine, () => false);
        var opened = new List<string>();
        pages.ViewAsked += (_, e) => opened.Add(e.Extension);

        var entries = pages.Entries();
        var quiz = entries.Single(entry => entry.Id == "extension:quiz");
        var simple = entries.Single(entry => entry.Id == "extension:simple");
        Assert.IsTrue(quiz.HasPicture);
        Assert.IsTrue(quiz.Picture!.Stroked);
        Assert.IsTrue(quiz.Picture.Geometry.IsFrozen);
        Assert.IsFalse(quiz.Badge!.IsShown);
        Assert.IsFalse(simple.HasPicture);
        Assert.IsNull(simple.Badge);
        Assert.AreSame(quiz.Badge, pages.Entries().Single(entry => entry.Id == "extension:quiz").Badge, "Le compteur reste le même d'un menu à l'autre.");
        Wait(pages.RefreshCountsAsync());
        Assert.IsNull(quiz.Badge.Text);
        Assert.IsEmpty(Wait(pages.CardsAsync()));

        var commands = pages.Commands();
        Assert.AreEqual("Quiz : Ajouter, Quiz : Casser, Quiz : Ouvrir", string.Join(", ", commands.Select(command => command.Label)));
        commands[0].Execute();
        Until(scene, () => quiz.Badge.Text == "1");

        var sheet = pages.MenuEntries(ExtensionMenuZone.Sheet, new Dictionary<string, string> { ["id"] = "f_1", ["titre"] = "Une fiche", ["dossier"] = "Histoire" });
        Assert.AreEqual("Ajouter ce quiz", sheet.Single().Label);
        sheet.Single().Run();
        Until(scene, () => quiz.Badge.Text == "2");
        var folder = pages.MenuEntries(ExtensionMenuZone.Folder, new Dictionary<string, string> { ["dossier"] = "Histoire" });
        Assert.AreEqual("Quiz du dossier", folder.Single().Label);

        var page = (ExtensionPageViewModel)quiz.Page;
        page.OnNavigatedTo();
        UiThread.Until(() => page.Content is not null, TimeSpan.FromMinutes(2));
        Assert.AreEqual("2 Une fiche", page.Content!.Text.Trim());
        folder.Single().Run();
        Until(scene, () => quiz.Badge.Text == "3" && page.Content?.Text.Trim() == "3 Histoire");

        var card = Wait(pages.CardsAsync()).Single();
        Assert.AreEqual(new DashboardCard("Quiz", "3 à revoir", "aujourd'hui"), card);

        commands[1].Execute();
        UiThread.Until(() => page.HasError, TimeSpan.FromMinutes(2));
        StringAssert.Contains(page.Error, "elle casse", StringComparison.Ordinal);

        commands[2].Execute();
        UiThread.Until(() => opened.Count == 1, TimeSpan.FromMinutes(2));
        Assert.AreEqual("quiz", opened.Single());

        var acting = page.ActAsync(new ExtensionAction("plus", null, new Dictionary<string, string>()));
        Until(scene, () => acting.IsCompleted && quiz.Badge.Text == "4");
        var telling = scene.Engine.ActAsync("quiz", "signaler", null, new Dictionary<string, string>());
        Until(scene, () => telling.IsCompleted && quiz.Badge.Text == "5");

        scene.Library.SetActive("quiz", false);
        Assert.IsEmpty(pages.Commands());
        Assert.IsEmpty(pages.MenuEntries(ExtensionMenuZone.Sheet, new Dictionary<string, string>()));
        Wait(pages.RefreshCountsAsync());
        Assert.AreEqual("5", quiz.Badge.Text, "Une extension arrêtée n'est plus interrogée.");
    }

    [EngineTest]
    public void A_block_of_a_sheet_is_drawn_by_its_extension_and_stays_text_without_it()
    {
        using var scene = new Scene();
        scene.InstallWith("bloc-quiz", """{"id":"bloc-quiz","nom":"Q","version":"1","blocs":["quiz"]}""", """
            let ouvertes = 0;
            kyfrev.bloc("quiz", (source) => {
              if (source.startsWith("casse")) throw new Error("bloc illisible");
              return `<p>${source.split("\n").length} ligne(s), ${ouvertes} ouverte(s)</p><p class="actions"><button data-action="ouvrir">Ouvrir</button><button data-action="rate">Rater</button></p>`;
            });
            kyfrev.action("ouvrir", () => { ouvertes += 1; });
            kyfrev.action("rate", () => { throw new Error("raté"); });
            """);
        using var ui = new UiThread();
        var blocks = new ExtensionBlocks(scene.Library, scene.Engine, () => false);
        Assert.IsTrue(blocks.Draws("quiz"));
        Assert.IsFalse(blocks.Draws("python"));
        Assert.IsFalse(blocks.Draws(null));
        Assert.AreEqual("quiz", string.Join(',', blocks.Languages));
        var sheet = new OpenedSheet("f_1", "Mitose", "fiches/Mitose.md", "Biologie", [], "```quiz\nQ: a\nR: b\n```\n\n```python\nx = 1\n```", 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []);
        var document = new DocumentBuilder(SheetReading.Of(sheet, _ => Outcome.Refused<byte[]>("non")), _ => { }, true, false) { ExtensionBlocks = blocks }.Build();
        var container = document.Blocks.OfType<BlockUIContainer>().Single();
        var view = (ExtensionBlockView)container.Child;
        Assert.AreEqual("quiz", view.BlockLanguage);
        Assert.AreEqual("Q: a\nR: b", view.Source);
        Assert.AreEqual(1, document.Blocks.OfType<Paragraph>().Count(), "Un bloc d'un autre langage reste du code.");

        var test = new TestShell();
        var host = new Border { Width = 700, Child = new StackPanel() };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        host.Resources[ExtensionBlocks.ResourceKey] = blocks;
        container.Child = null;
        ((StackPanel)host.Child).Children.Add(view);
        var window = new Window { Content = host, Left = -10000, Top = -10000, Width = 750, Height = 500, ShowActivated = false };
        window.Show();
        try
        {
            UiThread.Until(() => view.ShowsContent, TimeSpan.FromMinutes(2));
            Assert.IsTrue(Tree<TextBlock>(view).Any(text => TextOf(text) == "2 ligne(s), 0 ouverte(s)"));
            Assert.AreEqual(ReadingText.ExtensionBlockOf("quiz"), AutomationProperties.GetName(view));

            Click(Tree<Button>(view).Single(button => (string)button.Content == "Ouvrir"));
            UiThread.Until(() => { TestShell.Flush(); return Tree<TextBlock>(view).Any(text => TextOf(text) == "2 ligne(s), 1 ouverte(s)"); }, TimeSpan.FromMinutes(2));

            Click(Tree<Button>(view).Single(button => (string)button.Content == "Rater"));
            UiThread.Until(() => { TestShell.Flush(); return !view.ShowsContent; }, TimeSpan.FromMinutes(2));
            StringAssert.Contains(AutomationProperties.GetName(view), "raté", StringComparison.Ordinal);

            view.Source = "casse";
            UiThread.Until(() => view.Drawing.IsCompleted, TimeSpan.FromMinutes(2));
            Assert.IsFalse(view.ShowsContent);
            StringAssert.Contains(AutomationProperties.GetName(view), "bloc illisible", StringComparison.Ordinal);
            Assert.IsTrue(Tree<TextBlock>(view).Any(text => TextOf(text) == "casse"), "Son texte reste lisible.");

            scene.Library.SetActive("bloc-quiz", false);
            blocks.Forget();
            Assert.IsFalse(blocks.Draws("quiz"));
            view.Source = "Q: c";
            UiThread.Until(() => view.Drawing.IsCompleted, TimeSpan.FromMinutes(2));
            StringAssert.Contains(AutomationProperties.GetName(view), ReadingText.NoExtensionBlockOf("quiz"), StringComparison.Ordinal);
            Assert.IsFalse(Wait(blocks.ActAsync("quiz", new ExtensionAction("ouvrir", null, new Dictionary<string, string>()))).Succeeded);

            host.Resources.Remove(ExtensionBlocks.ResourceKey);
            view.BlockLanguage = "autre";
            UiThread.Until(() => view.Drawing.IsCompleted, TimeSpan.FromMinutes(2));
            Assert.IsFalse(view.ShowsContent);
        }
        finally
        {
            window.Close();
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionBlocks(null!, scene.Engine, () => false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionBlocks(scene.Library, null!, () => false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionBlocks(scene.Library, scene.Engine, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => blocks.ActAsync("quiz", null!));
    }

    [TestMethod]
    public void A_selected_text_has_the_entries_of_the_extensions_that_may_see_it()
    {
        using var scene = new Scene();
        scene.InstallWith("quiz-sel", """{"api":2,"id":"quiz-sel","nom":"Q","version":"1","permissions":["selection"],"commandes":[{"id":"ajouter","titre":"Ajouter"}],"menus":{"selection":[{"commande":"ajouter","titre":"Ajouter au quiz"}]}}""", "kyfrev.commande('ajouter', () => {});");
        var pages = new ExtensionPages(scene.Library, scene.Engine, () => false);

        Assert.AreEqual("Ajouter au quiz", pages.MenuEntries(ExtensionMenuZone.Selection, new Dictionary<string, string>()).Single().Label);
        Assert.IsEmpty(pages.MenuEntries(ExtensionMenuZone.Sheet, new Dictionary<string, string>()));
        scene.Library.SetGranted("quiz-sel", ExtensionRight.Selection, false);
        Assert.IsEmpty(pages.MenuEntries(ExtensionMenuZone.Selection, new Dictionary<string, string>()), "Sans la permission, la sélection ne lui est pas proposée.");
    }

    [TestMethod]
    public void An_icon_becomes_a_geometry_and_a_path_that_does_not_read_is_left_out()
    {
        Assert.IsNull(ExtensionPages.PictureOf(null));
        Assert.IsNull(ExtensionPages.PictureOf(new ExtensionIcon(0, 0, 1, 1, ["M0 0 L"], Stroked: false)));
        var picture = ExtensionPages.PictureOf(new ExtensionIcon(0, 0, 1, 1, ["M0 0 L", "M0 0L1 1"], Stroked: false))!;
        Assert.IsFalse(picture.Stroked);
        Assert.AreEqual(1, ((GeometryGroup)picture.Geometry).Children.Count);
        Assert.AreEqual("extension:quiz", ExtensionPages.EntryId("quiz"));
    }

    private static T Wait<T>(Task<T> task)
    {
        UiThread.Until(() => task.IsCompleted, TimeSpan.FromMinutes(2));
        return task.Result;
    }

    private static void Wait(Task task) => UiThread.Until(() => task.IsCompleted, TimeSpan.FromMinutes(2));

    private static string TextOf(TextBlock text) => new TextRange(text.ContentStart, text.ContentEnd).Text.Trim();

    private static bool Says(ExtensionPage page, string words) =>
        Tree<TextBlock>(page).Any(text => TextOf(text).Contains(words, StringComparison.Ordinal));

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Until(Scene scene, Func<bool> condition)
    {
        UiThread.Until(() =>
        {
            TestShell.Flush();
            return condition() || scene.Page is { HasError: true };
        }, TimeSpan.FromMinutes(2));
        Assert.IsTrue(condition(), scene.Page?.Error);
    }

    private static List<T> Tree<T>(DependencyObject root)
        where T : DependencyObject
    {
        var found = new List<T>();
        void Walk(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    found.Add(match);
                }
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }

    private static IEnumerable<Inline> Inlines(ExtensionPage page)
    {
        static IEnumerable<Inline> Walk(IEnumerable<Inline> inlines) =>
            inlines.SelectMany(inline => inline is Span span ? Walk(span.Inlines).Prepend(inline) : [inline]);
        return Tree<TextBlock>(page).SelectMany(text => Walk(text.Inlines));
    }

    /// <summary>Une bibliothèque, un moteur et une fenêtre d'essai.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kyfrev-extensions-" + Guid.NewGuid().ToString("N"));

        public Scene()
        {
            Directory.CreateDirectory(_root);
            Library = new ExtensionLibrary(Path.Combine(_root, "bibliotheque"), TimeProvider.System);
            Engine = new ExtensionEngine(FakeEnvironment.ExtensionData, Library, new ExtensionRequests(Library, Data));
        }

        public ExtensionLibrary Library { get; }

        public FakeData Data { get; } = new();

        public ExtensionEngine Engine { get; }

        public bool IsDark { get; set; }

        public ExtensionPageViewModel? Page { get; private set; }

        public Window Window { get; private set; } = null!;

        public string Install(string id, string script, bool view = true)
        {
            var source = Path.Combine(_root, "sources", id);
            Directory.CreateDirectory(source);
            var manifest = view
                ? System.Text.Json.JsonSerializer.Serialize(new { id, nom = id, version = "1", vue = new { titre = id } })
                : System.Text.Json.JsonSerializer.Serialize(new { id, nom = id, version = "1", blocs = OneBlock });
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), manifest);
            File.WriteAllText(Path.Combine(source, "extension.js"), script);
            Assert.IsTrue(Library.Install(source).Succeeded);
            return source;
        }

        /// <summary>Installe une extension d'essai avec son manifeste et d'autres fichiers.</summary>
        public void InstallWith(string id, string manifest, string script, params (string Name, string Content)[] files)
        {
            var source = Path.Combine(_root, "sources", id);
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), manifest);
            File.WriteAllText(Path.Combine(source, "extension.js"), script);
            foreach (var (name, content) in files)
            {
                File.WriteAllText(Path.Combine(source, name), content);
            }
            var installed = Library.Install(source);
            Assert.IsTrue(installed.Succeeded, installed.Error);
        }

        /// <summary>Ouvre la vue d'une extension et attend qu'elle soit dessinée.</summary>
        public ExtensionPage Open(string id)
        {
            var test = new TestShell();
            Page = new ExtensionPageViewModel(id, id, Engine, () => IsDark);
            var view = new ExtensionPage { DataContext = Page };
            var host = new Border { Width = 900, Height = 2000, Child = view };
            host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
            var canvas = new Canvas();
            canvas.Children.Add(host);
            Window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 950, Height = 2100, ShowActivated = false };
            Window.Show();
            TestShell.Flush();
            Page.OnNavigatedTo();
            UiThread.Until(() => Page.Content is not null || Page.HasError, TimeSpan.FromMinutes(2));
            Assert.IsFalse(Page.HasError, Page.Error);
            TestShell.Flush();
            return view;
        }

        public void Dispose()
        {
            Window?.Close();
            Engine.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeData : IExtensionData
    {
        public string Text { get; set; } = string.Empty;

        public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync() =>
            Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionSheet>>([new ExtensionSheet("f_1", "Une fiche", "Histoire", [], false)]));

        public Task<Outcome<ExtensionSheetText>> SheetAsync(string id) =>
            Task.FromResult(Outcome.Of(new ExtensionSheetText(id, "Une fiche", Text)));

        public Task<Outcome<string?>> StoredAsync(string extension) => Task.FromResult(Outcome.Of<string?>(null));

        public Task<Outcome<Done>> StoreAsync(string extension, string json) => Task.FromResult(Outcome.Done());
    }
}
