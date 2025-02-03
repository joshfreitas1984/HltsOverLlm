using System;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.RegularExpressions;
using System.Xml.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Translate;

public static class TranslationService
{
    public static TextFileToSplit[] GetTextFilesToSplit()
        => [
            new() { Path = "AchievementItem.txt", SplitIndexes = [1, 2, 12] },
            new() { Path = "AreaItem.txt", SplitIndexes = [1] },
            new() { Path = "BufferItem.txt", SplitIndexes = [1,2] },
            new() { Path = "CharacterPropertyItem.txt", SplitIndexes = [5] },
            new() { Path = "CreatePlayerQuestionItem.txt", SplitIndexes = [1] },
            new() { Path = "DefaultSkillItem.txt", SplitIndexes = [1] },
            new() { Path = "DefaultTalentItem.txt", SplitIndexes = [1] },
            new() { Path = "EquipInventoryItem.txt", SplitIndexes = [1,3] },
            new() { Path = "EventCubeItem.txt", SplitIndexes = [1] },
            new() { Path = "HelpItem.txt", SplitIndexes = [3,4] },
            new() { Path = "NicknameItem.txt", SplitIndexes = [1,2] },
            new() { Path = "NormalBufferItem.txt", SplitIndexes = [1] },
            new() { Path = "NormalInventoryItem.txt", SplitIndexes = [1,3] },
            new() { Path = "NpcItem.txt", SplitIndexes = [1] },
            //new() { Path = "NpcTalkItem.txt", SplitIndexes = [6] },
            //new() { Path = "QuestItem.txt", SplitIndexes = [1,3] },
            new() { Path = "ReforgeItem.txt", SplitIndexes = [3] },
            new() { Path = "SkillNodeItem.txt", SplitIndexes = [1,2] },
            new() { Path = "SkillTreeItem.txt", SplitIndexes = [1,3] },
            new() { Path = "StringTableItem.txt", SplitIndexes = [1] },
            new() { Path = "TeleporterItem.txt", SplitIndexes = [1] },
            new() { Path = "TalentItem.txt", SplitIndexes = [1,2] },

            new() { Path = "QuestItem.txt", SplitIndexes = [1,3] },
            new() { Path = "NpcTalkItem.txt", SplitIndexes = [6] },
        ];

    public static void ExportTextAssetsToCustomFormat(string workingDirectory)
    {
        string inputPath = $"{workingDirectory}/TextAsset";
        string outputPath = $"{workingDirectory}/Export";
        var serializer = Yaml.CreateSerializer();

        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        foreach (var textFileToTranslate in GetTextFilesToSplit())
        {
            var lines = File.ReadAllLines($"{inputPath}/{textFileToTranslate.Path}");

            var exportContent = new List<TranslationLine>();
            var lineNum = 0;

            foreach (var line in lines)
            {
                var translationLine = new TranslationLine(lineNum, line);

                if (!line.StartsWith('#'))
                {
                    var splits = line.Split('\t');
                    foreach (var index in textFileToTranslate.SplitIndexes!)
                        translationLine.Splits.Add(new TranslationSplit(index, splits[index]));
                }

                exportContent.Add(translationLine);
                lineNum++;
            }

            File.WriteAllText($"{outputPath}\\{textFileToTranslate.Path}", serializer.Serialize(exportContent));
        }
    }

