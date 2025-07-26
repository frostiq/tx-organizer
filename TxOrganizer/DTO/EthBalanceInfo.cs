namespace TxOrganizer.DTO;

public class EthBalanceInfo
{
    public decimal BalanceEth { get; set; }
    
    public long BlockNumber { get; set; }
    
    public string TransactionHash { get; set; } = string.Empty;
    
    public DateTime BlockTimestamp { get; set; }
    
    public string Address { get; set; } = string.Empty;
    
    /// <summary>
    /// Formats the balance information for display
    /// </summary>
    public override string ToString()
    {
        return $"ETH Balance: {BalanceEth:F6} ETH at block {BlockNumber}";
    }
}
