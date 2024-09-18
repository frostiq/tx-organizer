namespace TxOrganizer.DTO;

public class TaxLot
{
    private readonly List<TxSpend> _sellTransactions;

    private readonly List<TxSpend> _feeSpendTransactions;

    public static readonly TxType[] SupportedBuyTxTypes = { TxType.Trade, TxType.Migration, TxType.Airdrop, TxType.Income };

    public TaxLot(Transaction tx)
    {
        if (!SupportedBuyTxTypes.Contains(tx.Type)) throw new ArgumentException($"Unsupported tx type: {tx.Type}");
        
        BuyTransaction = tx;
        _sellTransactions = new List<TxSpend>();
        _feeSpendTransactions = new List<TxSpend>();
    }

    public DateTime Date => BuyTransaction.Date;

    public string Currency => BuyTransaction.BuyCurrency;

    public double InitialAmount => BuyTransaction.BuyAmount;

    public double InitialCostBasis => BuyTransaction.USDEquivalent;

    public double InitialPrice => InitialCostBasis / InitialAmount;

    public string Location => BuyTransaction.Location;

    public virtual double RemainingAmount =>
        BuyTransaction.BuyAmount - SellTransactions.Sum(x => x.Amount) - FeeSpendTransactions.Sum(x => x.Amount);

    public bool Sold { get; private set; }

    public Transaction BuyTransaction { get; set; }

    public IEnumerable<TxSpend> SellTransactions => _sellTransactions;

    public IEnumerable<TxSpend> FeeSpendTransactions => _feeSpendTransactions;

    /// <param name="tx"></param>
    /// <param name="sellAmount">amount from tx to sell</param>
    /// <returns>The unsold transaction amount</returns>
    /// <exception cref="ArgumentException"></exception>
    public (double remaining, double sold) Sell(Transaction tx, double sellAmount)
    {
        if (Sold) throw new ApplicationException("Can't sell against sold tax lot");
        if (sellAmount <= 0) throw new ArgumentOutOfRangeException(nameof(sellAmount));
        if (tx.SellCurrency != Currency) throw new ArgumentException("Sell transaction has an invalid currency");
        if (tx.Date < Date) throw new ArgumentException("Sell transaction predates tax lot");

        var remaining = RemainingAmount;
        var sold = Math.Min(sellAmount, remaining);
        _sellTransactions.Add(new TxSpend(tx, sold));

        var buffer = remaining * 0.001;
        Sold = sellAmount >= remaining - buffer;
            
        return (sellAmount - sold, sold);
    }
    
    public (double remaining, double sold) SpendFee(Transaction tx, double spendAmount)
    {
        if (Sold) throw new ApplicationException("Can't spemd against sold tax lot");
        if (spendAmount <= 0) throw new ArgumentOutOfRangeException(nameof(spendAmount));
        if (tx.FeeCurrency != Currency) throw new ArgumentException("Fee spend transaction has an invalid currency");
        if (tx.Date < Date) throw new ArgumentException("Fee spend transaction predates tax lot");

        var remaining = RemainingAmount;
        var sold = Math.Min(spendAmount, remaining);
        _feeSpendTransactions.Add(new TxSpend(tx, sold));

        var buffer = remaining * 0.0001;
        Sold = spendAmount >= remaining - buffer;
            
        return (spendAmount - sold, sold);
    }

    public override string ToString()
    {
        return $"{Date}: {InitialAmount} {Currency} @ {InitialPrice} on {BuyTransaction.Location}";
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Sold, BuyTransaction);
    }
}