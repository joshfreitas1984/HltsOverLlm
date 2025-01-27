using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
            { "【唉…仔细想想，我也确实没经历过黄裳那桩灭门惨事，如此强要他放下仇恨，他又怎么听得进去呢…】", "Oh... Upon careful thought, I truly haven't experienced the tragedy that befell Huang Shang's family. How can one insist so strongly for him to let go of his hatred and expect him to accept it?" },
            { "Hmm... So, she will betray Xiao Meng Ge and join forces with those bandits from Merciless Hall in this Shu region to stir up trouble. The reasons and intentions are quite obvious.", "" },
        };
    }

    [Fact]
    public async Task UpdateCurrentTranslatedLines()
    {
        var config = Configuration.GetConfiguration(workingDirectory);
        var pattern = LineValidation.ChineseCharPattern;
        var totalRecordsModded = 0;
        bool resetFlag = false;
        //resetFlag = true;

        var manual = GetManualCorrections();
        var newGlossaryStrings = new List<string>
        {
            //"梅星河",
            //"梅村长",
            //"梅红绮"
        };

        var safeGlossary = new Dictionary<string, string>();
        Configuration.AddToDictionaryGlossary(safeGlossary, config.GameData.Names.Entries);
        Configuration.AddToDictionaryGlossary(safeGlossary, config.GameData.Factions.Entries);
        Configuration.AddToDictionaryGlossary(safeGlossary, config.GameData.Locations.Entries);
        Configuration.AddToDictionaryGlossary(safeGlossary, config.GameData.SpecialTermsSafe.Entries);

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            int recordsModded = 0;

            foreach (var line in fileLines)
            {
                foreach (var split in line.Splits)
                {
                    // Add Manual Translations in that are missing
                    var preparedRaw = LineValidation.PrepareRaw(split.Text);

                    // If it is manually corrected
                    if (manual.TryGetValue(preparedRaw, out string? value))
                    {
                        if (split.Translated != value)
                        {
                            Console.WriteLine($"Manually Translated {textFileToTranslate.Path} \n{split.Text}\n{split.Translated}");
                            split.Translated = LineValidation.CleanupLineBeforeSaving(LineValidation.PrepareResult(value), split.Text, outputFile);
                            recordsModded++;
                        }
                        continue;
                    }

                    // Reset all the retrans flags
                    if (resetFlag)
                    {
                        recordsModded++;
                        split.ResetFlags();
                    }

                    //// Try and flag crazy shit
                    //if (!split.FlaggedForRetranslation
                    //    //&& ContainsGender(split.Translated))
                    //    && ContainsAnimalSounds(split.Translated))
                    //{
                    //    Console.WriteLine($"Contains whack {textFileToTranslate.Path} \n{split.Translated}");
                    //    recordsModded++;
                    //    split.FlaggedForRetranslation = true;
                    //}

                    // If it is already translated or just special characters return it
                    if (!Regex.IsMatch(split.Text, pattern) && split.Translated != split.Text)
                    {
                        Console.WriteLine($"Already Translated {textFileToTranslate.Path} \n{split.Translated}");
                        split.Translated = split.Text;
                        recordsModded++;
                        continue;
                    }

                    // Clean up Translations that are already in
                    if (string.IsNullOrEmpty(split.Translated))
                        continue;



                    // Glossary Clean up
                    //if (config.GameData.SafeGlossary.ContainsKey(split.Text))
                    foreach (var item in safeGlossary)
                    {
                        if (split.Text.Contains(item.Key) && !split.Translated.Contains(item.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"Mistranslated:{textFileToTranslate.Path}\n{item.Value}\n{split.Translated}");
                            split.FlaggedForRetranslation = true;
                            split.FlaggedGlossaryIn = item.Value;
                            recordsModded++;
                        }
                        //else if (!split.Text.Contains(item.Key) && split.Translated.Contains(item.Value, StringComparison.OrdinalIgnoreCase))
                        //{
                        //    Console.WriteLine($"Glossary in Non trans:{textFileToTranslate.Path} \n{split.Translated}");
                        //    split.FlaggedForRetranslation = true;
                        //    split.FlaggedGlossaryOut = item.Value;
                        //    recordsModded++;
                        //}
                    }


                    //Trim
                    if (split.Translated.Trim().Length != split.Translated.Length)
                    {
                        Console.WriteLine($"Needed Trimming:{textFileToTranslate.Path} \n{split.Translated}");
                        split.Translated = split.Translated.Trim();
                        recordsModded++;
                        //Don't continue we still want other stuff to happen
                    }

                    //Add . back in
                    if (outputFile.EndsWith("NpcTalkItem.txt") && char.IsLetter(split.Translated[^1]))
                    {
                        Console.WriteLine($"Needed full stop:{textFileToTranslate.Path} \n{split.Translated}");
                        split.Translated += '.';
                        recordsModded++;
                        //Don't continue we still want other stuff to happen
                    }

                    foreach (var glossary in newGlossaryStrings)
                        if (split.Text.Contains(glossary))
                        {
                            Console.WriteLine($"New Glossary {textFileToTranslate.Path} Replaces: \n{split.Translated}");
                            split.FlaggedForRetranslation = true;
                            recordsModded++;
                            continue;
                        }

                    // Clean up Diacritics
                    var cleanedUp = LineValidation.CleanupLineBeforeSaving(split.Translated, split.Text, outputFile);
                    if (cleanedUp != split.Translated)
                    {
                        Console.WriteLine($"Cleaned up {textFileToTranslate.Path} \n{split.Translated}\n{cleanedUp}");
                        split.Translated = cleanedUp;
                        recordsModded++;
                        continue;
                    }

                    // Remove Invalid ones
                    var result = LineValidation.CheckTransalationSuccessful(config, split.Text, split.Translated ?? string.Empty);
                    if (!result.Valid)
                    {
                        Console.WriteLine($"Invalid {textFileToTranslate.Path} Failures:{result.CorrectionPrompt}\n{split.Translated}");
                        split.Translated = string.Empty;
                        recordsModded++;
                    }
                }
            }

            totalRecordsModded += recordsModded;
            var serializer = Yaml.CreateSerializer();            
            if (recordsModded > 0)
            {
                Console.WriteLine($"Writing {recordsModded} records to {outputFile}");
                await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
            }
        });

        Console.WriteLine($"Total Lines: {totalRecordsModded} records");
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

    private static bool ContainsGender(string input)
    {
        // Deliberately only want 'he' and not 'He' (because common name)
        if (input.Contains(" he "))
        {
            if (input.Contains("brother", StringComparison.OrdinalIgnoreCase) || input.Contains("lord", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        if (input.StartsWith("she ", StringComparison.OrdinalIgnoreCase) |
            input.Contains(" she ", StringComparison.OrdinalIgnoreCase))
        {
            if (input.Contains("miss", StringComparison.OrdinalIgnoreCase) || input.Contains("lady", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        return false;
    }

    private static bool ContainsAnimalSounds(string? input)
    {
        if (input == null)
            return false;

        // Deliberately only want 'he' and not 'He' (because common name)
        if (input.Contains("meow", StringComparison.OrdinalIgnoreCase)
            || input.Contains("hss", StringComparison.OrdinalIgnoreCase)
            || input.Contains("woof", StringComparison.OrdinalIgnoreCase)
            || input.Contains("moo", StringComparison.OrdinalIgnoreCase)
            || input.Contains("chirp", StringComparison.OrdinalIgnoreCase)
            || input.Contains("hiss", StringComparison.OrdinalIgnoreCase))     
        {
            if (!input.Contains("moon", StringComparison.OrdinalIgnoreCase)
                && !input.Contains("mood", StringComparison.OrdinalIgnoreCase)
                && !input.Contains("smooth", StringComparison.OrdinalIgnoreCase))             
                return true;
        }

        return false;
    }
}
