using System.Drawing;
using Translate;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;

namespace Translate.Tests;

public class TranslationCleanupTests
{
    const string workingDirectory = "../../../../Files";

    [Fact]
    public async Task RemoveInvalidTranslationsFromTranslated()
    {
        var config = Configuration.GetConfiguration(workingDirectory);

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            int recordsModded = 0; 
            foreach (var line in fileLines)
            {
                foreach (var split in line.Splits)
                {
                    if (string.IsNullOrEmpty(split.Translated))
                        continue;

                    var result = TranslationService.CheckTransalationSuccessful(config, split.Text, split.Translated ?? string.Empty);
                    if (!result.Valid)
                    {
                        Console.WriteLine($"Invalid {textFileToTranslate.Path} Failures:{result.CorrectionPrompt}\n{split.Translated}");
                        split.Translated = string.Empty;
                        recordsModded++;
                    }
                }
            }

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            if (recordsModded > 0)
            {
                Console.WriteLine($"Writing {recordsModded} records to {outputFile}");
                await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
            }
        });
    }

    [Fact]
    public async Task FindAllFailingTranslations()
    {
        var failures = new List<string>();

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            foreach (var line in fileLines)
            {
                foreach (var split in line.Splits)
                {
                    if (string.IsNullOrEmpty(split.Translated))
                        failures.Add($"Invalid {textFileToTranslate.Path}:\n{split.Text}");
                }
            }

            await Task.CompletedTask;
        });
        
        File.WriteAllLines($"{workingDirectory}/TestResults/FailingTranslations.txt", failures);
    }

    [Fact]
    public void CheckTransalationSuccessfulTest()
    {
        string outputPath = "../../../../Files/";
        var config = Configuration.GetConfiguration(workingDirectory);
        var testLines = new List<(string, string)> {
            ("振翅千里", "Soar thousands of miles (or leagues)")
        };

        var lines = new List<string>();

        foreach (var line in testLines)
        {
            var valid = TranslationService.CheckTransalationSuccessful(config, line.Item1, line.Item2);
            lines.Add($"valid:  {valid}");
            lines.Add($"from:  {line.Item1}");
            lines.Add($"to:    {line.Item2}");
            lines.Add("");
        }

        File.WriteAllLines($"{outputPath}/TestResults/CheckTranslationSuccesful.txt", lines);
    }

    [Fact]
    public void FindMarkUpTest()
    {
        var strings = TranslationService.FindMarkup("唔…也许<color=#FF0000>李叹兄弟</color>识货无数，对于藏宝诗词肯定也是懂的。或许可以找个适当借口，向李叹兄弟问问这事。");

        foreach (var text in strings)
            Console.WriteLine(text);
    }
}
