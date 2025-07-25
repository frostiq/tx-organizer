using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;

namespace TxOrganizer.DataSource;

public class EtherscanApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.etherscan.io/v2/api";
    private readonly string _quickNodeUrl;
    private readonly int _chainId;
    private static readonly SemaphoreSlim _etherscanRateLimitSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _quickNodeRateLimitSemaphore = new(1, 1);
    private static DateTime _lastEtherscanRequestTime = DateTime.MinValue;
    private static DateTime _lastQuickNodeRequestTime = DateTime.MinValue;
    private static readonly TimeSpan _etherscanMinRequestInterval = TimeSpan.FromMilliseconds(250); // 4 requests per second to stay under 5/sec limit
    private static readonly TimeSpan _quickNodeMinRequestInterval = TimeSpan.FromMilliseconds(21); // ~47 requests per second to stay under 50/sec limit
    
    public EtherscanApiService(HttpClient httpClient, string apiKey, string quickNodeUrl = "")
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _quickNodeUrl = quickNodeUrl;
        _chainId = 1; // Default to Ethereum Mainnet
    }
    
    private async Task EnforceEtherscanRateLimitAsync()
    {
        await _etherscanRateLimitSemaphore.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastEtherscanRequestTime;
            if (timeSinceLastRequest < _etherscanMinRequestInterval)
            {
                var delayTime = _etherscanMinRequestInterval - timeSinceLastRequest;
                Console.WriteLine($"Etherscan rate limiting: waiting {delayTime.TotalMilliseconds}ms...");
                await Task.Delay(delayTime);
            }
            _lastEtherscanRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _etherscanRateLimitSemaphore.Release();
        }
    }
    
    private async Task EnforceQuickNodeRateLimitAsync()
    {
        await _quickNodeRateLimitSemaphore.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastQuickNodeRequestTime;
            if (timeSinceLastRequest < _quickNodeMinRequestInterval)
            {
                var delayTime = _quickNodeMinRequestInterval - timeSinceLastRequest;
                Console.WriteLine($"QuickNode rate limiting: waiting {delayTime.TotalMilliseconds}ms...");
                await Task.Delay(delayTime);
            }
            _lastQuickNodeRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _quickNodeRateLimitSemaphore.Release();
        }
    }
    
    public async Task<decimal> GetBalanceAtBlockAsync(string address, long blockNumber)
    {
        await EnforceQuickNodeRateLimitAsync();
        
        // Use QuickNode JSON-RPC API for balance retrieval (better support for older blocks)
        var blockHex = $"0x{blockNumber:X}";
        var requestBody = new
        {
            jsonrpc = "2.0",
            method = "eth_getBalance",
            @params = new[] { address, blockHex },
            id = 1
        };
        
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        
        // Use configured QuickNode endpoint or throw error if not configured
        if (string.IsNullOrEmpty(_quickNodeUrl))
        {
            throw new InvalidOperationException("QuickNode URL is required for balance retrieval. Please configure it in the constructor.");
        }
        
        var response = await _httpClient.PostAsync(_quickNodeUrl, content);
        var responseString = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonObject>(responseString);
        
        // Check for JSON-RPC errors
        if (json?["error"] != null)
        {
            var errorMessage = json["error"]?["message"]?.ToString() ?? "Unknown QuickNode error";
            throw new Exception($"QuickNode API error: {errorMessage}");
        }
        
        var resultValue = json?["result"]?.ToString() ?? "0x0";
        
        // Clean and validate hex value
        var weiAmount = new HexBigInteger(resultValue);
        
        // Convert Wei to ETH using Nethereum's UnitConversion
        decimal ethAmount = UnitConversion.Convert.FromWei(weiAmount, UnitConversion.EthUnit.Ether);
        
        return ethAmount;
    }
    
    public async Task<List<EthTransaction>> GetTransactionsAsync(string address, long startBlock = 0, long endBlock = 999999999)
    {
        Console.WriteLine($"Fetching transactions for address {address} from block {startBlock} to {endBlock}...");
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
        var allTransactions = new List<EthTransaction>();
        const int pageSize = 10000; // Etherscan max page size
        int page = 1;
        
        while (true)
        {
            await EnforceEtherscanRateLimitAsync();
            
            var url = $"{_baseUrl}?chainid={_chainId}&module=account&action=txlist&address={address}&startblock={startBlock}&endblock={endBlock}&page={page}&offset={pageSize}&sort=asc&apikey={_apiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonSerializer.Deserialize<JsonObject>(response);
            
            var status = json?["status"]?.ToString();
            if (status != "1")
            {
                // No more results or error
                break;
            }
            
            var results = json?["result"]?.AsArray();
            if (results == null || results.Count == 0)
            {
                // No more results
                break;
            }
            
            var pageTransactions = new List<EthTransaction>();
            foreach (var result in results)
            {
                var tx = result?.AsObject();
                if (tx == null) continue;
                
                pageTransactions.Add(new EthTransaction
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
            
            allTransactions.AddRange(pageTransactions);
            Console.WriteLine($"Fetched page {page} with {pageTransactions.Count} normal transactions");
            
            // If we got less than the page size, we've reached the end
            if (pageTransactions.Count < pageSize)
            {
                break;
            }
            
            page++;
        }
        
        return allTransactions;
    }
    
    private async Task<List<EthTransaction>> GetInternalTransactionsAsync(string address, long startBlock, long endBlock)
    {
        var allTransactions = new List<EthTransaction>();
        const int pageSize = 10000; // Etherscan max page size
        int page = 1;
        
        while (true)
        {
            await EnforceEtherscanRateLimitAsync();
            
            var url = $"{_baseUrl}?chainid={_chainId}&module=account&action=txlistinternal&address={address}&startblock={startBlock}&endblock={endBlock}&page={page}&offset={pageSize}&sort=asc&apikey={_apiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonSerializer.Deserialize<JsonObject>(response);
            
            var status = json?["status"]?.ToString();
            if (status != "1")
            {
                // No more results or error
                break;
            }
            
            var results = json?["result"]?.AsArray();
            if (results == null || results.Count == 0)
            {
                // No more results
                break;
            }
            
            var pageTransactions = new List<EthTransaction>();
            foreach (var result in results)
            {
                var tx = result?.AsObject();
                if (tx == null) continue;
                
                pageTransactions.Add(new EthTransaction
                {
                    Hash = tx["hash"]?.ToString() ?? string.Empty,
                    BlockNumber = long.Parse(tx["blockNumber"]?.ToString() ?? "0"),
                    TransactionIndex = 0, // Use traceId for internal txs
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(tx["timeStamp"]?.ToString() ?? "0")).DateTime,
                    From = tx["from"]?.ToString() ?? string.Empty,
                    To = tx["to"]?.ToString() ?? string.Empty,
                    Value = BigInteger.Parse(tx["value"]?.ToString() ?? "0"),
                    IsInternal = true,
                    IsError = tx["isError"]?.ToString() == "1"
                });
            }
            
            allTransactions.AddRange(pageTransactions);
            Console.WriteLine($"Fetched page {page} with {pageTransactions.Count} internal transactions");
            
            // If we got less than the page size, we've reached the end
            if (pageTransactions.Count < pageSize)
            {
                break;
            }
            
            page++;
        }
        
        return allTransactions;
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
