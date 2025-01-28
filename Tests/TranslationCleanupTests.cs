using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Translate.Tests;

public class TranslationCleanupTests
{
    const string workingDirectory = "../../../../Files";

    public static Dictionary<string, string> GetManualCorrections()
    {
        return new Dictionary<string, string>()
        {
            // Manual
            {  "接仙篇", "Chapter of Receiving Immortality" },
            {  "桔糖", "Tangerine Candy" },
            {  "奖励：", "Reward:" },
            {  "进度：", "Progress:" },
            {  "刚勇", "Brave and Bold" },
            {  "迁识", "Transfer of Consciousness" },
            {  "气血", "Lifeforce" },
            {  "狂狷", "Wild and Good" },
            {  "阴阳", "Yin and Yang" },
            {  "姑娘", "Young lady" },
            {  "唔！", "Ugh!" },
            {  "唔呃…", "Not cheating..." },
            {  "戾气？", "Malevolent Qi?" },
            {  "-请便", "Excuse me" },
            {  "{0}{1} 经验", "{0} {1} Experience" },
            { "杂项事件对话", "Miscellaneous Events Dialogue" }, 
            { "哼，这点轻功也敢来俏梦阁？", "Ha, you dare come to the Charming Dream Pavilion with such basic Qinggong skills?" },
            { "这就是禾家马帮大锅头？", "So, you're the big boss of the He Family Horse Gang?" },             
        };
    }    

    [Fact]
    public async Task UpdateCurrentTranslatedLines()
    {
        await UpdateCurrentTranslationLines();
    }
    
