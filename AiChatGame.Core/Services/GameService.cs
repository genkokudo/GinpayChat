using AiChatGame.Core.Models;
using AiChatGame.Core.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiChatGame.Core.Services;

/// <summary>
/// ゲームのセッション管理とAI呼び出しを統括するサービス。
/// BlazorコンポーネントはこのクラスだけをDI経由で使用する。
/// GamePluginやKernelを直接触らないこと。
/// </summary>
public class GameService
{
    private readonly Kernel _kernel;
    private readonly GamePlugin _plugin;
    private readonly KernelPlugin _promptPlugin;
    private readonly ILogger<GameService> _logger;

    private GameState _state = new();

    public GameService(Kernel kernel, ILogger<GameService> logger)
    {
        _kernel = kernel;
        _logger = logger;

        // C#ネイティブPlugin（評価・要約など複雑なロジック）
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        _plugin = new GamePlugin(chatService);

        // ファイルベースPlugin（プロンプトテキスト外部管理）
        var promptDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "Prompts", "GamePlugin");
        _promptPlugin = kernel.ImportPluginFromPromptDirectory(promptDir, "GamePlugin");
    }

    // ─────────────────────────────────────────────
    // 公開メソッド
    // ─────────────────────────────────────────────

    /// <summary>
    /// セッション開始。genreがnullの場合AIが自動生成する。
    /// </summary>
    public async Task StartSessionAsync(string? genre, int maxRounds = 3)
    {
        _state = new GameState { MaxRounds = maxRounds };

        if (string.IsNullOrWhiteSpace(genre))
        {
            // ファイルPluginでジャンル・舞台を自動生成
            var result = await _kernel.InvokeAsync(_promptPlugin["GenerateSetting"]);
            var text = result.GetValue<string>() ?? "";
            var lines = text.Split('\n');
            _state.Genre = lines.FirstOrDefault(l => l.StartsWith("ジャンル:"))
                                  ?.Replace("ジャンル:", "").Trim() ?? "ファンタジー";
            _state.Setting = lines.FirstOrDefault(l => l.StartsWith("舞台:"))
                                  ?.Replace("舞台:", "").Trim() ?? "謎の王国";
        }
        else
        {
            _state.Genre = genre;
            _state.Setting = genre;
        }

        _logger.LogInformation("セッション開始: {Genre} / {Setting}", _state.Genre, _state.Setting);
    }

    /// <summary>
    /// 現在のゲーム状態を返す（読み取り専用で参照する用途）
    /// </summary>
    public GameState GetCurrentState() => _state;

    /// <summary>
    /// 現ラウンドの問題・状況をAIに生成させて返す。
    /// 呼び出し前にStartSessionAsyncが完了していること。
    /// </summary>
    public async Task<string> GenerateProblemAsync()
    {
        _state.CurrentRound++;
        _logger.LogInformation("Round {Round} 問題生成開始", _state.CurrentRound);

        var result = await _kernel.InvokeAsync(_promptPlugin["GenerateProblem"],
            new KernelArguments
            {
                ["setting"] = _state.Setting,
                ["storySummary"] = _state.GetStorySummary(),
                ["round"] = _state.CurrentRound.ToString(),
                ["successCount"] = _state.SuccessCount.ToString(),
                ["failureCount"] = _state.FailureCount.ToString()
            });

        var problem = result.GetValue<string>() ?? "異変が起きた。";
        _state.CurrentProblem = problem;  // ← 追加
        return problem;
    }

    /// <summary>
    /// プレイヤーの入力を送信し、AI評価結果を返す。
    /// 内部でStorySummaryへの追記も行う。
    /// </summary>
    public async Task<RoundResult> SubmitActionAsync(string playerInput)
    {
        _logger.LogInformation("Round {Round} プレイヤー行動受信", _state.CurrentRound);

        // 現在の問題テキストは直近のラウンド記録から取得
        // ※GenerateProblemAsyncの戻り値を呼び出し元で保持しておく方が確実だが、
        //   簡略化のためStateに一時保存する設計にしている
        var problem = _state.CurrentProblem;

        // Step1: プレイヤー入力をAIで要約
        var summarized = await _plugin.SummarizePlayerInputAsync(playerInput);

        // Step2: AI評価
        var (isSuccess, evaluation, nextSituation) =
            await _plugin.EvaluateActionAsync(problem, summarized, _state.Setting);

        // Step3: State更新
        if (isSuccess) _state.SuccessCount++;
        else _state.FailureCount++;

        _state.Rounds.Add(new RoundRecord
        {
            RoundNumber = _state.CurrentRound,
            Problem = problem,
            PlayerAction = summarized,
            IsSuccess = isSuccess,
            Evaluation = evaluation,
            NextSituation = nextSituation
        });

        // Step4: あらすじログに追記
        var roundSummary = await _plugin.SummarizeRoundAsync(
            problem, summarized,
            isSuccess ? "成功" : "失敗",
            nextSituation);
        _state.StoryLog.Add(roundSummary);

        _logger.LogInformation("Round {Round} 完了: {Result}", _state.CurrentRound, isSuccess ? "成功" : "失敗");

        return new RoundResult
        {
            IsSuccess = isSuccess,
            Evaluation = evaluation,
            NextSituation = nextSituation
        };
    }

    /// <summary>
    /// 全ラウンド終了後にエンディングを生成する。
    /// </summary>
    public async Task<string> GenerateEndingAsync()
    {
        _logger.LogInformation("エンディング生成開始");

        var result = await _kernel.InvokeAsync(_promptPlugin["GenerateEnding"],
            new KernelArguments
            {
                ["setting"] = _state.Setting,
                ["storyLog"] = _state.GetStorySummary(),
                ["successCount"] = _state.SuccessCount.ToString(),
                ["failureCount"] = _state.FailureCount.ToString()
            });

        return result.GetValue<string>() ?? "物語は幕を閉じた。";
    }

    /// <summary>
    /// セッションをリセットする（再プレイ用）
    /// </summary>
    public void ResetSession()
    {
        _state = new GameState();
        _logger.LogInformation("セッションリセット");
    }
}