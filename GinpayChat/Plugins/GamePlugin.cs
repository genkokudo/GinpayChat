using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace GinpayChat.Plugins;

/// <summary>
/// ゲームの核心ロジックをSKプラグインとして定義。
/// [KernelFunction]属性をつけることでSKがAIに機能を公開できる。
/// Phase2ではこのクラスをそのままライブラリに移植する。
/// </summary>
public class GamePlugin(IChatCompletionService chat)
{
    /// <summary>
    /// プレイヤーの入力文章をAIに要約させる
    /// </summary>
    [KernelFunction, Description("プレイヤーの入力を簡潔に要約する")]
    public async Task<string> SummarizePlayerInputAsync(
        [Description("プレイヤーが入力した文章")] string playerInput)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(
            "あなたは文章を簡潔にまとめる要約係です。");
        history.AddUserMessage(
            $"以下の文章を100文字以内で要約してください。\n" +
            $"要約文だけを出力し、余計な説明は不要です。\n\n" +
            $"{playerInput}");

        var result = await chat.GetChatMessageContentAsync(history);
        return result.Content ?? playerInput;
    }

    /// <summary>
    /// ジャンルと舞台を自動生成する
    /// </summary>
    [KernelFunction, Description("ゲームのジャンルと舞台をAIが自動生成する")]
    public async Task<(string Genre, string Setting)> GenerateSettingAsync()
    {
        var history = new ChatHistory();
        history.AddSystemMessage(
            "あなたはTRPGのゲームマスターです。");
        history.AddUserMessage(
            "ゲームの舞台を1つ考えてください。\n" +
            "ジャンル（例: ファンタジー冒険、現代オフィス、SF宇宙船）と\n" +
            "具体的な舞台設定を1～2文で答えてください。\n" +
            "形式: ジャンル: XXX\n舞台: XXX");

        var result = await chat.GetChatMessageContentAsync(history);
        var text = result.Content ?? "";

        // パース
        var lines = text.Split('\n');
        var genre = lines.FirstOrDefault(l => l.StartsWith("ジャンル:"))
                        ?.Replace("ジャンル:", "").Trim() ?? "ファンタジー";
        var setting = lines.FirstOrDefault(l => l.StartsWith("舞台:"))
                          ?.Replace("舞台:", "").Trim() ?? "謎の王国";

        return (genre, setting);
    }


    [KernelFunction, Description("直前のラウンドを1文で要約する")]
    public async Task<string> SummarizeRoundAsync(
    [Description("今回の状況")] string problem,
    [Description("プレイヤーの行動（要約済み）")] string playerAction,
    [Description("結果（成功/失敗）")] string result,
    [Description("次の展開")] string nextSituation)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(
            "あなたはTRPGセッションの記録係です。簡潔な日本語で書いてください。");
        history.AddUserMessage(
            $"状況: {problem}\n" +
            $"行動: {playerAction}\n" +
            $"結果: {result}\n" +
            $"次の展開: {nextSituation}\n\n" +
            "上記を50文字以内の1文で要約してください。" +
            "要約文だけを出力し、余計な説明は不要です。");

        var aiResult = await chat.GetChatMessageContentAsync(history);
        return aiResult.Content ?? $"ラウンド：{result}";
    }

    /// <summary>
    /// 現在の状態から問題・事件を生成する
    /// </summary>
    [KernelFunction, Description("現在のゲーム状態に応じた問題・事件を生成する")]
    public async Task<string> GenerateProblemAsync(
        [Description("ゲームの舞台設定")] string setting,
        [Description("全ラウンドのあらすじログ")] string storyLog,
        [Description("現在のラウンド番号")] int round,
        [Description("成功回数")] int successCount,
        [Description("失敗回数")] int failureCount)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(
            $"あなたはTRPGのゲームマスターです。\n" +
            $"【舞台】{setting}\n" +
            $"【これまでのあらすじ】{(string.IsNullOrEmpty(storyLog) ? "（まだなし）" : storyLog)}\n" +
            $"成功: {successCount}回, 失敗: {failureCount}回\n\n" +
            "必ずあらすじの続きとなる問題を出すこと。" +
            "すでに起きた出来事と同じ状況を繰り返さないこと。");
        history.AddUserMessage(
            $"ラウンド{round}の問題・事件を提示してください。" +
            "プレイヤーが解決策や台詞を入力できるような具体的な状況を3～5文で描写してください。");

        var result = await chat.GetChatMessageContentAsync(history);
        return result.Content ?? "異変が起きた。";
    }

    /// <summary>
    /// プレイヤーの行動を評価して成功/失敗と次の展開を返す
    /// </summary>
    [KernelFunction, Description("プレイヤーの行動を評価する")]
    public async Task<(bool IsSuccess, string Evaluation, string NextSituation)> EvaluateActionAsync(
        [Description("現在の問題・状況")] string problem,
        [Description("プレイヤーの行動・台詞")] string playerAction,
        [Description("ゲームの舞台設定")] string setting)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(
            $"あなたはTRPGのゲームマスターです。舞台: {setting}");
        history.AddUserMessage(
            $"状況: {problem}\n" +
            $"プレイヤーの行動: {playerAction}\n\n" +
            "この行動を評価してください。\n" +
            "形式:\n" +
            "結果: 成功 or 失敗\n" +
            "評価: （2～3文で評価コメント）\n" +
            "次の展開: （1～2文で次の状況）");

        var result = await chat.GetChatMessageContentAsync(history);
        var text = result.Content ?? "";
        var lines = text.Split('\n');

        var isSuccess = lines.FirstOrDefault(l => l.StartsWith("結果:"))
                            ?.Contains("成功") ?? false;
        var evaluation = lines.FirstOrDefault(l => l.StartsWith("評価:"))
                             ?.Replace("評価:", "").Trim() ?? "";
        var next = lines.FirstOrDefault(l => l.StartsWith("次の展開:"))
                       ?.Replace("次の展開:", "").Trim() ?? "";

        return (isSuccess, evaluation, next);
    }

    /// <summary>
    /// 全セッションを統合した一貫したエンディングを生成する
    /// </summary>
    [KernelFunction, Description("セッション全体を統合した物語（エンディング）を生成する")]
    public async Task<string> GenerateEndingAsync(
        [Description("ゲームの舞台設定")] string setting,
        [Description("全ラウンドのあらすじログ")] string storyLog,
        [Description("成功回数")] int successCount,
        [Description("失敗回数")] int failureCount)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(
            "あなたは優れた小説家兼TRPGゲームマスターです。");
        history.AddUserMessage(
            $"舞台: {setting}\n" +
            $"セッションの記録: {storyLog}\n" +
            $"結果: 成功{successCount}回、失敗{failureCount}回\n\n" +
            "これまでの全展開を踏まえた、一貫性のある物語のエンディングを書いてください。\n" +
            $"成功{successCount}回が多ければハッピーエンド、" +
            $"失敗{failureCount}回が多ければ苦いエンディングにしてください。\n" +
            "300～500文字程度の読み応えある文章でお願いします。");

        var result = await chat.GetChatMessageContentAsync(history);
        return result.Content ?? "物語は幕を閉じた。";
    }
}

