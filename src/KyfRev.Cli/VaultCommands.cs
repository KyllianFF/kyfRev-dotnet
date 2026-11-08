using System.CommandLine;
using System.Text;
using KyfRev.Conversion;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Analysis;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Cli;

/// <summary>Commandes de <c>kyfrev-coffre</c>.</summary>
internal static class VaultCommands
{
    /// <summary>Aucune erreur (des avertissements peuvent exister).</summary>
    public const int Success = 0;

    /// <summary>Au moins un fichier illisible, en double ou d'un format plus récent.</summary>
    public const int ErrorsFound = 1;

    /// <summary>Le dossier n'est pas un coffre kyfRev.</summary>
    public const int NotAVault = 2;

    /// <summary>La conversion a refusé ou échoué, ou le secret n'ouvre pas le coffre chiffré à vérifier : rien n'a été écrit.</summary>
    public const int Refused = 3;

    /// <summary>
    /// Les commandes, écrivant sur <paramref name="output"/>. Les mots de passe se lisent dans <paramref name="input"/> s'il est
    /// donné (les essais), sinon au clavier sans écho, ou ligne à ligne si l'entrée est redirigée. Avec <c>--json</c>, la
    /// question du mot de passe et le refus s'écrivent sur <paramref name="errors"/> (la sortie d'erreur par défaut) :
    /// <paramref name="output"/> ne porte que le rapport.
    /// </summary>
    public static RootCommand Build(TextWriter output, TextReader? input = null, TextWriter? errors = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        var vault = new Argument<DirectoryInfo>("coffre") { Description = $"Dossier du coffre (celui qui contient .suivi/coffre.json, ou {EncryptedVault.HeaderFile} s'il est chiffré)." };
        var json = new Option<bool>("--json") { Description = "Rapport au format JSON, pour un traitement automatique." };
        var verify = new Command("verifier", "Analyse le coffre, en clair ou chiffré, en lecture seule : rien n'y est écrit ni verrouillé. Un coffre chiffré demande son mot de passe ou sa clé de récupération.") { vault, json };
        verify.SetAction(parseResult =>
        {
            var root = parseResult.GetValue(vault)!.FullName;
            var asJson = parseResult.GetValue(json);
            if (!EncryptedVault.IsEncrypted(root))
            {
                return Report(VaultInspector.Inspect(root), asJson, output);
            }
            var said = asJson ? errors ?? Console.Error : output;
            return VerifyEncrypted(root, Secret(said, input, "Mot de passe ou clé de récupération : "), asJson, output, said);
        });

        var source = new Argument<DirectoryInfo>("source") { Description = "Le coffre à convertir ; il reste intact." };
        var destination = new Argument<DirectoryInfo>("destination") { Description = "Un nouveau dossier : il ne doit pas exister." };
        var pbkdf2 = new Option<bool>("--pbkdf2") { Description = "Dérive la clé par PBKDF2-HMAC-SHA256 plutôt que par Argon2id." };
        var encrypt = new Command("chiffrer", "Copie chiffrée du coffre dans un nouveau dossier, vérifiée fichier par fichier ; la source reste intacte.") { source, destination, pbkdf2 };
        encrypt.SetAction(parseResult =>
        {
            var password = Secret(output, input, "Mot de passe du coffre chiffré : ");
            if (password != Secret(output, input, "Le même, une seconde fois : "))
            {
                output.WriteLine("Les deux saisies diffèrent : rien n'a été écrit.");
                return Refused;
            }
            output.WriteLine("Chiffrement en cours…");
            var parameters = parseResult.GetValue(pbkdf2) ? PasswordKeys.NewPbkdf2() : PasswordKeys.Calibrated();
            var result = VaultConversion.Encrypt(parseResult.GetValue(source)!.FullName, parseResult.GetValue(destination)!.FullName, password, parameters);
            output.WriteLine(result.Message);
            if (result.RecoveryKey is { } recovery)
            {
                output.WriteLine();
                output.WriteLine("Clé de récupération : notez-la maintenant, elle ne sera plus jamais affichée. Avec elle, le coffre");
                output.WriteLine("s'ouvre même si le mot de passe est oublié ; sans le mot de passe ni elle, il est perdu.");
                output.WriteLine();
                output.WriteLine("    " + recovery);
            }
            return result.Succeeded ? Success : Refused;
        });
        var decrypt = new Command("dechiffrer", "Copie en clair d'un coffre chiffré dans un nouveau dossier, vérifiée fichier par fichier ; la source reste intacte.") { source, destination };
        decrypt.SetAction(parseResult =>
        {
            var secret = Secret(output, input, "Mot de passe ou clé de récupération : ");
            output.WriteLine("Déchiffrement en cours…");
            var result = VaultConversion.Decrypt(parseResult.GetValue(source)!.FullName, parseResult.GetValue(destination)!.FullName, secret);
            output.WriteLine(result.Message);
            return result.Succeeded ? Success : Refused;
        });

        return new RootCommand("Outil du coffre kyfRev.") { verify, encrypt, decrypt };
    }

