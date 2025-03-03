using System.Text.Json;
using System.Text.Json.Nodes;
using TxOrganizer.Database;
using TxOrganizer.DTO;

namespace TxOrganizer.DataSource;

public class CoinGeckoPriceFetcher
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private readonly AppDbContext _dbContext;

    public CoinGeckoPriceFetcher(AppDbContext dbContext)
    {
        this._dbContext = dbContext;
    }

    public async Task<Dictionary<string, double>> GetPricesAsync(List<string> tokenSymbols)
    {
        var coinIdsMapping = _dbContext.CoinGeckoIds
            .Where(c => tokenSymbols.Contains(c.Symbol));
            
        var coingeckoData = await GetCoingeckoPricesAsync(coinIdsMapping.Where(x => !x.CoinId.StartsWith("0x")));
        var geckoTerminalData = await GetGeckoterminalPricesAsync(coinIdsMapping.Where(x => x.CoinId.StartsWith("0x")));
        
        var result = coingeckoData.Concat(geckoTerminalData)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        if (tokenSymbols.Contains("ETH2.0"))
        {
            result["ETH2.0"] = result["ETH"];
        }

        return result;
    }

    private static async Task<Dictionary<string, double>> GetCoingeckoPricesAsync(IEnumerable<CoinGeckoId> coinGeckoIds)
    {
        if (!coinGeckoIds.Any()) return new Dictionary<string, double>();
        var coinIdsMapping = coinGeckoIds.ToDictionary(x => x.CoinId, x => x.Symbol);
        var ids = string.Join(",", coinIdsMapping.Keys);
        var url = $"https://api.coingecko.com/api/v3/simple/price?ids={ids}&vs_currencies=usd";

        var response = await HttpClient.GetStringAsync(url);

        var prices = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(response);

        var result = prices!.ToDictionary(kvp => coinIdsMapping[kvp.Key], kvp => kvp.Value["usd"]);

        return result;
    }

    private static async Task<Dictionary<string, double>> GetGeckoterminalPricesAsync(IEnumerable<CoinGeckoId> coinGeckoIds)
    {
        if (!coinGeckoIds.Any()) return new Dictionary<string, double>();
        var ids = string.Join(",", coinGeckoIds.Select(x => x.CoinId));
        var url = $"https://api.geckoterminal.com/api/v2/networks/base/tokens/multi/{ids}";

        var response = await HttpClient.GetStringAsync(url);

        var prices = JsonSerializer.Deserialize<JsonNode>(response)!["data"]!.AsArray();
        var mappedPrices = prices.Select(x => new { Symbol = x["attributes"]["symbol"], PriceUsd = x["attributes"]["price_usd"] });

        var result = mappedPrices.ToDictionary(x => x.Symbol.GetValue<string>(), data => double.Parse(data.PriceUsd.GetValue<string>()));

        return result;
    }
}