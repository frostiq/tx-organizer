namespace TxOrganizer.DTO;

public enum TxType
{
    Trade, Deposit, Withdrawal, Spend, Migration, Gift, Income, Lost, Airdrop, Borrow, Repay, Stolen
}

public class Transaction
{
    public int Id { get; set; } 
    
    public DateTime Date { get; set; }

    public TxType Type { get; set; }

    public double BuyAmount { get; set; }

    public string BuyCurrency { get; set; }

    public double SellAmount { get; set; }
    
    public string SellCurrency { get; set; }

    public double Fee { get; set; }

    public string? FeeCurrency { get; set; }

    public string Location { get; set; }

    public double USDEquivalent { get; set; }

    public string? TxHash { get; set; }

    public string? Comment { get; set; }
    
    public override string ToString()
    {
        return Type switch
        {
            TxType.Deposit => $"{Date}: {Type} {BuyAmount} {BuyCurrency}",
            TxType.Withdrawal => $"{Date}: {Type} {SellAmount} {SellCurrency}",
            TxType.Spend => $"{Date}: {Type} {Fee} {FeeCurrency}",
            _ => $"{Date}: {Type} {SellAmount} {SellCurrency} => {BuyAmount} {BuyCurrency} on {Location}"
        };
    }
}