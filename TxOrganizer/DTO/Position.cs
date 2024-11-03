namespace TxOrganizer.DTO;

public enum PositionType
{
    Investment,
    Arbitrage,
    Perpetuals
}

public class Position: TaxLot
{
    private readonly List<Transaction> _buyTransactions;
    
    public PositionType PositionType { get; }
    
    public double? CurrentPrice { get; set; }

    public Position(Transaction tx, PositionType positionType) : base(tx, positionType != PositionType.Investment)
    {
        PositionType = positionType;
        _buyTransactions = new List<Transaction> { tx };
    }
    
    public IEnumerable<Transaction> BuyTransactions => _buyTransactions;

    public double TotalAmount => _buyTransactions.Sum(x => x.BuyAmount);
    
    public double CostBasis => _buyTransactions.Sum(x => x.USDEquivalent);
    
    public double AverageExitPrice => Proceeds / TxSpends.Sum(x => x.Amount);

    public double Proceeds => TxSpends.Sum(x => x.Amount * x.PriceUsd);

    public double CurrentValue => CurrentPrice.HasValue ? RemainingAmount * CurrentPrice.Value : 0;
    
    public override double RemainingAmount => 
        TotalAmount - TxSpends.Sum(x => x.Amount) - FeeSpendTransactions.Sum(x => x.Amount);

    public void Buy(Transaction tx)
    {
        if (!SupportedBuyTxTypes.Contains(tx.Type)) throw new ArgumentException($"Unsupported tx type: {tx.Type}");
        if (Sold && !CanBeReopened) throw new ApplicationException("Can't buy into already sold position");
        if (tx.BuyCurrency != Currency) throw new ArgumentException("Buy transaction has an invalid currency");
        if (tx.Date < Date) throw new ArgumentException("Buy transaction predates position creation date: " + tx);
        
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
    
    public void RemoveBuyTransaction(Transaction tx)
    {
        _buyTransactions.Remove(tx);
    }
}