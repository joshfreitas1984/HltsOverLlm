using System;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

    public static async Task FillTranslationCache(string workingDirectory, int charsToCache, Dictionary<string, string> cache, LlmConfig config)
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
            await FillTranslationCache(workingDirectory, charsToCache, translationCache, config);

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

                            split.FlaggedForRetranslation = false;
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

                Console.WriteLine($"Line: {i + batchRange} of {totalLines} File: {outputFile} Unprocessable: {incorrectLineCount} Processed: {totalRecordsProcessed}");

                if (bufferedRecords > 250)
                {
                    Console.WriteLine($"Writing Buffer....");
                    await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
                    bufferedRecords = 0;
                }
            }
            
            var elapsed = stopWatch.ElapsedMilliseconds;
            var speed = recordsProcessed == 0 ? 0 : elapsed / recordsProcessed;
            Console.WriteLine($"Done: {totalLines} ({elapsed} ms ~ {speed}/line)");
            await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
        }
    }

    public static async Task PackageFinalTranslation(string workingDirectory)
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

    public static async Task<(bool split, string result)> SplitIfNeeded(string testString, LlmConfig config, string raw, HttpClient client, string outputFile)
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

    public static async Task<(bool split, string result)> SplitBracketsIfNeeded(LlmConfig config, string raw, HttpClient client, string outputFile)
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

        var optimisationFolder = $"{config.WorkingDirectory}/TestResults/Optimisation";

        // We do segementation here since saves context window by splitting // "。" doesnt work like u think it would
        var testStrings = new string[] { ":", "：", "<br>" };
        foreach (var testString in testStrings)
        {
            var (split, result) = await SplitIfNeeded(testString, config, raw, client, outputFile);

            // Because its recursive we want to bail out on the first successful one
            if (split)
                return result;
        }

        //Brackets
        var (split2, result2) = await SplitBracketsIfNeeded(config, raw, client, outputFile);
        if (split2)
            return result2;

        // Prepare the raw by stripping out anything the LLM can't support
        var preparedRaw = LineValidation.PrepareRaw(raw);

        // Define the request payload
        List<object> messages = GenerateBaseMessages(config, preparedRaw);

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

                // Write for optimising correction prompts
                if (config.OptimizationMode && retryCount > 1)
                    File.WriteAllText($"{optimisationFolder}/{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid()}.json", requestData);

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
                        messages = GenerateBaseMessages(config, preparedRaw);
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

    public static List<object> GenerateBaseMessages(LlmConfig config, string raw)
    {
        //Dynamically build prompt using whats in the raws
        var basePrompt = new StringBuilder(config.Prompts["BaseSystemPrompt"]);
        
        if (raw.Contains('{'))
            basePrompt.AppendLine(config.Prompts["DynamicPlaceholderPrompt"]);

        //if (raw.Contains("<"))
        //    basePrompt.AppendLine(config.Prompts["DynamicMarkupPrompt"]);

        basePrompt.AppendLine(Glossary.ConstructGlossaryPrompt(raw, config));

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
}
