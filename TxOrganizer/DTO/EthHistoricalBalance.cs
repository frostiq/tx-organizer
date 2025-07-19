namespace TxOrganizer.DTO;

public class EthHistoricalBalance
{
    public int Id { get; set; }
    
    public string Address { get; set; } = string.Empty;
    
    public long BlockNumber { get; set; }
    
    public DateTime BlockTimestamp { get; set; }
    
    public string TransactionHash { get; set; } = string.Empty;
    
    public decimal BalanceEth { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
