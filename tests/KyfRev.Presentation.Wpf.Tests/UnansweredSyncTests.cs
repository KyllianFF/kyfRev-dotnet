using System.Windows;
using System.Windows.Input;
using KyfRev.Application;
using KyfRev.Presentation.Wpf.Editing;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le dernier texte avant de quitter l'écriture, ou de verrouiller le coffre : un champ qui n'a pas répondu ne passe pas pour
/// enregistré, car il tient peut-être des frappes plus récentes que ce que l'application en connaît.
/// </summary>
[TestClass]
public sealed class UnansweredSyncTests
{
    private const string Body = "# Mitose\n\nUn premier paragraphe.\n";

    /// <summary>
    /// Un champ comme le texte mis en forme : ce qui s'y tape n'arrive à l'application qu'à la demande, et seulement quand
    /// il répond.
    /// </summary>
    private sealed class SlowSurface : IEditorSurface
    {
        public event EventHandler? TextEdited;

        event EventHandler<EditorTools>? IEditorSurface.ToolsChanged
        {
            add { }
            remove { }
        }

        event EventHandler<LinkInput?>? IEditorSurface.LinkInputChanged
        {
            add { }
            remove { }
        }

        event EventHandler<LinkQuestion>? IEditorSurface.LinkAsked
        {
            add { }
            remove { }
        }

        event EventHandler<FormulaQuestion>? IEditorSurface.FormulaAsked
        {
            add { }
            remove { }
        }

        event EventHandler<DiagramQuestion>? IEditorSurface.DiagramAsked
        {
            add { }
            remove { }
        }

        event EventHandler? IEditorSurface.SaveAsked
        {
            add { }
            remove { }
        }

        event EventHandler<Key>? IEditorSurface.ListKeyPressed
        {
            add { }
            remove { }
        }

        /// <summary>Le champ répond aux demandes du texte.</summary>
        public bool Answers { get; set; } = true;

        /// <summary>Ce que le champ tient vraiment.</summary>
        public string Written { get; private set; } = string.Empty;

        /// <summary>Ce que l'application en connaît.</summary>
        public string Text { get; private set; } = string.Empty;

        Point IEditorSurface.InputPoint => default;

        /// <summary>Une frappe dans le champ : l'application ne la connaît qu'à la prochaine demande qui reçoit sa réponse.</summary>
        public void Type(string text)
        {
            Written += text;
            TextEdited?.Invoke(this, EventArgs.Empty);
        }

        public Task<bool> SyncAsync()
        {
            if (Answers)
            {
                Text = Written;
            }
            return Task.FromResult(Answers);
        }

        public void ReplaceAll(string text)
        {
            Written = text;
            Text = text;
        }

        void IEditorSurface.ToggleMark(InlineMark mark)
        {
        }

        void IEditorSurface.SetHeading(int level)
        {
        }

        void IEditorSurface.ToggleList(ListKind kind)
        {
        }

        void IEditorSurface.ToggleQuote()
        {
        }

        void IEditorSurface.InsertDiagram(string info, string code, int? position)
        {
        }

        void IEditorSurface.ShowDrawingCode(int position)
        {
        }

        void IEditorSurface.SetColor(string? color)
        {
        }

        void IEditorSurface.SetFontSize(string? size)
        {
        }

        void IEditorSurface.SetAlign(TextAlign align)
        {
        }

        void IEditorSurface.ChangeCase(CaseStyle? style)
        {
        }

        void IEditorSurface.InsertTable(int rows, int columns)
        {
        }

        void IEditorSurface.SetTableLook(string change, string? value)
        {
        }

        void IEditorSurface.InsertCode(string language)
        {
        }

        void IEditorSurface.InsertRule()
        {
        }

        void IEditorSurface.AskFormula()
        {
        }

        void IEditorSurface.InsertFormula(string tex, bool block, int? position)
        {
        }

        void IEditorSurface.AskLink()
        {
        }

        void IEditorSurface.InsertLink(string label, string address)
        {
        }

        void IEditorSurface.InsertImage(string link, string alternative, int? position)
        {
        }

        void IEditorSurface.InsertSheetLink(string label, string address)
        {
        }

        void IEditorSurface.SetProposalsOpen(bool open)
        {
        }

