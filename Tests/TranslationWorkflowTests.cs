namespace Translate.Tests;
public class TranslationWorkflowTests
{
    const string workingDirectory = "../../../../Files";

    [Fact]
    public void ExportAssetsIntoTranslated()
    {
        TranslationService.ExportTextAssetsToCustomFormat(workingDirectory);
    }

    [Fact]
    public async Task TranslateLinesBruteForce()
    {
        await PerformTranslateLines(true);
    }

    [Fact]
    public async Task TranslateLines()
    {
        await PerformTranslateLines(false);
    }

    public async Task PerformTranslateLines(bool keepCleaning)
    {
        if (keepCleaning)
        {
            int remaining = 9999999;
            int lastRemaining = remaining;
            int iterations = 0;
            while  (remaining > 0 && iterations < 10)
            {
                await TranslationService.TranslateViaLlmAsync(workingDirectory, false);
                remaining = await TranslationCleanupTests.UpdateCurrentTranslationLines();
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
        await TranslationService.PackageFinalTranslation(workingDirectory);

        var sourceDirectory = $"{workingDirectory}/Mod";
        var gameDirectory = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\河洛群俠傳 (Ho Tu Lo Shu ： The Books of Dragon)\\Mod";
        if (Directory.Exists(gameDirectory))
            Directory.Delete(gameDirectory, true);

        TranslationService.CopyDirectory(sourceDirectory, gameDirectory);
    }
}
