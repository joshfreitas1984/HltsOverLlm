using System.Drawing;
using Translate;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace Tests
{
    public class Tests
    {
        //[Fact]
        //public void Export()
        //{
        //    ExportLines.Export();
        //}

        [Fact]
        public async Task UndoInvalidTranslations()
        {
            string outputPath = "../../../../Files/Translated";

            foreach (var textFileToTranslate in Translation.TextFilesToSplit)
            {
                var outputFile = $"{outputPath}/{textFileToTranslate.Path}";

                if (!File.Exists(outputFile))
                    continue;

                var content = File.ReadAllText(outputFile);

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var fileLines = deserializer.Deserialize<List<TranslationLine>>(content);

                foreach (var line in fileLines)
                {
                    foreach (var split in line.Splits)
                    {
                        if (!Translation.CheckTransalationSuccessful(split.Text, split.Translated ?? string.Empty))
                        {
                            Console.WriteLine($"Invalid {textFileToTranslate.Path}: {split.Translated}");
                            split.Translated = string.Empty;
                        }
                    }
                }

                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                Console.WriteLine($"Writing {outputFile}");
                await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
            }
        }

        [Fact]
        public async Task TranslateLines()
        {
            await Translation.TranslateViaLlmAsync(false);
        }

        [Fact]
        public async Task TestPrompt()
        {
            string inputPath = "../../../../Files/Export";
            string outputPath = "../../../../Files/";

            var config = Configuration.GetConfiguration($"{outputPath}/Config.yaml");

            // Create an HttpClient instance
            using HttpClient client = new HttpClient();

            var testLines = new List<string> {
                "初入江湖",
                "经验值80000点，江湖声望，玄铁",
                "{0} 加入队伍",
                "{0} 离开队伍",
                "{0}{1} 经验",
                "唔…也许<color=#FF0000>李叹兄弟</color>识货无数，对于藏宝诗词肯定也是懂的。或许可以找个适当借口，向李叹兄弟问问这事。",
                "资质+{0}",
                "心念不起，自性不动。<br>着相即乱，离相不乱。",
                "<color=#FF0000>炼狱</color>",
                "颜玉书在场上时，所有队友的攻击力提升50%，减伤10%",
                "剧情",
                "{name_1}兄，你没事吧？",
                "{name_2}兄，你没事吧？",
            };

            var lines = new List<string>();

            foreach (var line in testLines)
            {
                lines.Add($"from:  {line}");
                lines.Add($"to:    {await Translation.TranslateSplitAsync(config, line, client)}");
                lines.Add("");
            }

            File.WriteAllLines($"{outputPath}/tests.txt", lines);
        }

        [Fact]
        public void CheckTransalationSuccessfulTest()
        {
            string outputPath = "../../../../Files/";
            var testLines = new List<(string, string)> {
                ("振翅千里", "Soar thousands of miles (or leagues)")
            };

            var lines = new List<string>();

            foreach (var line in testLines)
            {
                var valid = Translation.CheckTransalationSuccessful(line.Item1, line.Item2);
                lines.Add($"valid:  {valid}");
                lines.Add($"from:  {line.Item1}");
                lines.Add($"to:    {line.Item2}");
                lines.Add("");
            }

            File.WriteAllLines($"{outputPath}/SuccessfulTests.txt", lines);
        }

        //[Fact]
        //public void FindMarkUpTest()
        //{
        //    var strings = Translation.FindMarkup("唔…也许<color=#FF0000>李叹兄弟</color>识货无数，对于藏宝诗词肯定也是懂的。或许可以找个适当借口，向李叹兄弟问问这事。");

        //    foreach (var text in strings)
        //        Console.WriteLine(text);
        //}
    }
}
