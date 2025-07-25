namespace TxOrganizer.Configuration;

public class ApiKeysConfiguration
{
    public const string SectionName = "ApiKeys";
    
    public string Etherscan { get; set; } = string.Empty;
    public string QuickNodeUrl { get; set; } = string.Empty;
}
