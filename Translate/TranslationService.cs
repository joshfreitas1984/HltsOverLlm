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
            {  "-叽呼", "Chirp chirp" },
            {  "-请便", "Excuse me" },

            // Auto
            { "以简驭繁", "With simplicity, govern complexity" },
            { "枕戈待旦", "To sleep on one's armour and wait at dawn" },
            { "义薄云天", "Righteous as the Heavens" },
            { "信步山河", "Wandering through mountains and rivers" },
            { "似箭归心", "Like an arrow returning to its heart" },
            { "图穷匕现", "When all means fail, the dagger will appear" },
            { "安内攘外", "Stabilize the interior and repel external threats" },
            { "佛口蛇心", "Speak with a Buddha's mouth, think with a snake's heart" },
            { "层峦迭嶂", "Layer upon layer of hills and mountains" },
            { "师命难违", "A disciple must not disobey their master's order" },
            { "披麻带孝", "Wearing mourning clothes and performing the rites of filial mourning" },
            { "百密一疏", "One loophole in a hundred precautions" },
            { "鼠啃灵芝", "Mouse nibbles at lingzhi" },
            { "刀剑归真", "Swords and Blades Return to Truth" },
            { "刀枪不入", "Impermeable to blades and spears" },
            { "玉坊劈柴", "Jade Courtyard chopping wood" },
            { "迷魂水寨", "Enchanted Spirit Water Fortress" },
            { "兔死狗烹", "When the rabbit dies, the dog gets cooked" },
            { "猛龙下山", "Dragon Descends from the Mountain" },
            { "笑拈春华", "Smile as you pluck the spring flowers" },
            { "鸟尽弓藏", "All arrows are drawn, and the birds have flown away" },
            { "雁行千里", "Swan in formation, thousand miles" },
            { "满川桃李", "Plentiful as the peach and plum trees across the river" },
            { "踏月留香", "Trace Moonlight, Leave Fragrance" },
            { "飞剑归山", "Return the flying sword to the mountain" },
            { "大漠孤烟", "Lonely smoke in the vast desert" },
            { "云横秦岭", "Clouds spread across the Qinling Mountains" },
            { "左手画圆", "Draw a circle with the left hand" },
            { "花拳绣腿", "Flower Hands, Embroidered Legs" },
            { "水火不侵", "Water and fire do not harm each other" },
            { "桃谷升仙", "Ascending to immortality in Peach Valley" },
            { "豹死留皮", "The dead tiger leaves only its skin" },
            { "千锤百炼", "Tempered by a thousand hammers, forged in a hundred fires" },
            { "顺时敬天", "Follow the times and respect heaven" },
            { "以虚御实", "Use emptiness to meet fullness" },
            { "引蝶招蜂", "Attract butterflies, summon bees" },
            { "踏雪寻梅", "Traverse the snow to seek plum blossoms" },
            { "遭遇山贼", "Encountered Mountain Bandits" },
            { "生死有命", "Life and death are determined by fate" },
            { "定国安邦", "Stabilize the nation and maintain peace" },
            { "披荆斩棘", "Braving thorns and cutting through brambles" },
            { "梅开孤芳", "Stand out like a single plum blossom in bloom" },
            { "小枫鬼叫", "Little Maple Screams Like a Ghost" },
            { "祸起萧墙", "Disaster begins from within the family" },
            { "河朔立威", "Establish authority in the River North" },
            { "倚鞍思骏", "Lean on the saddle, think of the horse" },
            { "图穷匕见", "When the path ends, danger appears" },
            { "食蜜硕鼠", "Seek sweetness and find a fat mouse" },
            { "落花奈何", "What can be done about falling flowers?" },
            { "婆媳问题", "Mother-in-law and daughter-in-law issues" },
            { "仇山恨鬼", "Grudge against the mountain, hatred for ghosts" },
            { "回美馔楼", "Return to the Delicacies Pavilion" },
            { "也不想想", "They don't even want to think about it" },
            { "送交书画", "Submit paintings and calligraphy" },
            { "狐仙有赏", "There is a reward for the fox immortal" },
            { "重宝还京", "Return precious items to the capital" },

            //Manual
            { "{name_1}兄，近来听聚落兄弟们提及江湖风波，对{name_1}兄的言行举止似乎颇有微词。我希望这些只是谣言闲语，但还是在此提醒{name_1}兄，希望{name_1}兄不会误入歧途。", "Brother {name_1}, I've heard whispers about you from our fellow martial artists. They seem concerned with your recent actions. There seems to be some criticism about your conduct. I hope these are just rumors and idle talk, but I must remind you not to stray from the righteous path or be led astray by temptations." },
            { "嗯？怎么了？这几位也是要帮咱们找那书生的人吗？", "What? What's going on? Are these people also here to help us find the scholar?" },
            { "哼哼，他当然知道。只可惜对头太强。没种去救。", "Hmm, he certainly knows. It's just too bad that the adversary is too strong. He doesn't have the guts to save." },
            { "哼…他手上已有数片天书碎片，肯定是利用了天书的力量。", "Hum... He already has several pieces of the Heavenly Scriptures, so he must be using the power of the Heavenly Scriptures." },
            { "哼哼哼...不长...不长...有了河图和洛书，征服这神州大陆，不过是眨眼之间的事。", "Hmm... not long... not long... With the Hetu and Luoshu, conquering this Divine Land would be as easy as blinking an eye." },
            { "俏梦阁的艳雪姑娘希望明煦公子能找到一对终生不嫌不弃、白首不离的夫妇，并在这对夫妇前立誓终身不负。<br>将这讯息回复给明煦公子吧。", "Charming Dream Hall's beautiful Snow Maiden hopes that Young Master Ming Xu can find a couple who will not tire of each other for life and never part ways in old age, and she wants them to take an oath before her never to betray their lifelong commitment<br>Forward this information to Lord Ming Xu." },
            { "我和阿兄看了这里，觉得虽然穷，可大家都很和善，若是努力干活儿，也愿意给咱们一口饭吃，就打算留下来了。", "I and my elder brother looked around here, and we felt that although it's poor, everyone is very kind. If we work hard, they are willing to give us something to eat. So, we decided to stay" },
            { "…兄长说的是，{name_1}兄，思良无礼了。", "My elder brother said, Duan Silang was rude" },
            { "南诏王杨天宁与其弟杨天义得段将军之助，逐退狼蛮，顺利建国，却怎料在登基之后，那杨天宁竟突然变了个人，开始施行苛政，压榨国内少数民族，激起遍地民怨。", "Nanzhao King Yang Tianning and his brother Yang Tianyi, with the help of General Duan, repelled the wolf barbarians and successfully established a country. However, after their enthronement, Yang Tianning unexpectedly changed as a person and began to implement harsh policies, oppressing ethnic minorities within the nation, which incited widespread public discontent." },
            { "呃…你说你在热茶？但这里既没柴，也没火，怎么热茶？", "Uh... you said you were drinking hot tea? But there's neither firewood nor fire here. How can you drink hot tea?" },
            { "哼哼，这问我可就问对了！", "Ha ha, you got that right!" },
            { "我还想问你是谁，为什么躺在墓地里的棺材里装死？", "I also want to ask you who you are and why you are lying in a coffin in the cemetery, pretending to be dead?" },
            { "没错…噶字意为「口」，而举字意为「传」，合起来正是口传之意。", "That's right... The character 噶 means mouth, and 举 means transmit. Together, they signify the meaning of oral transmission" },
            { "摸出来的？还是别让段兄知道比较好。", "Found it by feeling around? It might be better not to let Brother Duan know." },
            { "哼哼，你不用再装了，王大柱已经全部招认了。", "Hmph, you don't need to pretend anymore, Wang Dazhu has already confessed everything." },
            { "哼，还道是谁，原来是青城派的三流弟子众。", "Hmph, who did you think it was? It turns out they're just the lower-tier disciples of the Qingcheng Sect." },
            { "哎呀，奴家可没存甚么坏心眼呢，{name_1}公子，将这乖孩子让给了奴家吧！奴家绝对会好好报答你的。呵呵。", "Oh dear, I didn't have any ill intentions at all, Master {name_1}. Please let this obedient child go with me! I will surely reward you for it. Haha" },
            { "嗯...这不重要...", "Uh, this is not important..." },
            { "第一道题：喇嘛二字各为何意？", "First question：喇嘛 (Lama) is composed of two characters, what is the meaning of each character?" },
            { "第三道题：哪个不是藏传佛教的佛教七宝？", "Third question：Which one is not one of the Seven Jewels of Buddhism in Tibetan Buddhism?" },
            { "淬炼费用：", "Tempering cost:" },
            { "锻造费用：", "Forging cost:" },
            { "说出你的渴望：", "Express your desires:" },
            { "限定装备：", "Mysterious Artifact:" },
            { "呃？这里有张信，信上以娟秀的笔迹写着：", "Huh? There's a letter here, written in elegant script by Yi Juan" },
            { "需求数量：", "Quanity Required:" },
            { "所持金额：", "Owned:" },
            { "将软筋散解药交给周益", "Give the 'Ruan Jin San Jie' medicine to Zhou Yi." },
            { "目前选择的游戏难易度为炼狱等级，部分游戏内容会有些不同：", "The current selected game difficulty is 'Purgatory' and some parts of the game content will be different." },
            { "别别别！我见识短浅所以不知道珍馐会的大名，还希望大哥您高抬贵手。", "No no no! Due to my limited experience, I'm unaware of the renowned Jianxu Hall, and I hope you, elder brother, would graciously overlook it." },
            { "呃，你这是在干什么?", "Huh, what are you doing?" },
            { "咦？怎会有人把东西留在这种地方？里面有块图片背后写着：", "Huh? How did someone leave something in such a place? There's a picture inside with writing on the back" },
            { "嗯...没留意到...怎么了？", "Hmm... I didn't notice that... What's wrong?" },
            { "不，这不是传说。", "No, this isn't a legend." },
            { "「为了赶快修缮房屋；我熬夜甚至冒雨在岛上收集木材，但现在想想这真是太不智了…我好像生病了…。」", "「In order to quickly repair the house, I stayed up late and even collected wood on the island in the rain; but now looking back, it was really unwise... I think I'm getting sick...」" },
            { "苏清瑞的过去", "Suo Qingshui's past" },
            { "唔…不不不！你们别误会，我只是恰好有事要找他罢了！", "Um... no, no, no! Don't misunderstand, I just happened to need to see him!" },
            { "给厨师的书信", "A Letter to the Chef" },
            { "咦？这种地方竟然有骸骨，还留下笔记，上面写着：", "Huh? There are actually bones here, and even a note left behind; it says on the note:" },
            { "迦罗，府里就拜托你了，我随{name_1}兄走一趟，情势若是有变，随时捎信来报。", "Jia Luo, I'm entrusting you with the matters at the office. I'll go along with Brother {name_1} for a while. If the situation changes, send me a message anytime to report." },
            { "来段关于刑法的笑话吧。", "C'mon, share with me a tale of law and laughter from the courts of martial arts!" },
            { "数年后，大研镇便有了这样的传闻：", "Several years later, the rumors about Dayan Town spread like this:" },
            { "我是妹控，给我妹子", "I'm a big fan of younger women, and I will bring happiness to your sister!" },
            { "请给我所有珍馐会事件的开启旗标。", "Please give me all opening sigils for all events at Delicacies Society." },
            { "我想要赋闲书院的介绍信。", "I would like an introduction letter from the Leisurely Scholars Academy." },
            { "嗯...原来是这么个名字...你和那土豪在圣堂经历过的事儿，这酒鬼都已经告诉我了。", "Hmm... so that's your name... the drunkard has already told me about what you and that nobleman experienced in the sanctuary." },
            { "上次……谢谢你带我去大研镇，这是我第一次去这么大的城市。如果没有你，或许我一辈子都不会踏进这样的大城市。我以后想要去更远的地方，回去娘出生的地方，了解自己祖宗生活的地方…………", "The last time... thank you for taking me to Dayan Town, it was my first time visiting such a large city. Without you, perhaps I would never set foot in such a big city. In the future, I want to go even farther, to where my mother was born and learn about where my ancestors lived.…" },
            { "唔...你以为...我们会这么容易告诉你吗？发信号！", "Ugh... you think... we'd just tell you so easily? Send a signal!" },
            { "唔……看起来和我来的圣堂还挺像的……难道这里也是圣堂？不会还要我再找一次十四天书吧？", "Hmm... It looks quite similar to the Holy Sanctuary I came from...Could this place also be a Holy Sanctuary? I hope I don't have to search for the Fourteen-Day Book again..." },
            { "这……这不是槟榔嘛！骆兄到底是从哪里找到的！", "This... this is not betel nut! Where did Brother Lou find it from anyway!" },
            { "嗯？等等，这不是槟榔吗？怎么会在这种地方？", "Huh? Wait, isn't this a betel nut? How is it here in a place like this?" },
            { "还有南闲…嗯……村里的人会知道这个名号吗？他叫什么名字来着…？", "There is also the Southern Sage... hmm... Would the villagers know this title? What was his name again?" },
            { "六片...洛书碎片...南闲，你这情报来自何处？怎么前些日子都没和我提起？", "Six pieces... fragments of the Luoshu... Southern Leisure, where did you get this intelligence from? Why didn't you mention to me these past few days?" },
            { "呃…我不是淘石帮里的人。兄弟，你怎地看起来如此害怕，一个人躲在这里。", "Um... I'm not from Taoshi Sect. Brother, why do you look so scared and hiding here all by yourself?" },
            { "呃…我身上一时没带这么多钱，回头等我凑到五千文钱立刻回来买下这只锦缎观月瓶。", "Um... I don't have that much money on me right now. I'll come back and buy this embroidered moon flask for five thousand wen once I have the cash." },
        };
    }

    public static void ExportTextAssetsToCustomFormat(string workingDirectory)
    {
        string inputPath = $"{workingDirectory}/TextAsset";
        string outputPath = $"{workingDirectory}/Export";

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

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            File.WriteAllText($"{outputPath}\\{textFileToTranslate.Path}", serializer.Serialize(exportContent));
        }
    }

    public static async Task FillTranslationCache(string workingDirectory, int charsToCache, Dictionary<string, string> cache)
    {
        // Add Manual adjustments 
        foreach (var k in GetManualCorrections())
            cache.Add(k.Key, k.Value);

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            foreach (var line in fileLines)
            {
                foreach (var split in line.Splits)
                {
                    if (string.IsNullOrEmpty(split.Translated))
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

        // Translation Cache - for smaller translations that tend to hallucinate
        var translationCache = new Dictionary<string, string>();
        var charsToCache = 7;

        if (useTranslationCache)
            await FillTranslationCache(workingDirectory, charsToCache, translationCache);

        // Create output folder
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        var config = Configuration.GetConfiguration(workingDirectory);

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

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var fileLines = deserializer.Deserialize<List<TranslationLine>>(content);
            var serializer = new SerializerBuilder()
               .WithNamingConvention(CamelCaseNamingConvention.Instance)
               .Build();

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
        string outputPath = $"{workingDirectory}/Translated";

        foreach (var textFileToTranslate in GetTextFilesToSplit())
        {
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                continue;

            var content = File.ReadAllText(outputFile);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

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

        basePrompt.AppendLine(config.Prompts["BaseGlossaryPrompt"]);

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
