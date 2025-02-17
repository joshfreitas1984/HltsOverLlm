using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Translate.Tests;

public class TranslationCleanupTests
{
    const string workingDirectory = "../../../../Files";    

    [Fact]
    public void MatchRawLines()
    {
        string outputPath = $"{workingDirectory}/Converted";
        string exportPath = $"{workingDirectory}/Raw/Export";
        var serializer = Yaml.CreateSerializer();
        var deserializer = Yaml.CreateDeserializer();

        foreach (var textFileToTranslate in TranslationService.GetTextFilesToSplit())
        {
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";
            var exportFile = $"{exportPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                continue;

            var exportLines = deserializer.Deserialize<List<TranslationLine>>(File.ReadAllText(exportFile));
            var transLines = deserializer.Deserialize<List<TranslationLine>>(File.ReadAllText(outputFile));

            for (int i = 0; i < exportLines.Count; i++)
            {
                if (exportLines[i].LineNum == transLines[i].LineNum)
                    transLines[i].Raw = exportLines[i].Raw;
            }

            File.WriteAllText(outputFile, serializer.Serialize(transLines));
        }
    }

    [Fact]
    public async Task FindAllFailingTranslations()
    {
        var failures = new List<string>();
        var pattern = LineValidation.ChineseCharPattern;

        var forTheGlossary = new List<string>();

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            foreach (var line in fileLines)
            {
                foreach (var split in line.Splits)
                {
                    if (string.IsNullOrEmpty(split.Text))
                        continue;

                    // If it is already translated or just special characters return it
                    if (!Regex.IsMatch(split.Text, pattern))
                        continue;

                    if (!string.IsNullOrEmpty(split.Text) && string.IsNullOrEmpty(split.Translated))
                    {
                        failures.Add($"Invalid {textFileToTranslate.Path}:\n{split.Text}");

                        //if (split.Text.Length < 6)
                        if (!forTheGlossary.Contains(split.Text))
                            forTheGlossary.Add(LineValidation.PrepareRaw(split.Text));
                    }
                }
            }

            await Task.CompletedTask;
        });

        File.WriteAllLines($"{workingDirectory}/TestResults/FailingTranslations.txt", failures);
        File.WriteAllLines($"{workingDirectory}/TestResults/ForManualTrans.txt", forTheGlossary);

        await TranslateFailedLinesForManualTranslation();
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
            var valid = LineValidation.CheckTransalationSuccessful(config, line.Item1, line.Item2, string.Empty);
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

    [Fact]
    public static void TestPuttingColorsBackForSelected()
    {
        var input = "<color=#FFCC22>我手上有一封信，是洪义交给我的。</color>";
        var translated = "Inner Translation";
        var input2 = "<color=#FFCC22>我手上有一封信，是洪义交给我的。</color>我手上有一封信，是洪义交给我的<color=#FFCC22>Should Fail</color>";

        //If the string is 100% encased 
        var result1 = LineValidation.EncaseColorsForWholeLines(input, translated);
        var result2 = LineValidation.EncaseColorsForWholeLines(input2, translated);
        var result3 = LineValidation.EncaseColorsForWholeLines(input, result1);

        Assert.StartsWith("<color=#FFCC22>", result1);
        Assert.EndsWith("</color>", result1);
        Assert.True(result2 == translated);
        Assert.Equal(result3, result1);
    }

    [Fact]
    public async Task TranslateFailedLinesForManualTranslation()
    {
        await FindAllFailingTranslations();

        var config = Configuration.GetConfiguration(workingDirectory);
        string inputFile = $"{workingDirectory}/TestResults/ForManualTrans.txt";

        // Create an HttpClient instance
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(300);

        var batchSize = config.BatchSize ?? 10;
        var testLines = File.ReadLines(inputFile).ToList();

        var results = new List<string>();
        var totalLines = testLines.Count;
        var stopWatch = Stopwatch.StartNew();

        // Turn off validation
        config.SkipLineValidation = true;

        for (int i = 0; i < totalLines; i += batchSize)
        {
            stopWatch.Restart();

            int batchRange = Math.Min(batchSize, totalLines - i);
            var batch = testLines.GetRange(i, batchRange);
            int recordsProcessed = 0;

            // Process the batch in parallel
            await Task.WhenAll(batch.Select(async line =>
            {
                var result = await TranslationService.TranslateSplitAsync(config, line, client, string.Empty);
                results.Add($"{{ \"{line}\", \"{result}\" }}, ");
                recordsProcessed++;
            }));

            var elapsed = stopWatch.ElapsedMilliseconds;
            var speed = recordsProcessed == 0 ? 0 : elapsed / recordsProcessed;
            Console.WriteLine($"Line: {i + batchRange} of {totalLines} ({elapsed} ms ~ {speed}/line)");
        }

        File.WriteAllLines($"{workingDirectory}/TestResults/ForManualTransComplete.txt", results);
    }

    [Fact]
    public void TestBracketSplit()
    {
        var inputs = new List<string>
        {
            "(whatifIstart) This is a sample text with (brackets) and (some more) text. I like (brackets) to translate. (Ok) sdsaf",
            "This is a sample text with (brackets) and (some more) text. I like (brackets) to translate. (Ok) sdsaf",
            "This is a sample text with (brackets) and (some more) text. I like (brackets) to translate. (Ok)",
            "Text (brackets)"
        };

        foreach (var input in inputs)
        {
            string output = SplitBracket(input);
            Console.WriteLine(output);
            Assert.Equal(input, output);
        }
    }

    private static string SplitBracket(string input)
    {
        string output = string.Empty;
        string pattern = @"([^\(]*|(?:.*?))\(([^\)]*)\)|([^\(\)]*)$";

        MatchCollection matches = Regex.Matches(input, pattern);
        foreach (Match match in matches)
        {
            var outsideStart = match.Groups[1].Value.Trim();
            var outsideEnd = match.Groups[3].Value.Trim();
            var inside = match.Groups[2].Value.Trim();

            Console.WriteLine("OutsideStart: " + outsideStart);
            Console.WriteLine("Inside: " + inside);
            Console.WriteLine("outsideEnd: " + outsideEnd);

            if (!string.IsNullOrEmpty(outsideStart))
                output += outsideStart;

            if (!string.IsNullOrEmpty(inside))
                output += $" ({inside}) ";

            if (!string.IsNullOrEmpty(outsideEnd))
                output += outsideEnd;
        }

        return output.Trim();
    }    

    public static bool MatchesContextRetrans(string input)
    {
        string[] words = ["you", "me"]; //"I", "isn't"

        foreach (var word in words)
        {
            var pattern = $@"\b{word}\b";
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }
}
