namespace TxOrganizer.DTO;

public class LocalBalance
{
    public enum ProcessingStatus
    {
        Processed,
        NotRelevant,
        NegativeBalance,
        Diverged,
    }

    private List<Transaction> _transactions;

    public string Location { get; private set; }

    public string Currency { get; private set; }

    public double Balance { get; private set; }

    public double Deposited { get; private set; }

    public double Withdrawn { get; private set; }
    
    public double Bought { get; private set; }

    public double Sold { get; private set; }

    public double Fees { get; private set; }

    public double LastDivergence { get; private set; }

    public LocalBalance(string? location, string currency)
    {
        Location = location;
        Currency = currency;
        _transactions = new List<Transaction>();
    }

    public void UpdateBalance(double amount)
    {
        Balance += amount;
    }
    
    public void UpdateLastDivergence(double actualBalance)
    {
        // Calculate divergence between our calculated balance and actual balance
        LastDivergence = Math.Abs(Balance - actualBalance);
    }
    
    public void UpdateDeposited(double amount)
    {
        Deposited += amount;
    }
    
    public void UpdateWithdrawn(double amount)
    {
        Withdrawn += amount;
    }
    
    public void UpdateBought(double amount)
    {
        Bought += amount;
    }
    
    public void UpdateSold(double amount)
    {
        Sold += amount;
    }
    
    public void UpdateFees(double amount)
    {
        Fees += amount;
    }
    
    public void AddTransaction(Transaction tx)
    {
        _transactions.Add(tx);
    }
}