    public static async Task<int> UpdateCurrentTranslationLines()
    {
        var config = Configuration.GetConfiguration(workingDirectory);
        var totalRecordsModded = 0;        
        var manual = GetManualCorrections();
        bool resetFlag = true;

        //Use this when we've changed a glossary value that doesnt check hallucination
        var newGlossaryStrings = new List<string>
        {
            //"梅星河",
            //"梅村长",
            //"梅红绮"
        };

        var mistranslationCheckGlossary = new Dictionary<string, string>();
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Names.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Factions.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Locations.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.SpecialTermsSafe.Entries);
        //Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.SpecialTermsUnsafe.Entries); //Not safe can be translated differently
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Titles.Entries);

        var hallucinationCheckGlossary = new Dictionary<string, string>();
        Configuration.AddToDictionaryGlossary(hallucinationCheckGlossary, config.GameData.Names.Entries);
        Configuration.AddToDictionaryGlossary(hallucinationCheckGlossary, config.GameData.Factions.Entries);
        Configuration.AddToDictionaryGlossary(hallucinationCheckGlossary, config.GameData.Locations.Entries);
        Configuration.AddToDictionaryGlossary(hallucinationCheckGlossary, config.GameData.SpecialTermsSafe.Entries);
        //Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.SpecialTermsUnsafe.Entries); //Not safe can be translated differently
        //Configuration.AddToDictionaryGlossary(hallucinationCheckGlossary, config.GameData.Titles.Entries); //Not safe because of multiple ways to get titles

        //var dupeNames = new Dictionary<string, (string key1, string key2)>();
        var dupeNames = mistranslationCheckGlossary
            .GroupBy(pair => pair.Value)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.Key).ToList()
            );

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            int recordsModded = 0;
            
            foreach (var line in fileLines)
                foreach (var split in line.Splits)
                {
                    // Reset all the retrans flags
                    if (resetFlag)
                        split.ResetFlags();

                    if (CheckSplit(newGlossaryStrings, manual, split, outputFile, hallucinationCheckGlossary, mistranslationCheckGlossary, dupeNames, config))
                        recordsModded++;
                }

            totalRecordsModded += recordsModded;
            var serializer = Yaml.CreateSerializer();
            if (recordsModded > 0 || resetFlag)
            {
                Console.WriteLine($"Writing {recordsModded} records to {outputFile}");
                await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
            }
        });

        Console.WriteLine($"Total Lines: {totalRecordsModded} records");

        return totalRecordsModded;
    }

    //TODOs: Animal sounds
    //TODO: gongzi / gongzu
    //TODO: Duan Meng?
    //TODO: Lan yu
    //TODO: Fix {name} brother
    public static bool CheckSplit(List<string> newGlossaryStrings, Dictionary<string, string> manual, TranslationSplit split, string outputFile,
        Dictionary<string, string> hallucinationCheckGlossary, Dictionary<string, string> mistranslationCheckGlossary,  Dictionary<string, List<string>> dupeNames, LlmConfig config)
    {
        var pattern = LineValidation.ChineseCharPattern;
        bool modified = false;

        // Flags        
        bool cleanWithGlossary = true;
        bool checkCommonMistakes = false;

        //////// Quick Validation here
    
        // If it is already translated or just special characters return it
        if (!Regex.IsMatch(split.Text, pattern) && split.Translated != split.Text)
        {
            Console.WriteLine($"Already Translated {outputFile} \n{split.Translated}");
            split.Translated = split.Text;
            split.ResetFlags();
            return true;
        }

        foreach (var glossary in newGlossaryStrings)
        {
            if (split.Text.Contains(glossary))
            {
                Console.WriteLine($"New Glossary {outputFile} Replaces: \n{split.Translated}");
                split.FlaggedForRetranslation = true;
                return true;
            }
        }

        // Add Manual Translations in that are missing
        var preparedRaw = LineValidation.PrepareRaw(split.Text);

        // If it is manually corrected
        if (manual.TryGetValue(preparedRaw, out string? value))
        {
            if (split.Translated != value)
            {
                Console.WriteLine($"Manually Translated {outputFile} \n{split.Text}\n{split.Translated}");
                split.Translated = LineValidation.CleanupLineBeforeSaving(LineValidation.PrepareResult(value), split.Text, outputFile);
                split.ResetFlags();
                return true;
            }

            return false;
        }

        // Clean up Translations that are already in
        if (string.IsNullOrEmpty(split.Translated))
            return false;

        //////// Manipulate split from here
        if (cleanWithGlossary)
        {
            // Glossary Clean up - this won't check our manual jobs
            modified = CheckMistranslationGlossary(split, mistranslationCheckGlossary, modified);
            modified = CheckHallucinationGlossary(split, hallucinationCheckGlossary, dupeNames, modified);
        }

        if (checkCommonMistakes)
        {
            var (found, word) = LineValidation.ContainsCommonMistakes(split.Translated, split.Text);
            if (found)
            {
                Console.WriteLine($"Common Mistake:{outputFile}\n{word}\n{split.Translated}");
                split.Translated = split.Translated.Trim();
                modified = true;
            }
        }

        //// Try and flag crazy shit
        //if (!split.FlaggedForRetranslation
        //    //&& ContainsGender(split.Translated))
        //    && ContainsAnimalSounds(split.Translated))
        //{
        //    Console.WriteLine($"Contains whack {outputFile} \n{split.Translated}");
        //    recordsModded++;
        //    split.FlaggedForRetranslation = true;
        //}        

        // Trim line
        if (split.Translated.Trim().Length != split.Translated.Length)
        {
            Console.WriteLine($"Needed Trimming:{outputFile} \n{split.Translated}");
            split.Translated = split.Translated.Trim();
            modified = true;
            //Don't continue we still want other stuff to happen
        }

        // Add . into Dialogue
        if (outputFile.EndsWith("NpcTalkItem.txt") && char.IsLetter(split.Translated[^1]) && split.Text != split.Translated)
        {
            Console.WriteLine($"Needed full stop:{outputFile} \n{split.Translated}");
            split.Translated += '.';
            modified = true;
        }

        // Clean up Diacritics
        var cleanedUp = LineValidation.CleanupLineBeforeSaving(split.Translated, split.Text, outputFile);
        if (cleanedUp != split.Translated)
        {
            Console.WriteLine($"Cleaned up {outputFile} \n{split.Translated}\n{cleanedUp}");
            split.Translated = cleanedUp;
            modified = true;
        }

        // Remove Invalid ones
        var result = LineValidation.CheckTransalationSuccessful(config, split.Text, split.Translated ?? string.Empty);
        if (!result.Valid)
        {
            Console.WriteLine($"Invalid {outputFile} Failures:{result.CorrectionPrompt}\n{split.Translated}");
            split.Translated = string.Empty;
            modified = true;
        }

        return modified;
    }

    private static bool CheckMistranslationGlossary(TranslationSplit split, Dictionary<string, string> glossary, bool modified)
    {
        if (split.Translated == null)
            return modified;

        foreach (var item in glossary)
        {
            if (split.Text.Contains(item.Key) && !split.Translated.Contains(item.Value, StringComparison.OrdinalIgnoreCase))
            {
                //Console.WriteLine($"Mistranslated:{outputFile}\n{item.Value}\n{split.Translated}");
                split.FlaggedForRetranslation = true;
                split.FlaggedGlossaryIn += item.Value + ",";
                modified = true;
            }
        }

        return modified; // Will be previous value - even if it didnt find anything
    }

    private static bool CheckHallucinationGlossary(TranslationSplit split, Dictionary<string, string> glossary, Dictionary<string, List<string>> dupeNames, bool modified)
    {
        if (split.Translated == null)
            return modified;

        foreach (var item in glossary)
        {
            var wordPattern = $"\\b{item.Value}\\b";

            if (!split.Text.Contains(item.Key) && split.Translated.Contains(item.Value, StringComparison.OrdinalIgnoreCase))
            {
                //If we dont word match - ie matched He Family in the family
                if (!Regex.IsMatch(split.Translated, wordPattern, RegexOptions.IgnoreCase))
                    continue;

                // Handle Quanpai (entire sect)
                if (item.Value == "Qingcheng Sect" && split.Text.Contains("青城全派"))
                    continue;

                // If one of the dupes are in the raw
                bool found = false;
                if (dupeNames.TryGetValue(item.Value, out List<string>? dupes))
                {
                    foreach (var dupe in dupes)
                    {
                        found = split.Text.Contains(dupe);
                        if (found)
                            break;
                    }
                }

                if (!found)
                {
                    //Console.WriteLine($"Hallucinated:{outputFile}\n{item.Value}\n{split.Translated}");
                    split.FlaggedForRetranslation = true;
                    split.FlaggedGlossaryOut += item.Value + ",";
                    modified = true;
                }
            }
        }

        return modified; // Will be previous value - even if it didnt find anything
    }

    [Fact]
    public async Task MatchRawLines()
    {
        string outputPath = $"{workingDirectory}/Translated";
        string exportPath = $"{workingDirectory}/Export";
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

            await File.WriteAllTextAsync(outputFile, serializer.Serialize(transLines));
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

        await TranslateForManualTranslation();
    }

    //[Fact]
    //public async Task IsItEnglishPrompt()
    //{
    //    var config = Configuration.GetConfiguration(workingDirectory);
    //    var serializer = Yaml.CreateSerializer();        

    //    // Create an HttpClient instance
    //    using var client = new HttpClient();
    //    client.Timeout = TimeSpan.FromSeconds(300);

    //    // Prime the Request

    //    var basePrompt = config.Prompts["QueryEnglish"];
    //    var lines = new List<string>();

    //    var parallelOptions = new ParallelOptions
    //    {
    //        MaxDegreeOfParallelism = config.BatchSize ?? 10
    //    };

    //    await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
    //    {
    //        foreach (var line in fileLines)
    //        {
    //            int recordsModded = 0;

    //            await Parallel.ForEachAsync(line.Splits, parallelOptions, async (split, cancellationToken) =>
    //            {
    //                var stopWatch = new Stopwatch();
    //                stopWatch.Start();

    //                if (string.IsNullOrEmpty(split.Text))
    //                    return;

    //                if (split.FlaggedForRetranslation)
    //                    return;

    //                var prompt = $"{basePrompt}\n{split.Translated}";

    //                List<object> messages =
    //                   [
    //                       LlmHelpers.GenerateUserPrompt(prompt)
    //                   ];

    //                // Generate based on what would have been created
    //                var requestData = LlmHelpers.GenerateLlmRequestData(config, messages);

    //                // Send correction & Get result
    //                HttpContent content = new StringContent(requestData, Encoding.UTF8, "application/json");
    //                HttpResponseMessage response = await client.PostAsync(config.Url, content, cancellationToken);
    //                response.EnsureSuccessStatusCode();
    //                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
    //                using var jsonDoc = JsonDocument.Parse(responseBody);
    //                var result = jsonDoc.RootElement
    //                    .GetProperty("message")!
    //                    .GetProperty("content")!
    //                    .GetString()
    //                    ?.Trim() ?? string.Empty;

    //                if (result.StartsWith("No"))
    //                {
    //                    var output = $"File: {outputFile}\nLine: {line.LineNum}-{split.Split} Text: {split.Translated}";
    //                    Console.WriteLine(output);
    //                    Console.WriteLine(result);
    //                    lines.Add(output);
    //                }

    //                Console.WriteLine($"Elapsed: {stopWatch.Elapsed}");

    //                split.FlaggedForRetranslation = true;
    //                recordsModded++;
    //            });

    //            if (recordsModded > 0)
    //            {
    //                Console.WriteLine($"Writing {recordsModded} records to {outputFile}");
    //                await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
    //            }
    //        }

    //        await Task.CompletedTask;
    //        File.WriteAllLines($"{workingDirectory}/TestResults/NotEnglishLines.txt", lines);
    //    });



    //    File.WriteAllLines($"{workingDirectory}/TestResults/NotEnglishLines.txt", lines);
    //}

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
    public async Task TranslateForManualTranslation()
    {
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

        //Optimisation Folder
        var optimisationFolder = $"{workingDirectory}/TestResults/Optimisation";
        if (config.OptimizationMode && Directory.Exists(optimisationFolder))
            Directory.Delete(optimisationFolder, true);
        Directory.CreateDirectory(optimisationFolder);

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

    //private static bool ContainsGender(string input)
    //{
    //    // Deliberately only want 'he' and not 'He' (because common name)
    //    if (input.Contains(" he "))
    //    {
    //        if (input.Contains("brother", StringComparison.OrdinalIgnoreCase) || input.Contains("lord", StringComparison.OrdinalIgnoreCase))
    //            return false;

    //        return true;
    //    }

    //    if (input.StartsWith("she ", StringComparison.OrdinalIgnoreCase) |
    //        input.Contains(" she ", StringComparison.OrdinalIgnoreCase))
    //    {
    //        if (input.Contains("miss", StringComparison.OrdinalIgnoreCase) || input.Contains("lady", StringComparison.OrdinalIgnoreCase))
    //            return false;

    //        return true;
    //    }

    //    return false;
    //}

    //private static bool ContainsAnimalSounds(string? input)
    //{
    //    if (input == null)
    //        return false;

    //    // Deliberately only want 'he' and not 'He' (because common name)
    //    if (input.Contains("meow", StringComparison.OrdinalIgnoreCase)
    //        || input.Contains("hss", StringComparison.OrdinalIgnoreCase)
    //        || input.Contains("woof", StringComparison.OrdinalIgnoreCase)
    //        || input.Contains("moo", StringComparison.OrdinalIgnoreCase)
    //        || input.Contains("chirp", StringComparison.OrdinalIgnoreCase)
    //        || input.Contains("hiss", StringComparison.OrdinalIgnoreCase))
    //    {
    //        if (!input.Contains("moon", StringComparison.OrdinalIgnoreCase)
    //            && !input.Contains("mood", StringComparison.OrdinalIgnoreCase)
    //            && !input.Contains("smooth", StringComparison.OrdinalIgnoreCase))
    //            return true;
    //    }

    //    return false;
    //}
}
