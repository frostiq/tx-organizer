namespace TxOrganizer.DTO;

public enum TxType
{
    Trade = 0,
    Deposit = 1,
    Withdrawal = 2,
    Spend = 3,
    Migration = 4,
    Gift = 5,
    Income = 6,
    Lost = 7,
    Airdrop = 8,
    Borrow = 9,
    Repay = 10,
    Stolen = 11,
    Mining = 12
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