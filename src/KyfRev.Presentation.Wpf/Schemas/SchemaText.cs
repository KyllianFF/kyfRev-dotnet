using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Schemas;

/// <summary>Textes de l'hôte des schémas réseau, lus dans les ressources (<c>Resources/SchemaText.resx</c>).</summary>
public static class SchemaText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.SchemaText", typeof(SchemaText).Assembly);

    /// <summary>« Appel refusé. ».</summary>
    public static string BadCall => Text(nameof(BadCall));

    /// <summary>« Le schéma réseau est illisible ou absent. ».</summary>
    public static string Unavailable => Text(nameof(Unavailable));

    /// <summary>« Schéma réseau : le module est désactivé… ».</summary>
    public static string ModuleOff => Text(nameof(ModuleOff));

    /// <summary>« Schéma réseau vide. ».</summary>
    public static string EmptyBlock => Text(nameof(EmptyBlock));

    /// <summary>« Double-cliquer pour le modifier ».</summary>
    public static string EditHint => Text(nameof(EditHint));

    /// <summary>« Ce schéma est encore rangé dans la fiche (format de la 0.x)… ».</summary>
    public static string InlineHint => Text(nameof(InlineHint));

    /// <summary>« Dessin du schéma réseau ».</summary>
    public static string PictureName => Text(nameof(PictureName));

    /// <summary>« Écrivez un nom, ou choisissez un schéma. ».</summary>
    public static string NothingChosen => Text(nameof(NothingChosen));

    /// <summary>« Choisir un schéma à importer ».</summary>
    public static string ChooseFile => Text(nameof(ChooseFile));

    /// <summary>« Le moteur web n'a pas pu démarrer : {0} ».</summary>
    public static string EngineFailedOf(object reason) => Format("EngineFailed", reason);

    /// <summary>« Schémas (*.json)|*.json ».</summary>
    public static string FileFilter => Text(nameof(FileFilter));

    /// <summary>« Schémas réseau ».</summary>
    public static string IslandName => Text(nameof(IslandName));

    private static string Text(string name) => Resources.GetString(name, CultureInfo.InvariantCulture)!;

    private static string Format(string name, params object[] arguments) => string.Format(CultureInfo.InvariantCulture, Text(name), arguments);
}
