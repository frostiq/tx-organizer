namespace TxOrganizer.DTO;

public class Position: TaxLot
{
    private readonly List<Transaction> _buyTransactions;
    
    public double? CurrentPrice { get; set; }

    public Position(Transaction tx, bool isArbitrage = false) : base(tx, isArbitrage)
    {
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
        if (Sold && !IsArbitrage) throw new ApplicationException("Can't buy into already sold tax lot");
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