    public static async Task FillTranslationCacheAsync(string workingDirectory, int charsToCache, Dictionary<string, string> cache, LlmConfig config)
    {
        // Add Manual adjustments 
        //foreach (var k in GetManualCorrections())
        //    cache.Add(k.Key, k.Value);

        Glossary.AddGlossaryToCache(config, cache);

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            foreach (var line in fileLines)
            {
                foreach (var split in line.Splits)
                {
                    if (string.IsNullOrEmpty(split.Translated) || split.FlaggedForRetranslation)
                        continue;

                    if (split.Text.Length <= charsToCache && !cache.ContainsKey(split.Text))
                        cache.Add(split.Text, split.Translated);
                }
            }

            await Task.CompletedTask;
        });
    }

    public static async Task TranslateViaLlmAsync(string workingDirectory, bool forceRetranslation, bool useTranslationCache = true)
    {
        string inputPath = $"{workingDirectory}/Export";
        string outputPath = $"{workingDirectory}/Translated";

        // Create output folder
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        var config = Configuration.GetConfiguration(workingDirectory);

        // Translation Cache - for smaller translations that tend to hallucinate
        var translationCache = new Dictionary<string, string>();
        var charsToCache = 10;
        if (useTranslationCache)
            await FillTranslationCacheAsync(workingDirectory, charsToCache, translationCache, config);

        // Create an HttpClient instance
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(300);

        if (config.ApiKeyRequired)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        int incorrectLineCount = 0;
        int totalRecordsProcessed = 0;

        foreach (var textFileToTranslate in GetTextFilesToSplit())
        {
            var inputFile = $"{inputPath}/{textFileToTranslate.Path}";
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                File.Copy(inputFile, outputFile);

            var content = File.ReadAllText(outputFile);

            Console.WriteLine($"Processing File: {outputFile}");

            var serializer = Yaml.CreateSerializer();
            var deserializer = Yaml.CreateDeserializer();
            var fileLines = deserializer.Deserialize<List<TranslationLine>>(content);

            var batchSize = config.BatchSize ?? 20;
            var totalLines = fileLines.Count;
            var stopWatch = Stopwatch.StartNew();
            int recordsProcessed = 0;
            int bufferedRecords = 0;

            int logProcessed = 0;
            int logProcessMin = 1000;

            for (int i = 0; i < totalLines; i += batchSize)
            {
                int batchRange = Math.Min(batchSize, totalLines - i);

                // Use a slice of the list directly
                var batch = fileLines.GetRange(i, batchRange);

                // Process the batch in parallel
                await Task.WhenAll(batch.Select(async line =>
                {
                    foreach (var split in line.Splits)
                    {
                        if (string.IsNullOrEmpty(split.Text))
                            continue;

                        var cacheHit = translationCache.ContainsKey(split.Text);

                        if (string.IsNullOrEmpty(split.Translated) || forceRetranslation || (config.TranslateFlagged && split.FlaggedForRetranslation))
                        {
                            if (useTranslationCache && cacheHit)
                                split.Translated = translationCache[split.Text];
                            else
                                split.Translated = await TranslateSplitAsync(config, split.Text, client, outputFile);

                            split.ResetFlags();
                            recordsProcessed++;
                            totalRecordsProcessed++;
                            bufferedRecords++;
                        }

                        if (string.IsNullOrEmpty(split.Translated))
                            incorrectLineCount++;
                        //Two translations could be doing this at the same time
                        else if (!cacheHit && useTranslationCache && split.Text.Length <= charsToCache)
                            translationCache.TryAdd(split.Text, split.Translated);
                    }
                }));

                logProcessed++;

                if (batchSize != 1 || (logProcessed % logProcessMin == 0))
                    Console.WriteLine($"Line: {i + batchRange} of {totalLines} File: {outputFile} Unprocessable: {incorrectLineCount} Processed: {totalRecordsProcessed}");

                if (bufferedRecords > 250)
                {
                    Console.WriteLine($"Writing Buffer....");
                    File.WriteAllText(outputFile, serializer.Serialize(fileLines));
                    bufferedRecords = 0;
                }
            }

            var elapsed = stopWatch.ElapsedMilliseconds;
            var speed = recordsProcessed == 0 ? 0 : elapsed / recordsProcessed;
            Console.WriteLine($"Done: {totalLines} ({elapsed} ms ~ {speed}/line)");
            File.WriteAllText(outputFile, serializer.Serialize(fileLines));
        }
    }

    public static async Task PackageFinalTranslationAsync(string workingDirectory)
    {
        string inputPath = $"{workingDirectory}/Translated";
        string outputPath = $"{workingDirectory}/Mod/EnglishLlmByLash/config/textfiles";
        string failedPath = $"{workingDirectory}/TestResults/Failed";

        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);

        if (Directory.Exists(failedPath))
            Directory.Delete(failedPath, true);

        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(failedPath);

        var passedCount = 0;
        var failedCount = 0;

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            var failedLines = new List<string>();
            var finalLines = new List<string>();

            foreach (var line in fileLines)
            {
                var splits = line.Raw.Split('\t');
                var failed = false;

                foreach (var split in line.Splits)
                {
                    if (!string.IsNullOrEmpty(split.Translated))
                        splits[split.Split] = split.Translated;
                    //If it was already blank its all good
                    else if (!string.IsNullOrEmpty(split.Text))
                        failed = true;
                }

                line.Translated = string.Join('\t', splits);

                if (!failed)
                    finalLines.Add(line.Translated);
                else
                {
                    finalLines.Add(line.Raw);
                    failedLines.Add(line.Raw);
                }
            }

            if (finalLines.Count > 0)
                File.WriteAllLines($"{outputPath}/{textFileToTranslate.Path}", finalLines);

            if (failedLines.Count > 0)
                File.WriteAllLines($"{failedPath}/{textFileToTranslate.Path}", failedLines);

            passedCount += finalLines.Count;
            failedCount += failedLines.Count;

            await Task.CompletedTask;
        });

        Console.WriteLine($"Passed: {passedCount}");
        Console.WriteLine($"Failed: {failedCount}");

        ModHelper.GenerateModConfig(workingDirectory);
    }

    public static async Task IterateThroughTranslatedFilesAsync(string workingDirectory, Func<string, TextFileToSplit, List<TranslationLine>, Task> performActionAsync)
    {
        var deserializer = Yaml.CreateDeserializer();
        string outputPath = $"{workingDirectory}/Translated";

        foreach (var textFileToTranslate in GetTextFilesToSplit())
        {
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                continue;

            var content = File.ReadAllText(outputFile);

            var fileLines = deserializer.Deserialize<List<TranslationLine>>(content);

            if (performActionAsync != null)
                await performActionAsync(outputFile, textFileToTranslate, fileLines);
        }
    }

    public static async Task<(bool split, string result)> SplitIfNeededAsync(string testString, LlmConfig config, string raw, HttpClient client, string outputFile)
    {
        if (raw.Contains(testString))
        {
            var splits = raw.Split(testString);
            var builder = new StringBuilder();

            foreach (var split in splits)
            {
                var trans = await TranslateSplitAsync(config, split, client, outputFile);

                // If one fails we have to kill the lot
                if (string.IsNullOrEmpty(trans))
                    return (true, string.Empty);

                builder.Append(trans);
                builder.Append(testString);
            }

            var result = builder.ToString();

            return (true, result[..^testString.Length]);
        }

        return (false, string.Empty);
    }

    public static async Task<(bool split, string result)> SplitBracketsIfNeededAsync(LlmConfig config, string raw, HttpClient client, string outputFile)
    {
        if (raw.Contains('('))
        {
            string output = string.Empty;
            string pattern = @"([^\(]*|(?:.*?))\(([^\)]*)\)|([^\(\)]*)$"; // Matches text outside and inside brackets

            MatchCollection matches = Regex.Matches(raw, pattern);
            foreach (Match match in matches)
            {
                var outsideStart = match.Groups[1].Value.Trim();
                var outsideEnd = match.Groups[3].Value.Trim();
                var inside = match.Groups[2].Value.Trim();

                if (!string.IsNullOrEmpty(outsideStart))
                {
                    var trans = await TranslateSplitAsync(config, outsideStart, client, outputFile);
                    output += trans;

                    // If one fails we have to kill the lot
                    if (string.IsNullOrEmpty(trans))
                        return (true, string.Empty);
                }

                if (!string.IsNullOrEmpty(inside))
                {
                    var trans = await TranslateSplitAsync(config, inside, client, outputFile);
                    output += $" ({trans}) ";

                    // If one fails we have to kill the lot
                    if (string.IsNullOrEmpty(trans))
                        return (true, string.Empty);
                }

                if (!string.IsNullOrEmpty(outsideEnd))
                {
                    var trans = await TranslateSplitAsync(config, outsideEnd, client, outputFile);
                    output += trans;

                    // If one fails we have to kill the lot
                    if (string.IsNullOrEmpty(trans))
                        return (true, string.Empty);
                }
            }

            return (true, output.Trim());
        }

        return (false, string.Empty);
    }

    public static async Task<string> TranslateSplitAsync(LlmConfig config, string? raw, HttpClient client, string outputFile)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var pattern = LineValidation.ChineseCharPattern;
        // If it is already translated or just special characters return it
        if (!Regex.IsMatch(raw, pattern))
            return raw;

        // We do segementation here since saves context window by splitting // "。" doesnt work like u think it would
        var testStrings = new string[] { ":", "：", "<br>" };
        foreach (var testString in testStrings)
        {
            var (split, result) = await SplitIfNeededAsync(testString, config, raw, client, outputFile);

            // Because its recursive we want to bail out on the first successful one
            if (split)
                return result;
        }

        //Brackets
        var (split2, result2) = await SplitBracketsIfNeededAsync(config, raw, client, outputFile);
        if (split2)
            return result2;

        // Prepare the raw by stripping out anything the LLM can't support
        var preparedRaw = LineValidation.PrepareRaw(raw);

        // Define the request payload
        List<object> messages = GenerateBaseMessages(config, preparedRaw, outputFile);

        try
        {
            var translationValid = false;
            var retryCount = 0;
            var preparedResult = string.Empty;

            while (!translationValid && retryCount < (config.RetryCount ?? 1))
            {
                // Create an HttpContent object
                var requestData = LlmHelpers.GenerateLlmRequestData(config, messages);
                HttpContent content = new StringContent(requestData, Encoding.UTF8, "application/json");

                // Make the POST request
                HttpResponseMessage response = await client.PostAsync(config.Url, content);

                // Ensure the response was successful
                response.EnsureSuccessStatusCode();

                // Read and display the response content
                string responseBody = await response.Content.ReadAsStringAsync();

                using var jsonDoc = JsonDocument.Parse(responseBody);
                var llmResult = jsonDoc.RootElement
                    .GetProperty("message")!
                    .GetProperty("content")!
                    .GetString()
                    ?.Trim() ?? string.Empty;

                preparedResult = LineValidation.PrepareResult(llmResult);

                if (!config.SkipLineValidation)
                {
                    var validationResult = LineValidation.CheckTransalationSuccessful(config, preparedRaw, preparedResult);
                    translationValid = validationResult.Valid;

                    // Append history of failures
                    if (!translationValid && config.CorrectionPromptsEnabled)
                    {
                        var correctionPrompt = LineValidation.CalulateCorrectionPrompt(config, validationResult, preparedRaw, llmResult);

                        // Regenerate base messages so we dont hit token limit by constantly appending retry history
                        messages = GenerateBaseMessages(config, preparedRaw, outputFile);
                        AddCorrectionMessages(messages, llmResult, correctionPrompt);
                    }
                }
                else
                    translationValid = true;

                retryCount++;
            }

            return translationValid ? LineValidation.CleanupLineBeforeSaving(preparedResult, raw, outputFile) : string.Empty;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request error: {e.Message}");
            return string.Empty;
        }
    }

    public static void AddCorrectionMessages(List<object> messages, string result, string correctionPrompt)
    {
        messages.Add(LlmHelpers.GenerateAssistantPrompt(result));
        messages.Add(LlmHelpers.GenerateUserPrompt(correctionPrompt));
    }

    public static List<object> GenerateBaseMessages(LlmConfig config, string raw, string outputFile, string additionalSystemPrompt = "")
    {
        //Dynamically build prompt using whats in the raws
        var basePrompt = new StringBuilder(config.Prompts["BaseSystemPrompt"]);

        if (raw.Contains('{'))
            basePrompt.AppendLine(config.Prompts["DynamicPlaceholderPrompt"]);

        //if (raw.Contains("<"))
        //    basePrompt.AppendLine(config.Prompts["DynamicMarkupPrompt"]);

        basePrompt.AppendLine(Glossary.ConstructGlossaryPrompt(raw, config));

        // File Specific prompt
        if (outputFile.Contains("NpcItem.txt"))
            basePrompt.AppendLine(config.Prompts["FileNpcItemPrompt"]);

        basePrompt.AppendLine(additionalSystemPrompt);

        return
        [
            LlmHelpers.GenerateSystemPrompt(basePrompt.ToString()),
            LlmHelpers.GenerateUserPrompt(raw)
        ];
    }

    public static void AddPromptWithValues(this StringBuilder builder, LlmConfig config, string promptName, params string[] values)
    {
        var prompt = string.Format(config.Prompts[promptName], values);
        builder.Append(' ');
        builder.Append(prompt);
    }

    public static void CopyDirectory(string sourceDir, string destDir)
    {
        // Get the subdirectories for the specified directory.
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDir}");

        // If the destination directory doesn't exist, create it.
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            var tempPath = Path.Combine(destDir, file.Name);
            file.CopyTo(tempPath, false);
        }

        // Copy each subdirectory using recursion
        DirectoryInfo[] dirs = dir.GetDirectories();
        foreach (DirectoryInfo subdir in dirs)
        {
            var tempPath = Path.Combine(destDir, subdir.Name);
            CopyDirectory(subdir.FullName, tempPath);
        }
    }

    public static async Task<string> ExtractGlossaryItemAsync(LlmConfig config, HttpClient client, string input)
    {
        // Prime the Request
        var basePrompt = config.Prompts["GlossaryBuildUpPrompt"];

        List<object> messages =
            [
                LlmHelpers.GenerateSystemPrompt(basePrompt),
                LlmHelpers.GenerateUserPrompt(input)
            ];

        //Generate Schema
        if (!config.ModelParams!.ContainsKey("format"))
        {
            JsonSerializerOptions options = JsonSerializerOptions.Default;
            JsonNode schema = options.GetJsonSchemaAsNode(typeof(StructuredGlossaryLine[]));
            config.ModelParams!.Add("format", schema);
        }

        // Generate based on what would have been created
        var requestData = LlmHelpers.GenerateLlmRequestData(config, messages);

        // Send correction & Get result
        HttpContent content = new StringContent(requestData, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync(config.Url, content);
        response.EnsureSuccessStatusCode();
        string responseBody = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(responseBody);
        var result = jsonDoc.RootElement
            .GetProperty("message")!
            .GetProperty("content")!
            .GetString()
            ?.Trim() ?? string.Empty;

        return result;
    }


    public class GlossaryCapture : Dictionary<string, StructuredGlossaryLine>
    {
        public bool AddItem(StructuredGlossaryLine item)
        {
            // Find by Original Text - else add a new one in
            if (!ContainsKey(item.OriginalText))
            {
                Add(item.OriginalText, item);
                return true;
            }

            return false;
        }
    }

    public class GlossaryOutput : Dictionary<string, GlossaryCapture>
    {
        public bool AddItem(StructuredGlossaryLine item)
        {
            var criteriaEntry = item.Criteria
                .Replace('\\', '_')
                .Replace('/', '_')
                .ToLower();

            if (!TryGetValue(criteriaEntry, out GlossaryCapture? newValue))
            {
                newValue = [];
                Add(criteriaEntry, newValue);
            }

            return newValue.AddItem(item);
        }
    }

    public static void WriteGlossaryOutput(string workingDirectory, GlossaryOutput glossary)
    {
        var serializer = Yaml.CreateSerializer();

        foreach (var entry in glossary)
        {
            var outputFile = $"{workingDirectory}/GlossaryExtract/{entry.Key}.txt";
            File.WriteAllText(outputFile, serializer.Serialize(entry.Value));
        }
    }

    public static async Task ExtractGlossaryAsync(string workingDirectory)
    {
        await Task.CompletedTask;
        throw new NotImplementedException($"It's too green at the moment - needs better prompts {workingDirectory}");

        //var config = Configuration.GetConfiguration(workingDirectory);

        //// Create an HttpClient instance
        //using var client = new HttpClient();
        //client.Timeout = TimeSpan.FromSeconds(300);

        //if (config.ApiKeyRequired)
        //    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        //int incorrectLineCount = 0;
        //int totalRecordsProcessed = 0;

        //var outputGlossary = new GlossaryOutput();

        //await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        //{
        //    int recordsModded = 0;
        //    int linesProcessed = 0;
        //    int totalLines = fileLines.Count;

        //    foreach (var line in fileLines)
        //    {
        //        foreach (var split in line.Splits)
        //        {
        //            if (!split.FlaggedForGlossaryExtraction)
        //                continue;

        //            var raw = LineValidation.PrepareRaw(split.Text);
        //            var result = await ExtractGlossaryItemAsync(config, client, raw);

        //            try
        //            {
        //                var foundEntries = JsonSerializer.Deserialize<StructuredGlossaryLine[]>(result);
        //                if (foundEntries != null && foundEntries.Length > 0)
        //                    foreach (var foundEntry in foundEntries)
        //                        if (outputGlossary.AddItem(foundEntry))
        //                            recordsModded++;

        //                split.FlaggedForGlossaryExtraction = false;
        //            }
        //            catch
        //            {
        //                incorrectLineCount++;
        //            }

        //            recordsModded++;
        //        }

        //        // Each Line
        //        linesProcessed++;
        //        if (linesProcessed % 5 == 0)
        //        {
        //            Console.WriteLine($"Line: {linesProcessed} of {totalLines} File: {outputFile} Unprocessable: {incorrectLineCount} Processed: {totalRecordsProcessed}");
        //            WriteGlossaryOutput(workingDirectory, outputGlossary);
        //        }
        //    }

        //    //Each File
        //    Console.WriteLine($"Line: {linesProcessed} of {totalLines} File: {outputFile} Unprocessable: {incorrectLineCount} Processed: {totalRecordsProcessed}");
        //    totalRecordsProcessed += recordsModded;
        //    await Task.CompletedTask;
        //});

        //WriteGlossaryOutput(workingDirectory, outputGlossary);
        //Console.WriteLine("Done");
    }
}
