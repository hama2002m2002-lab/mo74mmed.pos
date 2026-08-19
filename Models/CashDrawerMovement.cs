using System;

namespace HamoPos.Models;

public class CashDrawerMovement : BaseEntity
{
    public string CashierName { get; set; } = string.Empty;
    public string MovementType { get; set; } = "Deposit"; // "Deposit" (إيداع) or "Withdrawal" (سحب)
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string MovementTypeDisplayName => MovementType switch
    {
        "Withdrawal" => "📤 سحب من الدرج (راکێشان)",
        _ => "📥 إيداع في الدرج (دانان)"
    };
    public string FormattedAmount => $"{(MovementType == "Withdrawal" ? "-" : "+")}{Amount:N0} د.ع";
}
