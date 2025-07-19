using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TxOrganizer.DataSource;

public class EtherscanApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.etherscan.io/api";
    
    public EtherscanApiService(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }
    
    public async Task<decimal> GetBalanceAtBlockAsync(string address, long blockNumber)
    {
        var url = $"{_baseUrl}?module=account&action=balance&address={address}&tag={blockNumber}&apikey={_apiKey}";
        
        var response = await _httpClient.GetStringAsync(url);
        var json = JsonSerializer.Deserialize<JsonObject>(response);
        
        if (json?["status"]?.ToString() != "1")
        {
            throw new Exception($"Etherscan API error: {json?["message"]?.ToString()}");
        }
        
        var balanceWei = BigInteger.Parse(json["result"]?.ToString() ?? "0");
        return (decimal)balanceWei / (decimal)Math.Pow(10, 18); // Convert Wei to ETH
    }
    
    public async Task<List<EthTransaction>> GetTransactionsAsync(string address, long startBlock = 0, long endBlock = 999999999)
    {
        var normalTxs = await GetNormalTransactionsAsync(address, startBlock, endBlock);
        var internalTxs = await GetInternalTransactionsAsync(address, startBlock, endBlock);
        
        var allTxs = normalTxs.Concat(internalTxs)
            .OrderBy(tx => tx.BlockNumber)
            .ThenBy(tx => tx.TransactionIndex)
            .ToList();
            
        return allTxs;
    }
    
    private async Task<List<EthTransaction>> GetNormalTransactionsAsync(string address, long startBlock, long endBlock)
    {
        var url = $"{_baseUrl}?module=account&action=txlist&address={address}&startblock={startBlock}&endblock={endBlock}&sort=asc&apikey={_apiKey}";
        
        var response = await _httpClient.GetStringAsync(url);
        var json = JsonSerializer.Deserialize<JsonObject>(response);
        
        if (json?["status"]?.ToString() != "1")
        {
            return new List<EthTransaction>();
        }
        
        var transactions = new List<EthTransaction>();
        var results = json["result"]?.AsArray();
        
        if (results == null) return transactions;
        
        foreach (var result in results)
        {
            var tx = result?.AsObject();
            if (tx == null) continue;
            
            transactions.Add(new EthTransaction
            {
                Hash = tx["hash"]?.ToString() ?? string.Empty,
                BlockNumber = long.Parse(tx["blockNumber"]?.ToString() ?? "0"),
                TransactionIndex = int.Parse(tx["transactionIndex"]?.ToString() ?? "0"),
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(tx["timeStamp"]?.ToString() ?? "0")).DateTime,
                From = tx["from"]?.ToString() ?? string.Empty,
                To = tx["to"]?.ToString() ?? string.Empty,
                Value = BigInteger.Parse(tx["value"]?.ToString() ?? "0"),
                IsError = tx["isError"]?.ToString() == "1"
            });
        }
        
        return transactions;
    }
    
    private async Task<List<EthTransaction>> GetInternalTransactionsAsync(string address, long startBlock, long endBlock)
    {
        var url = $"{_baseUrl}?module=account&action=txlistinternal&address={address}&startblock={startBlock}&endblock={endBlock}&sort=asc&apikey={_apiKey}";
        
        var response = await _httpClient.GetStringAsync(url);
        var json = JsonSerializer.Deserialize<JsonObject>(response);
        
        if (json?["status"]?.ToString() != "1")
        {
            return new List<EthTransaction>();
        }
        
        var transactions = new List<EthTransaction>();
        var results = json["result"]?.AsArray();
        
        if (results == null) return transactions;
        
        foreach (var result in results)
        {
            var tx = result?.AsObject();
            if (tx == null) continue;
            
            transactions.Add(new EthTransaction
            {
                Hash = tx["hash"]?.ToString() ?? string.Empty,
                BlockNumber = long.Parse(tx["blockNumber"]?.ToString() ?? "0"),
                TransactionIndex = int.Parse(tx["traceId"]?.ToString() ?? "0"), // Use traceId for internal txs
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(tx["timeStamp"]?.ToString() ?? "0")).DateTime,
                From = tx["from"]?.ToString() ?? string.Empty,
                To = tx["to"]?.ToString() ?? string.Empty,
                Value = BigInteger.Parse(tx["value"]?.ToString() ?? "0"),
                IsInternal = true,
                IsError = tx["isError"]?.ToString() == "1"
            });
        }
        
        return transactions;
    }
}

public class EthTransaction
{
    public string Hash { get; set; } = string.Empty;
    public long BlockNumber { get; set; }
    public int TransactionIndex { get; set; }
    public DateTime Timestamp { get; set; }
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public BigInteger Value { get; set; }
    public bool IsInternal { get; set; }
    public bool IsError { get; set; }
}
