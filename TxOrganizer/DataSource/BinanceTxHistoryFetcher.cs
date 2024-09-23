using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CsvHelper;
using Spectre.Console;

namespace TxOrganizer.DataSource
{
    public class BinanceTxHistoryFetcher
    {
        private static readonly HttpClient Client = new HttpClient();
        
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public async Task<List<Withdrawal>> FetchWithdrawalHistory(Dictionary<string, string> headers)
        {
            const string url = "https://www.binance.com/bapi/capital/v1/private/capital/withdraw/list";
            var withdrawals = new List<Withdrawal>();

            // Add headers to HttpClient
            foreach (var header in headers.Where(header => header.Key.ToUpper() != "CONTENT-TYPE"))
            {
                Client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
            
            const int startYear = 2017;
            var endYear = DateTime.Now.Year;
            for (var year = startYear; year <= endYear; year++)
            {
                var startDate = new DateTimeOffset(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                var endDate = new DateTimeOffset(new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc));
                
                var pageOffset = 0;
                const int pageSize = 10;

                while (true)
                {
                    var body = new
                    {
                        page = new { offset = pageOffset, limit = pageSize },
                        startTime = startDate.ToUnixTimeMilliseconds(),
                        endTime = endDate.ToUnixTimeMilliseconds(),
                        coin = "",
                        status = "",
                        txId = ""
                    };

                    var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                    var responseContent = string.Empty;
                    try
                    {
                        var response = await Client.PostAsync(url, content);
                        response.EnsureSuccessStatusCode();

                        var responseStream = await response.Content.ReadAsStreamAsync();
                        if (response.Content.Headers.ContentEncoding.Contains("gzip"))
                        {
                            responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
                        }
                        using var streamReader = new StreamReader(responseStream);
                        responseContent = await streamReader.ReadToEndAsync();
                        var responseModel = JsonSerializer.Deserialize<ResponseModel>(responseContent, JsonOptions);
                        
                        if (responseModel.Data.Rows.Count == 0)
                        {
                            break;
                        }

                        withdrawals.AddRange(responseModel.Data.Rows);
                        pageOffset += pageSize;
                    }
                    catch (Exception e)
                    {
                        AnsiConsole.WriteException(e);
                        AnsiConsole.WriteLine($"Response: {responseContent}");
                        throw;
                    }
                }
            }

            return withdrawals;
        }
        
        public void WriteTransactionHistoryToCsv(string filePath, IEnumerable<Withdrawal> withdrawals)
        {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
            csv.WriteRecords(withdrawals);
        }
    }

    public class ResponseModel
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string MessageDetail { get; set; }
        public DataModel Data { get; set; }
        public bool Success { get; set; }
    }

    public class DataModel
    {
        public int Total { get; set; }
        public List<Withdrawal> Rows { get; set; }
    }

    public class Withdrawal
    {
        public string Id { get; set; }
        public string TransferAmount { get; set; }
        public string TransactionFee { get; set; }
        public ulong ApplyTime { get; set; }
        public ulong Status { get; set; }
        public string AddressUrl { get; set; }
        public ulong UserId { get; set; }
        public ulong TranId { get; set; }
        public string Coin { get; set; }
        public string Address { get; set; }
        public string AddressTag { get; set; }
        public string AssetLabel { get; set; }
        public string TxId { get; set; }
        public ulong? CurConfirmTimes { get; set; }
        public ulong? ConfirmTimes { get; set; }
        public string Info { get; set; }
        public string TxUrl { get; set; }
        public string StatusName { get; set; }
        public string ApplyTimeStr { get; set; }
        public ulong TransferType { get; set; }
        public string Network { get; set; }
        public ulong WalletType { get; set; }
        public string TxKey { get; set; }
        public string ExtensionInfo { get; set; }
    }
}