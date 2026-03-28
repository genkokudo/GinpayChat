using System;
using System.Collections.Generic;
using System.Text;

namespace AiChatGame.Core.Models;

/// <summary>
/// ゲームセッション全体の状態を保持するクラス。
/// Phase2でライブラリ化するときそのまま移植できるよう設計してある。
/// </summary>
public class GameState
{
    public string Genre { get; set; } = string.Empty;
    public string Setting { get; set; } = string.Empty;
    public int CurrentRound { get; set; } = 0;
    public int MaxRounds { get; set; } = 3;

    // 各ラウンドの記録（問題・プレイヤー行動・評価結果）
    public List<RoundRecord> Rounds { get; set; } = new();

    // 成功/失敗の積み重ね（エンディングの色合いを変える）
    public int SuccessCount { get; set; } = 0;
    public int FailureCount { get; set; } = 0;
    // ラウンドごとに蓄積されるあらすじ
    public List<string> StoryLog { get; set; } = new();

    public bool IsComplete => CurrentRound >= MaxRounds;
    public string CurrentProblem { get; set; } = string.Empty;

    // GenerateProblemAsync用に直近のあらすじを連結して返す
    public string GetStorySummary() =>
        StoryLog.Count == 0
            ? "（まだなし）"
            : string.Join(" → ", StoryLog);
}
