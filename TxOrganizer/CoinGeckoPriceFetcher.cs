using System.Text.Json;

public class CoinGeckoPriceFetcher
{
    private static readonly HttpClient httpClient = new HttpClient();
    private readonly AppDbContext dbContext;

    public CoinGeckoPriceFetcher(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Dictionary<string, double>> GetPricesAsync(List<string> tokenSymbols)
    {
        var coinIdsMapping = dbContext.CoinGeckoIds
            .Where(c => tokenSymbols.Contains(c.Symbol))
            .ToDictionary(x => x.CoinId, x => x.Symbol);

        var ids = string.Join(",", coinIdsMapping.Keys);
        var url = $"https://api.coingecko.com/api/v3/simple/price?ids={ids}&vs_currencies=usd";

        var response = await httpClient.GetStringAsync(url);

        var prices = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(response);

        var result = prices.ToDictionary(kvp => coinIdsMapping[kvp.Key], kvp => kvp.Value["usd"]);

        if (tokenSymbols.Contains("ETH2.0"))
        {
            result["ETH2.0"] = result["ETH"];
        }

        return result;
    }
}