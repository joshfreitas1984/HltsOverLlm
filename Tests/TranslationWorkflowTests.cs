using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Schema;

namespace Translate.Tests;
public class TranslationWorkflowTests
{
    const string workingDirectory = "../../../../Files";

    [Fact]
    public void ExportAssetsIntoTranslated()
    {
        TranslationService.ExportTextAssetsToCustomFormat(workingDirectory);
    }

    //[Fact]
    //public async Task ExtractGlossary()
    //{
    //    await TranslationService.ExtractGlossaryAsync(workingDirectory);
    //}

    [Fact]
    public async Task TranslateLinesBruteForce()
    {
        await PerformTranslateLines(true);
    }

    [Fact]
    public async Task TranslateLines()
    {
        await PerformTranslateLines(false);
        await PackageFinalTranslation();
    }

    private async Task PerformTranslateLines(bool keepCleaning)
    {
        if (keepCleaning)
        {
            int remaining = await UpdateCurrentTranslationLines();
            int lastRemaining = remaining;
            int iterations = 0;
            while  (remaining > 0 && iterations < 10)
            {
                await TranslationService.TranslateViaLlmAsync(workingDirectory, false);
                remaining = await UpdateCurrentTranslationLines();
                iterations++;

                // We've hit our brute force limit
                if (lastRemaining == remaining)
                    break;
            }
        }
        else
            await TranslationService.TranslateViaLlmAsync(workingDirectory, false);


        await PackageFinalTranslation();
    }

    [Fact]
    public async Task PackageFinalTranslation()
    {
        await TranslationService.PackageFinalTranslationAsync(workingDirectory);

        var sourceDirectory = $"{workingDirectory}/Mod";
        var gameDirectory = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\河洛群俠傳 (Ho Tu Lo Shu ： The Books of Dragon)\\Mod";
        if (Directory.Exists(gameDirectory))
            Directory.Delete(gameDirectory, true);

        TranslationService.CopyDirectory(sourceDirectory, gameDirectory);
    }

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
            {  "唔呃…", "Uh..." },
            {  "戾气？", "Malevolent Qi?" },
            {  "-请便", "Excuse me" },
            {  "{0}{1} 经验", "{0} {1} Experience" },
            { "杂项事件对话", "Miscellaneous Events Dialogue" }, 
            //{ "哼，这点轻功也敢来俏梦阁？", "Ha, you dare come to the Charming Dream Pavilion with such basic Qinggong skills?" },
            //{ "这就是禾家马帮大锅头？", "So, you're the big boss of the He Family Horse Gang?" },             
        };
    }

    [Fact]
    public async Task ApplyRulesToCurrentTranslation()
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
            //"先生",
            //"豹王寨喽啰",
            //"妹妹"
        };

        var mistranslationCheckGlossary = new Dictionary<string, string>();
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Names.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Factions.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Locations.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.SpecialTermsSafe.Entries);
        //Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.SpecialTermsUnsafe.Entries); //Not safe can be translated differently
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Titles.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Placeholder1WithTitles.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Placeholder2WithTitles.Entries);
        Configuration.AddToDictionaryGlossary(mistranslationCheckGlossary, config.GameData.Placeholder1and2WithTitles.Entries);

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

                    // Manual Retrans trigger
                    //if (line.LineNum > 0 && line.LineNum < 1000 && outputFile.Contains("NpcTalkItem.txt"))
                    //    split.FlaggedForRetranslation = true;

                    if (CheckSplit(newGlossaryStrings, manual, split, outputFile, hallucinationCheckGlossary, mistranslationCheckGlossary, dupeNames, config))
                        recordsModded++;
                }

            totalRecordsModded += recordsModded;
            var serializer = Yaml.CreateSerializer();
            if (recordsModded > 0 || resetFlag)
            {
                Console.WriteLine($"Writing {recordsModded} records to {outputFile}");
                File.WriteAllText(outputFile, serializer.Serialize(fileLines));
            }

            await Task.CompletedTask;
        });

        Console.WriteLine($"Total Lines: {totalRecordsModded} records");

        return totalRecordsModded;
    }

    //TODOs: Animal sounds
    public static bool CheckSplit(List<string> newGlossaryStrings, Dictionary<string, string> manual, TranslationSplit split, string outputFile,
        Dictionary<string, string> hallucinationCheckGlossary, Dictionary<string, string> mistranslationCheckGlossary, Dictionary<string, List<string>> dupeNames, LlmConfig config)
    {
        var pattern = LineValidation.ChineseCharPattern;
        bool modified = false;

        // Flags        
        bool cleanWithGlossary = true;

        //////// Quick Validation here

        // If it is already translated or just special characters return it
        if (!Regex.IsMatch(split.Text, pattern) && split.Translated != split.Text)
        {
            Console.WriteLine($"Already Translated {outputFile} \n{split.Translated}");
            split.Translated = split.Text;
            split.ResetFlags();
            return true;
        }

        //if (split.Text.Contains("Target") || split.Text.Contains("Location") || split.Text.Contains("Inventory"))
        //{
        //    Console.WriteLine($"New Glossary {outputFile} Replaces: \n{split.Translated}");
        //    split.FlaggedForRetranslation = true;
        //    return true;
        //}

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

        // Skip Empty
        if (string.IsNullOrEmpty(split.Translated))
            return false;

        // Context retrans too fricken big
        //if (outputFile.Contains("NpcTalkItem.txt") && MatchesContextRetrans(split.Translated))
        //{
        //    split.FlaggedForRetranslation = true;
        //    modified = true;
        //}

        if (MatchesPinyin(split.Translated))
        {
            split.FlaggedForRetranslation = true;
            modified = true;
        }

        //////// Manipulate split from here
        if (cleanWithGlossary)
        {
            // Glossary Clean up - this won't check our manual jobs
            modified = CheckMistranslationGlossary(split, mistranslationCheckGlossary, modified);
            modified = CheckHallucinationGlossary(split, hallucinationCheckGlossary, dupeNames, modified);
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

        // Long NPC Names - this really should be 30
        if (outputFile.Contains("NpcItem.txt") && split.Translated.Length > 50)
        {
            split.FlaggedForRetranslation = true;
            modified = true;
        }

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
            split.FlaggedForRetranslation = true;
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
                // Handle placeholders being annoying basically if it caught a {name_2} only when the text has {1} and {2}
                if (split.Text.Contains("{name_1}{name_2}") && !item.Value.Contains("{name_1}"))
                    continue;


                //Console.WriteLine($"Mistranslated:{outputFile}\n{item.Value}\n{split.Translated}");
                split.FlaggedForRetranslation = true;
                split.FlaggedGlossaryIn += $"{item.Value},{item.Key},";
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
                    split.FlaggedGlossaryOut += $"{item.Value},{item.Key},";
                    modified = true;
                }
            }
        }

        return modified; // Will be previous value - even if it didnt find anything
    }

    public static bool MatchesPinyin(string input)
    {
        string[] words = ["hiu", "guniang", "tut", "thut", "oi", "avo", "porqe", "obrigado", 
            "nom", "esto", "tem", "mais", "com", "ver", "nos", "sobre", "vermos",
            "dar", "nam", "J'ai", "je", "veux", "pas", "ele", "una",  "keqi", "shiwu", 
            "niang", "fuck", "ich", "daren", "furen", "ein", "der", "ganzes", "Leben", "dort", "xiansheng"];

        foreach (var word in words)
        {
            var pattern = $@"\b{word}\b";
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

}
