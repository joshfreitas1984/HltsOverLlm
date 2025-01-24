using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

                    // If it is already translated or just special characters return it
                    if (!Regex.IsMatch(split.Text, @"\p{IsCJKUnifiedIdeographs}") && split.Translated != split.Text)
                    {
                        Console.WriteLine($"Already Translated {textFileToTranslate.Path} \n{split.Translated}");
                        split.Translated = split.Text;
                        recordsModded++;
                        continue;
                    }

                    // Clean up Diacritics
                    var cleanedUp = LineValidation.CleanupLine(split.Translated, split.Text);
                    if (cleanedUp != split.Translated)
                    {
                        Console.WriteLine($"Cleaned up {textFileToTranslate.Path} \n{split.Translated}\n{cleanedUp}");
                        split.Translated = cleanedUp;
                        recordsModded++;
                        continue;
                    }

                    var result = LineValidation.CheckTransalationSuccessful(config, split.Text, split.Translated ?? string.Empty);
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
    public async Task MatchRawLines()
    {
        string outputPath = $"{workingDirectory}/Translated";
        string exportPath = $"{workingDirectory}/Export";

        foreach (var textFileToTranslate in TranslationService.GetTextFilesToSplit())
        {
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";
            var exportFile = $"{exportPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                continue;

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var exportLines = deserializer.Deserialize<List<TranslationLine>>(File.ReadAllText(exportFile));
            var transLines = deserializer.Deserialize<List<TranslationLine>>(File.ReadAllText(outputFile));

            for (int i = 0; i < exportLines.Count; i++)
            {
                if (exportLines[i].LineNum == transLines[i].LineNum)
                    transLines[i].Raw = exportLines[i].Raw;
            }

            await File.WriteAllTextAsync(outputFile, serializer.Serialize(transLines));
        }
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
                    if (string.IsNullOrEmpty(split.Text))
                        continue;

                    // If it is already translated or just special characters return it
                    if (!Regex.IsMatch(split.Text, @"\p{IsCJKUnifiedIdeographs}"))
                        continue;

                    if (!string.IsNullOrEmpty(split.Text) && string.IsNullOrEmpty(split.Translated))
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
            var valid = LineValidation.CheckTransalationSuccessful(config, line.Item1, line.Item2);
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
        var strings = LineValidation.FindMarkup("唔…也许<color=#FF0000>李叹兄弟</color>识货无数，对于藏宝诗词肯定也是懂的。或许可以找个适当借口，向李叹兄弟问问这事。");

        foreach (var text in strings)
            Console.WriteLine(text);
    }

    [Fact]
    public static void ColorConversion()
    {
        string input = "佛教七宝就是，佛教僧人修行所用的七项宝物，有「<color=#FF0000>金</color>、<color=#FF0000>银</color>、<color=#FF0000>珍珠</color>、<color=#FF0000>珊瑚</color>、<color=#FF0000>蜜蜡</color>、<color=#FF0000>砗磲</color>、<color=#FF0000>红玉髓</color>」等七种，各自都有不同的修行作用与宗教意义。";

        // Convert <color> tags to <font> tags
        string fontTagOutput = LineValidation.ConvertColorTagsToPlaceholderTags(input);
        Console.WriteLine(fontTagOutput);  // Output the result

        // Convert <font> tags back to <color> tags
        string colorTagOutput = LineValidation.ConvertPlaceholderTagsToColorTags(fontTagOutput);
        Console.WriteLine(colorTagOutput);  // Output the result
    }
}
