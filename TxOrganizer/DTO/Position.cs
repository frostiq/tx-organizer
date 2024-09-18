namespace TxOrganizer.DTO;

public class Position: TaxLot
{
    private readonly List<Transaction> _buyTransactions;

    public Position(Transaction tx) : base(tx)
    {
        _buyTransactions = new List<Transaction> { tx };
    }

    public double TotalAmount => _buyTransactions.Sum(x => x.BuyAmount);
    
    public double CostBasis => _buyTransactions.Sum(x => x.USDEquivalent);
    
    public double AverageExitPrice => Proceeds / SellTransactions.Sum(x => x.Amount);

    public double Proceeds => SellTransactions.Sum(x => x.Amount * x.PriceUsd);

    public double? CurrentPrice { get; set; }
    
    public double CurrentValue => CurrentPrice.HasValue ? RemainingAmount * CurrentPrice.Value : 0;
    
    public override double RemainingAmount => 
        TotalAmount - SellTransactions.Sum(x => x.Amount) - FeeSpendTransactions.Sum(x => x.Amount);

    public void Buy(Transaction tx)
    {
        if (!SupportedBuyTxTypes.Contains(tx.Type)) throw new ArgumentException($"Unsupported tx type: {tx.Type}");
        if (Sold) throw new ApplicationException("Can't sell against sold tax lot");
        if (tx.BuyCurrency != Currency) throw new ArgumentException("Buy transaction has an invalid currency");
        if (tx.Date < Date) throw new ArgumentException("Sell transaction predates tax lot");
        
        _buyTransactions.Add(tx);
    }

    public override string ToString()
    {
        return $"{Date}: {TotalAmount} {Currency} @ ${CostBasis} on {BuyTransaction.Location}";
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Sold, BuyTransaction);
    }
}