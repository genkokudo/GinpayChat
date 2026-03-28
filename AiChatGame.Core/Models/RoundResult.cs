using System;
using System.Collections.Generic;
using System.Text;

namespace AiChatGame.Core.Models;

/// <summary>
/// 1ラウンドの評価結果。Blazorコンポーネントに返す。
/// </summary>
public class RoundResult
{
    public bool IsSuccess { get; set; }
    public string Evaluation { get; set; } = string.Empty;
    public string NextSituation { get; set; } = string.Empty;
}