using CsvHelper;
using Spectre.Console;
using TxOrganizer.DTO;

namespace TxOrganizer.ConsoleRender;

public class PositionsRenderer: UnmatchedSpendsRenderer
{
    public void PrintPositions(IEnumerable<Position> allPositions, IEnumerable<TxSpend> unmatchedSpends)
    {
        AnsiConsole.Clear();
        
        var onlyCurrentPositions = AnsiConsole.Confirm("Only current positions?", false);
        
        if (onlyCurrentPositions)
        {
            allPositions = allPositions.Where(x => x.Sold == false);
        }

        var assetFilter = AnsiConsole.Ask("Asset filter:", string.Empty);
        if (!string.IsNullOrEmpty(assetFilter))
        {
            allPositions = allPositions.Where(x => x.Currency.Contains(assetFilter));
        }
        
        var valueThreshold = AnsiConsole.Ask<double>("Value threshold, USD", 1000);
        allPositions = allPositions.Where(x => x.CostBasis > valueThreshold || x.Proceeds > valueThreshold);

        var totalCount = allPositions.Count();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Asset", column => { column.Footer("Total: " + totalCount); });
        table.AddColumn("Opened Date");
        table.AddColumn("Closing Date");
        table.AddColumn("Max Qty");
        table.AddColumn("Remaining Qty");
        table.AddColumn("Avg Cost");
        table.AddColumn("Market / Close Price");
        table.AddColumn("Cost");
        table.AddColumn("Gain/Loss");
        table.AddColumn("ROI%");

        var outputPositions = allPositions.Select(x => new
        {
            x.Currency,
            x.Date,
            x.Sold,
            ClosingDate = x.Sold ? x.SellTransactions.Max(x => x.Tx.Date) : (DateTime?)null,
            MaxQty = $"{x.TotalAmount:N} {x.Currency}",
            RemainingAmount = $"{x.RemainingAmount:N} {x.Currency}",
            AvgPrice = x.CostBasis / x.TotalAmount,
            LastPrice = x.Sold ? x.AverageExitPrice : x.CurrentPrice,
            x.CostBasis,
            GainLoss = x.Sold || x.CurrentPrice.HasValue ? x.Proceeds - x.CostBasis + x.CurrentValue : (double?)null,
            ROI = x.Sold || x.CurrentPrice.HasValue ? (x.Proceeds - x.CostBasis + x.CurrentValue) / x.CostBasis : (double?)null
        });

        foreach (var position in outputPositions)
        {
            var style = !position.Sold ? Style.Parse("blue") : Style.Plain;
            table.AddRow(
                new Markup(Markup.Escape(position.Currency), style), // Asset
                new Markup($"{position.Date:d}", style), // Opened Date
                new Markup($"{position.ClosingDate:d}"), // Closing date
                new Markup(Markup.Escape(position.MaxQty), style), // Max Qty
                new Markup(Markup.Escape(position.RemainingAmount), style), // Remaining Qty
                new Markup($"{position.AvgPrice:C}", style), // Avg Price
                new Markup(position.LastPrice.HasValue ? $"{position.LastPrice:C}" : "???", style), // Market / Close Price
                new Markup($"{position.CostBasis:C}", style), // Cost
                new Markup(position.GainLoss.HasValue ? $"{position.GainLoss:C}" : "???", style), // Gain/Loss
                new Markup(position.ROI.HasValue ? $"{position.ROI:P}" : "???", style) // ROI%
            );
        }

        AnsiConsole.Write(table);
        
        if (!unmatchedSpends.Any())
        {
            return;
        }
        
        PrintUnmatchedSpends(unmatchedSpends);

        var needCsv = AnsiConsole.Confirm("Produce CSV file?", false);
        if (needCsv)
        {
            WritePositionsToCsv(outputPositions, "positions.csv");
        }
    }

    public static void WritePositionsToCsv<T>(IEnumerable<T> positions, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
        csv.WriteRecords(positions);
    }

}