    /// <summary>Écrit le rapport de l'analyse, en texte ou en JSON, et rend le code de sortie qui la résume.</summary>
    private static int Report(VaultInspection inspection, bool asJson, TextWriter output)
    {
        output.Write(asJson ? InspectionReport.ToJson(inspection) : InspectionReport.ToText(inspection));
        return !inspection.IsVault ? NotAVault : inspection.HasErrors ? ErrorsFound : Success;
    }

    /// <summary>
    /// Analyse le coffre chiffré <paramref name="root"/>, en lecture seule : sa clé s'ouvre comme pour le déchiffrer, par le mot
    /// de passe ou par la clé de récupération, et ses fichiers se lisent par son index, sans que rien ne soit écrit, ni sur le
    /// disque, ni ailleurs qu'en mémoire. La clé est effacée ensuite. Un secret qui n'ouvre pas le coffre est refusé comme au
    /// déchiffrement ; un index qui ne s'ouvre pas est une erreur.
    /// </summary>
    /// <param name="root">Le dossier du coffre chiffré.</param>
    /// <param name="secret">Son mot de passe, ou sa clé de récupération.</param>
    /// <param name="asJson">Vrai : le rapport en JSON.</param>
    /// <param name="output">Là où s'écrit le rapport.</param>
    /// <param name="said">Là où s'écrivent les refus : la sortie, ou la sortie d'erreur avec <c>--json</c>.</param>
    private static int VerifyEncrypted(string root, string secret, bool asJson, TextWriter output, TextWriter said)
    {
        try
        {
            EncryptedVault.ReadHeader(root);
        }
        catch (VaultCryptoException exception)
        {
            // Un en-tête abîmé, ou d'un format plus récent : ce n'est pas le secret qui est en cause.
            said.WriteLine($"{exception.Message} Le coffre ne peut pas être analysé ; rien n'y a été écrit.");
            return ErrorsFound;
        }
        if (VaultConversion.OpenDataKey(root, secret) is not { } key)
        {
            said.WriteLine(VaultConversion.WrongSecret);
            return Refused;
        }
        EncryptedStorage? storage = null;
        try
        {
            try
            {
                storage = EncryptedStorage.Open(root, key, DiskStorage.Instance);
            }
            catch (VaultCryptoException exception)
            {
                said.WriteLine($"{exception.Message} Le coffre ne peut pas être analysé ; rien n'y a été écrit.");
                return ErrorsFound;
            }
            return Report(VaultInspector.Inspect(root, storage), asJson, output);
        }
        finally
        {
            storage?.Dispose();
            PasswordKeys.Clear(key);
        }
    }

    /// <summary>Une saisie secrète : jamais affichée, jamais écrite ailleurs que dans la mémoire du processus.</summary>
    private static string Secret(TextWriter output, TextReader? input, string prompt)
    {
        output.Write(prompt);
        if (input is not null || Console.IsInputRedirected)
        {
            var line = (input ?? Console.In).ReadLine() ?? string.Empty;
            output.WriteLine();
            return line;
        }
        var typed = ReadHidden(() => Console.ReadKey(intercept: true));
        output.WriteLine();
        return typed;
    }

    /// <summary>Une saisie au clavier, sans écho, jusqu'à Entrée : l'effacement retire le dernier caractère, les touches de contrôle ne comptent pas.</summary>
    internal static string ReadHidden(Func<ConsoleKeyInfo> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var typed = new StringBuilder();
        for (var key = next(); key.Key != ConsoleKey.Enter; key = next())
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }
        return typed.ToString();
    }
}
