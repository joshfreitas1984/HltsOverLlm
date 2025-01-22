using System.Drawing;
using Translate;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;

namespace Translate.Tests;

public class TranslationServiceTests
{
    const string workingDirectory = "../../../../Files";

    [Fact]
    public void Export()
    {
        TranslationService.Export(workingDirectory);
    }

    [Fact]
    public async Task UndoInvalidTranslations()
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
    public async Task PullFailedTranslations()
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
        
        File.WriteAllLines($"{workingDirectory}/TestResults/failures.txt", failures);
    }

    [Fact]
    public async Task TranslateLines()
    {
        await TranslationService.TranslateViaLlmAsync(workingDirectory, false);
    }

    [Fact]
    public async Task TestPrompt()
    {       
        var config = Configuration.GetConfiguration(workingDirectory);

        // Create an HttpClient instance
        using var client = new HttpClient();

        var ignoreValidityCheck = false;
        var outputResponse = false;
        var batchSize = config.BatchSize ?? 10;

        var testLines = new List<TranslatedRaw> {
            new ("初入江湖"),
            new ("经验值80000点，江湖声望，玄铁"),
            new("{0} 加入队伍"),
            new("{0} 离开队伍"),
            new("{0}{1} 经验"),
            new ("唔…也许<color=#FF0000>李叹兄弟</color>识货无数，对于藏宝诗词肯定也是懂的。或许可以找个适当借口，向李叹兄弟问问这事。"),
            new("资质+{0}"),
            new("心念不起，自性不动。<br>着相即乱，离相不乱。"),
            new("<color=#FF0000>炼狱</color>"),
            new("颜玉书在场上时，所有队友的攻击力提升50%，减伤10%"),
            new("剧情"),
            new("{name_1}兄，你没事吧？"),
            new("{name_2}兄，你没事吧？"),
            new("蟒蛇"),
            new("蟒蛇"),
            new("孩子，若是你<color=#FF0000>搜索天书的过程里有了些进展，便回来这儿看看</color>，说不准我们也会有什么重大的突破。"),
            new("<color=#FFCC22>我手上有一封信，是洪义交给我的。</color>"),
            new("难道会是梨花姑娘挣扎之时，从<color=#FF0000>凶手</color>身上扯将下来的<color=#FF0000>证据</color>吗？"),
            new("（收殓第<color=#FF0000>四</color>具骸骨。）"),
            new("佛教七宝就是，佛教僧人修行所用的七项宝物，有「<color=#FF0000>金</color>、<color=#FF0000>银</color>、<color=#FF0000>珍珠</color>、<color=#FF0000>珊瑚</color>、<color=#FF0000>蜜蜡</color>、<color=#FF0000>砗磲</color>、<color=#FF0000>红玉髓</color>」等七种，各自都有不同的修行作用与宗教意义。"),
            new("这是第<color=#FF0000>六</color>次的份量。大哥哥，记得，只剩最后一次的份量了，千万记得在一天之内将足量的药草带过来，否则就会前功尽弃，一切得要<color=#FF0000>重新来过</color>了呀！"),
            new("在孔金舍命相救下，总算是惊险逃出了圣堂，但如今势单力孤，敌暗我明，只能按照孔金之前的计划，前往拱石村寻找同为河洛一族的南闲。然而南闲似乎因为拖欠了酒钱，被押到村长那里却又早已自行逃脱。在协助解决了蛇窟的问题后，梅村长派出的乡勇邀请你一同搜索徐暇客的下落。<br>跟着乡勇前往集合处，却想不到乡勇之中竟混入了豹王寨的流寇，根据他们供出的情报，梅小青被他们绑到了后山山洞，村中乡勇不知还藏着多少内应，为免打草惊蛇，此刻当先前往后山山洞营救梅小青。"),
        };

        var results = new List<string>();
        var totalLines = testLines.Count;
        var stopWatch = Stopwatch.StartNew();

        for (int i = 0; i < totalLines; i += batchSize)
        {
            stopWatch.Restart();

            int batchRange = Math.Min(batchSize, totalLines - i);

            // Use a slice of the list directly
            var batch = testLines.GetRange(i, batchRange);

            int recordsProcessed = 0;

            // Process the batch in parallel
            await Task.WhenAll(batch.Select(async line =>
            {
                line.Trans = await TranslationService.TranslateSplitAsync(config, line.Raw, client, ignoreValidityCheck, outputResponse);
                recordsProcessed++;
            }));

            var elapsed = stopWatch.ElapsedMilliseconds;
            var speed = recordsProcessed == 0 ? 0 : elapsed / recordsProcessed;
            Console.WriteLine($"Line: {i + batchRange} of {totalLines} ({elapsed} ms ~ {speed}/line)");  
        }

        foreach (var line in testLines)
            results.Add($"From: {line.Raw}\nTo: {line.Trans}\n");

        File.WriteAllLines($"{workingDirectory}/TestResults/tests.txt", results);
    }

    //[Fact]
    //public void CheckTransalationSuccessfulTest()
    //{
    //    string outputPath = "../../../../Files/";
    //    var testLines = new List<(string, string)> {
    //        ("振翅千里", "Soar thousands of miles (or leagues)")
    //    };

    //    var lines = new List<string>();

    //    foreach (var line in testLines)
    //    {
    //        var valid = Translation.CheckTransalationSuccessful(line.Item1, line.Item2);
    //        lines.Add($"valid:  {valid}");
    //        lines.Add($"from:  {line.Item1}");
    //        lines.Add($"to:    {line.Item2}");
    //        lines.Add("");
    //    }

    //    File.WriteAllLines($"{outputPath}/TestResults/SuccessfulTests.txt", lines);
    //}

    //[Fact]
    //public void FindMarkUpTest()
    //{
    //    var strings = Translation.FindMarkup("唔…也许<color=#FF0000>李叹兄弟</color>识货无数，对于藏宝诗词肯定也是懂的。或许可以找个适当借口，向李叹兄弟问问这事。");

    //    foreach (var text in strings)
    //        Console.WriteLine(text);
    //}

    [Fact]
    public async Task CachePromptsTest()
    {
        var prompts = Configuration.CachePrompts(workingDirectory);

        var serializer = new SerializerBuilder()
                 .WithNamingConvention(CamelCaseNamingConvention.Instance)
                 .Build();

        await File.WriteAllTextAsync($"{workingDirectory}/TestResults/AllPrompts.yaml", serializer.Serialize(prompts));
    }
}
