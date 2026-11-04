using System.Reflection;
using System.Text.RegularExpressions;
using KyfRev.Application.UseCases;
using KyfRev.Modules.Citations;
using KyfRev.Modules.Schemas;

namespace KyfRev.Application.Tests.Architecture;

/// <summary>
/// Les 97 commandes de kyfRev 0.x (<c>docs/correspondance-commandes.md</c>) : chacune est reprise
/// par un cas d'usage (du cœur ou d'un module) marqué de son nom, ou attribuée au jalon qui l'apportera.
/// </summary>
[TestClass]
public sealed partial class CommandMapTests
{
    private const int ZeroXCommands = 97;

    [TestMethod]
    public void Every_command_of_version_0x_has_a_use_case_or_a_milestone()
    {
        var table = File.ReadAllLines(Path.Combine(ArchitectureTests.RepositoryRoot(), "docs", "correspondance-commandes.md"))
            .Select(line => Row().Match(line))
            .Where(match => match.Success)
            .Select(match => (Command: match.Groups["commande"].Value, UseCase: match.Groups["cas"].Value.Trim(), Milestone: match.Groups["jalon"].Value))
            .ToList();
        Assert.HasCount(ZeroXCommands, table);
        Assert.HasCount(ZeroXCommands, table.Select(row => row.Command).Distinct());

        var useCases = new[] { typeof(VaultHost).Assembly, typeof(CitationsModule).Assembly, typeof(SchemasModule).Assembly }
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<ZeroXCommandAttribute>()))
            .Where(pair => pair.Attribute is not null)
            .ToDictionary(pair => pair.Attribute!.Name, pair => $"`{pair.Method.DeclaringType!.Name}.{pair.Method.Name}`", StringComparer.Ordinal);

        foreach (var (command, useCase, milestone) in table)
        {
            if (useCase != "—")
            {
                Assert.AreEqual(useCase, useCases.GetValueOrDefault(command), command);
            }
            else
            {
                Assert.IsFalse(useCases.ContainsKey(command), $"{command} est déjà repris : le tableau doit le dire.");
            }
        }
        Assert.AreEqual(table.Count(row => row.UseCase != "—"), useCases.Count);
    }

    [GeneratedRegex(@"^\| `(?<commande>[a-z_]+)` \| (?<cas>[^|]+) \| (?<jalon>J[0-9.]+) \|")]
    private static partial Regex Row();
}
