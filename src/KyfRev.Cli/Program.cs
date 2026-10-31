using System.Text;
using KyfRev.Cli;

// Les rapports sont en français : la console doit afficher les accents.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
return VaultCommands.Build(Console.Out).Parse(args).Invoke();
