using Spectre.Console;
using TxOrganizer.DTO;
using TxOrganizer.Utilities;

namespace TxOrganizer.Analysis;

public static class DuplicateTransactionAnalyzer
{
    /// <summary>
    /// Finds and highlights duplicate transactions
    /// </summary>
    /// <param name="transactions">List of transactions to analyze</param>
    public static void FindAndHighlightDuplicates(List<Transaction> transactions)
    {
        // Group transactions by date, buy currency, and sell currency
        var duplicateGroups = transactions
            .Where(x => !(x.BuyCurrency.Contains("USDT") && x.SellCurrency.Contains("USDT"))
                        && !string.IsNullOrWhiteSpace(x.BuyCurrency)
                        && !string.IsNullOrWhiteSpace(x.SellCurrency)
                        && x.BuyAmount > 0
                        && x.SellAmount > 0)
            .GroupBy(t => 
            {
                var buyDecimals = DecimalHelper.GetDecimalPlaces(t.BuyAmount);
                var sellDecimals = DecimalHelper.GetDecimalPlaces(t.SellAmount);
                var minDecimals = Math.Min(buyDecimals, sellDecimals) - 1;
                if (minDecimals < 0) minDecimals = 0;
                if (minDecimals > 15) minDecimals = 15;
                
                return new
                {
                    Date = t.Date.ToString("MM/dd/yyyy"),
                    BuyCurrency = t.BuyCurrency?.ToUpperInvariant(),
                    SellCurrency = t.SellCurrency?.ToUpperInvariant(),
                    BuyAmount = Math.Round(t.BuyAmount, minDecimals),
                    SellAmount = Math.Round(t.SellAmount, minDecimals),
                };
            });
        
        var test = duplicateGroups.Where(x => x.Key.Date.Contains("03/12/2024")).ToList();
        duplicateGroups = duplicateGroups.Where(g => g.Select(x => x.Location).Distinct().Count() > 1)
            .OrderBy(g => g.First().Date)
            .ToList();

        if (!duplicateGroups.Any())
        {
            AnsiConsole.MarkupLine("[green]No duplicates found![/]");
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Found {duplicateGroups.Count()} groups of duplicate transactions:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Date");
        table.AddColumn("Buy Currency");
        table.AddColumn("Buy Amount");
        table.AddColumn("Sell Currency");
        table.AddColumn("Sell Amount");
        table.AddColumn("Location");
        table.AddColumn("Type");

        foreach (var group in duplicateGroups)
        {
            AnsiConsole.MarkupLine($"[bold]Duplicate group: {group.Key.Date:yyyy-MM-dd} - {group.Key.BuyCurrency ?? "N/A"}/{group.Key.SellCurrency ?? "N/A"}[/]");
            
            foreach (var transaction in group.OrderBy(t => t.Location))
            {
                table.AddRow(
                    transaction.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                    transaction.BuyCurrency ?? "N/A",
                    transaction.BuyAmount.ToString("F8"),
                    transaction.SellCurrency ?? "N/A",
                    transaction.SellAmount.ToString("F8"),
                    $"[red]{transaction.Location ?? "N/A"}[/]",
                    transaction.Type.ToString()
                );
            }
            
            table.AddEmptyRow();
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[yellow]Total duplicate transactions: {duplicateGroups.Sum(g => g.Count())}[/]");
    }
}
