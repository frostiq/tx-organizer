using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CsvHelper;
using Spectre.Console;
using TxOrganizer.DataSource;

namespace TxOrganizer.Processors;

public class DataProcessor
{
    public static async Task DownloadBinanceData()
    {
        var fetcher = new BinanceTxHistoryFetcher();

        var headers = ReadRequestHeaders();

        var type = AnsiConsole.Prompt(new SelectionPrompt<string>().AddChoices(
            "Deposits", "Withdrawals"
        ));

        switch (type)
        {
            case "Deposits":
            {
                var transactions = await fetcher.FetchDepositHistory(headers);
                WriteTransactionHistoryToCsv("binance-deposits.csv", transactions);

                break;
            }
            case "Withdrawals":
            {
                var transactions = await fetcher.FetchWithdrawalHistory(headers);
                WriteTransactionHistoryToCsv("binance-withdrawals.csv", transactions);
                break;
            }
        }
    }

    public static async Task DownloadTokenTaxLineItems()
    {
        var fetcher = new TokenTaxLineItemsFetcher();
        var headers = ReadRequestHeaders();
        var lineItems = await fetcher.FetchTokenTaxLineItems(7762773, headers);
        
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(lineItems, options);
        await File.WriteAllTextAsync("lineItems.json", json);
    }
    
    public static void WriteTransactionHistoryToCsv<T>(string filePath, IEnumerable<T> entities)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
        csv.WriteRecords(entities);
    }

    private static Dictionary<string, string> ReadRequestHeaders()
    {
        var lines = new List<string>();
        while (true)
        {
            var line = AnsiConsole.Prompt(new TextPrompt<string>("Enter your headers (or 'END' to finish):"));
            if (line.ToUpper() == "END")
            {
                break;
            }

            lines.Add(line);
        }

        var rawHeaders = string.Join("\n", lines);
        var matches = new Regex("-H '([^:;]+)[:;] ([^']+)").Matches(rawHeaders);
        var cookieMatch = new Regex("-b '([^']+)'").Match(rawHeaders);

        var headers = matches.ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        if (cookieMatch.Success)
        {
            headers.Add("Cookie", cookieMatch.Groups[1].Value);
        }
        
        
        return headers;
    }
}