using System;
using System.Collections.Generic;
using System.Text;

namespace GinpayChat.Models;

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

    public bool IsComplete => CurrentRound >= MaxRounds;
}

public class RoundRecord
{
    public int RoundNumber { get; set; }
    public string Problem { get; set; } = string.Empty;      // AIが提示した問題
    public string PlayerAction { get; set; } = string.Empty; // プレイヤーの入力
    public bool IsSuccess { get; set; }                      // 成功/失敗
    public string Evaluation { get; set; } = string.Empty;   // AIの評価コメント
    public string NextSituation { get; set; } = string.Empty;// 次の展開
}