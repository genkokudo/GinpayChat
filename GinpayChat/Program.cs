using GinpayChat.Models;
using GinpayChat.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

// appsettings.jsonとappsettings.local.jsonから設定を読み込む
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.local.json", optional: true) // ←上書きされる
    .Build();

var azureSection = config.GetSection("AzureOpenAI");
string? deployment = azureSection["Deployment"];
string? endpoint = azureSection["Endpoint"];
string? apiKey = azureSection["ApiKey"];

if (string.IsNullOrWhiteSpace(deployment))
{
    throw new InvalidOperationException("Configuration value 'AzureOpenAI:Deployment' is missing or empty.");
}
if (string.IsNullOrWhiteSpace(endpoint))
{
    throw new InvalidOperationException("Configuration value 'AzureOpenAI:Endpoint' is missing or empty.");
}
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Configuration value 'AzureOpenAI:ApiKey' is missing or empty.");
}


// Kernel構築 
var builder = Kernel.CreateBuilder();

builder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
var kernel = builder.Build();

var chatService = kernel.GetRequiredService<IChatCompletionService>();
var plugin = new GamePlugin(chatService);

// ゲーム開始
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("═══════════════════════════════════");
Console.WriteLine("    AI Chat Game  -  Phase 1");
Console.WriteLine("═══════════════════════════════════");

var state = new GameState { MaxRounds = 3 };

// セットアップ：ジャンル選択
Console.WriteLine("\n[1] AIに舞台を決めてもらう  [2] 自分で入力する");
Console.Write("選択 > ");
var choice = Console.ReadLine();

if (choice == "2")
{
    Console.Write("ジャンルを入力してください > ");
    state.Genre = Console.ReadLine() ?? "ファンタジー";
    Console.Write("舞台設定を入力してください > ");
    state.Setting = Console.ReadLine() ?? "謎の王国";
}
else
{
    Console.WriteLine("\nAIが舞台を考えています...");
    (state.Genre, state.Setting) = await plugin.GenerateSettingAsync();
}

Console.WriteLine($"\n【舞台】{state.Genre}");
Console.WriteLine($"【設定】{state.Setting}");
Console.WriteLine("\nEnterキーでゲームスタート！");
Console.ReadLine();

// ── メインゲームループ ──────────────────────────────────────────
while (!state.IsComplete)
{
    state.CurrentRound++;
    Console.WriteLine($"\n━━━ Round {state.CurrentRound} / {state.MaxRounds} ━━━");

    // 問題生成（リトライループ付き）
    string problem = string.Empty;
    while (true)
    {
        var summary = state.GetStorySummary();
        try
        {
            Console.WriteLine("AIが状況を生成中...");
            problem = await plugin.GenerateProblemAsync(
                state.Setting, summary,
                state.CurrentRound, state.SuccessCount, state.FailureCount);
            break; // 成功したら抜ける
        }
        catch (HttpOperationException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            Console.WriteLine("⚠️  AIのコンテントフィルターに引っかかりました。");
            Console.WriteLine("別の展開で再生成します...");
            // あらすじを少し書き換えて再試行
            summary = string.IsNullOrEmpty(summary)
                ? "穏やかな冒険が始まった。"
                : summary + "（場面は穏やかな方向へ転じた）";
        }
    }

    Console.WriteLine($"\n【状況】\n{problem}\n");

    // プレイヤー入力〜評価（リトライループ付き）
    while (true)
    {
        Console.Write("あなたの行動・台詞を入力してください > ");
        var rawInput = Console.ReadLine() ?? "";

        try
        {
            Console.WriteLine("入力を要約中...");
            var playerAction = await plugin.SummarizePlayerInputAsync(rawInput);

            Console.WriteLine("\nAIが評価中...");
            var (isSuccess, evaluation, nextSituation) =
                await plugin.EvaluateActionAsync(problem, playerAction, state.Setting);

            Console.WriteLine(isSuccess ? "\n✅ 成功！" : "\n❌ 失敗...");
            Console.WriteLine($"【評価】{evaluation}");
            Console.WriteLine($"【次の展開】{nextSituation}");

            if (isSuccess) state.SuccessCount++;
            else state.FailureCount++;

            state.Rounds.Add(new RoundRecord
            {
                RoundNumber = state.CurrentRound,
                Problem = problem,
                PlayerAction = playerAction,
                IsSuccess = isSuccess,
                Evaluation = evaluation,
                NextSituation = nextSituation
            });

            Console.WriteLine("あらすじを記録中...");
            var roundSummary = await plugin.SummarizeRoundAsync(
                problem,
                playerAction,
                isSuccess ? "成功" : "失敗",
                nextSituation);

            // ★ 洗い替えやなくリストに追加
            state.StoryLog.Add(roundSummary);
            Console.WriteLine($"【ログ追加】{roundSummary}");

            break;
        }
        catch (HttpOperationException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            Console.WriteLine("⚠️  その入力はAIのフィルターに引っかかりました。");
            Console.Write("別の行動を入力してください > ");
            // ループの先頭に戻って再入力させる
        }
    }

    Console.WriteLine("\nEnterキーで続ける...");
    Console.ReadLine();
}

// ── エンディング ────────────────────────────────────────────────
Console.WriteLine("\n═══════════════════════════════════");
Console.WriteLine("         ENDING");
Console.WriteLine("═══════════════════════════════════");
Console.WriteLine("AIが物語を紡いでいます...\n");

var ending = await plugin.GenerateEndingAsync(
    state.Setting,
    state.GetStorySummary(),
    state.SuccessCount,
    state.FailureCount);

Console.WriteLine(ending);
Console.WriteLine("\n━━━ おわり ━━━");
Console.ReadLine();