        void IEditorSurface.GoToHeading(string title, int occurrence)
        {
        }

        void IEditorSurface.Undo()
        {
        }

        void IEditorSurface.Redo()
        {
        }

        void IEditorSurface.Focus()
        {
        }
    }

    /// <summary>Laisse tourner le fil de l'interface jusqu'à la fin de la tâche, et rend sa réponse.</summary>
    private static bool Wait(Task<bool> task)
    {
        UiThread.Until(() => task.IsCompleted);
        return task.Result;
    }

    private static string OnDisk(ReadingVault vault) => vault.App.Sheets.Open(vault.SheetId).Value.Body;

    [STATestMethod]
    public void Writing_is_not_finished_while_the_field_keeps_its_last_keystrokes()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var before = OnDisk(vault);
        var editor = new SheetEditorViewModel(vault.App.Sheets.Open(vault.SheetId).Value, vault.App.Editing, vault.App.Sheets, FakeEnvironment.Markdown());
        var surface = new SlowSurface();
        editor.Attach(surface, () => null);

        surface.Answers = false;
        surface.Type("Suite.\n");

        Assert.IsFalse(Wait(editor.FinishAsync()), "Le champ n'a pas donné ses dernières frappes : rien ne dit qu'elles sont enregistrées.");
        Assert.AreEqual(before, OnDisk(vault));
        Assert.AreEqual(SaveState.Modified, editor.State);

        surface.Answers = true;

        Assert.IsTrue(Wait(editor.FinishAsync()));
        Assert.AreEqual(surface.Written, OnDisk(vault));
        Assert.AreEqual(SaveState.Saved, editor.State);
    }

    [STATestMethod]
    public void The_visual_text_says_whether_its_page_gave_the_last_keystrokes()
    {
        using var ui = new UiThread();
        using var visual = new VisualEditor(string.Empty, _ => Outcome.Refused<byte[]>("non"), new FakeSpelling(), FakeEnvironment.WebData);
        visual.ReplaceAll(Body);
        visual.Receive(new ReadyEvent());

        Assert.IsTrue(Wait(visual.SyncAsync()), "Rien de tapé : le texte connu est le dernier.");

        visual.Receive(new EditedEvent());

        Assert.IsFalse(Wait(visual.SyncAsync()), "Une frappe, et la page se tait : le texte connu est peut-être en retard.");
        Assert.AreEqual(Body, visual.Text);

        // La page répond à la demande suivante : la deuxième.
        var answered = visual.SyncAsync();
        visual.Receive(new TextEvent(2, Body + "Suite.\n"));

        Assert.IsTrue(Wait(answered));
        Assert.AreEqual(Body + "Suite.\n", visual.Text);

        // Sans répondre, la page envoie d'elle-même son texte après la dernière frappe : il vaut autant.
        visual.Receive(new EditedEvent());
        var sent = visual.SyncAsync();
        visual.Receive(new TextEvent(null, Body + "Suite et fin.\n"));

        Assert.IsTrue(Wait(sent));
        Assert.AreEqual(Body + "Suite et fin.\n", visual.Text);
    }

    [STATestMethod]
    public void An_insertion_is_not_in_the_text_until_the_page_gives_it_back()
    {
        using var ui = new UiThread();
        using var visual = new VisualEditor(string.Empty, _ => Outcome.Refused<byte[]>("non"), new FakeSpelling(), FakeEnvironment.WebData);
        visual.ReplaceAll(Body);

        // La page n'est pas prête : la formule attend, le texte connu ne l'a pas.
        visual.InsertFormula("E=mc^2", block: false, position: null);
        Assert.IsFalse(Wait(visual.SyncAsync()), "une insertion en attente n'est pas encore dans le texte");

        // Prête, elle reçoit la formule ; tant qu'elle n'a pas rendu son texte, il est en retard.
        visual.Receive(new ReadyEvent());
        Assert.IsFalse(Wait(visual.SyncAsync()));
        var answered = visual.SyncAsync();
        visual.Receive(new TextEvent(3, Body + "$E=mc^2$\n"));
        Assert.IsTrue(Wait(answered));

        // Une demande qui ne change rien au texte (son chargement) ne le rend pas en retard.
        visual.ReplaceAll(Body);
        Assert.IsTrue(Wait(visual.SyncAsync()));
    }
}
