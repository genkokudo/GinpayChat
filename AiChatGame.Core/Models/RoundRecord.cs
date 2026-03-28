
namespace AiChatGame.Core.Models;

public class RoundRecord
{
    public int RoundNumber { get; set; }
    public string Problem { get; set; } = string.Empty;      // AIが提示した問題
    public string PlayerAction { get; set; } = string.Empty; // プレイヤーの入力
    public bool IsSuccess { get; set; }                      // 成功/失敗
    public string Evaluation { get; set; } = string.Empty;   // AIの評価コメント
    public string NextSituation { get; set; } = string.Empty;// 次の展開
}

