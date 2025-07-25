namespace TxOrganizer.DTO;

public class AddressLabel
{
    public string Address { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Category { get; set; } // e.g., "Exchange", "DeFi Protocol", "Personal Wallet", "Contract"
}
