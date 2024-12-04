using CsvHelper;
using Spectre.Console;
using TxOrganizer.Database;
using TxOrganizer.DTO;

namespace TxOrganizer.ConsoleRender;

public class PositionsRenderer: UnmatchedSpendsRenderer
{
    private readonly SettingsRepository _settingsRepository;

    public PositionsRenderer(SettingsRepository settingsRepository)
    {
        this._settingsRepository = settingsRepository;
    }

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
        allPositions = allPositions.Where(x => x.CostBasis > valueThreshold || x.Proceeds > valueThreshold)
            .OrderBy(x => x.Date);

        var totalCount = allPositions.Count();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Asset", column => { column.Footer("Total: " + totalCount); });
        table.AddColumn("Opened Date");
        table.AddColumn("Closing Date");
        table.AddColumn("Total Qty");
        table.AddColumn("Remaining Qty");
        table.AddColumn("Avg Cost");
        table.AddColumn("Market / Close Price");
        table.AddColumn("Cost");
        table.AddColumn("Gain/Loss");
        table.AddColumn("ROI%");
        table.AddColumn("Annotation");

        string MapCurrency(Position position) =>
            position.PositionType switch
            {
                PositionType.Investment => position.Currency,
                PositionType.Arbitrage => $"{position.Currency} [ARB]",
                PositionType.Perpetuals => $"{position.Currency} [PERP]",
                _ => throw new ArgumentOutOfRangeException()
            };

        var outputPositions = allPositions.Select(x => new
        {
            Currency = MapCurrency(x),
            x.Date,
            x.Sold,
            ClosingDate = x.Sold && x.TxSpends.Any() ? x.TxSpends.Max(x => x.Tx.Date) : (DateTime?)null,
            TotalAmount = $"{x.TotalAmount:N} {x.Currency}",
            RemainingAmount = $"{x.RemainingAmount:N} {x.Currency}",
            AvgPrice = x.CostBasis / x.TotalAmount,
            LastPrice = x.Sold ? x.AverageExitPrice : x.CurrentPrice,
            x.CostBasis,
            GainLoss = x.Sold || x.CurrentPrice.HasValue ? x.Proceeds - x.CostBasis + x.CurrentValue : (double?)null,
            ROI = x.Sold || x.CurrentPrice.HasValue ? (x.Proceeds - x.CostBasis + x.CurrentValue) / x.CostBasis : (double?)null,
            x.Annotation
        });

        foreach (var position in outputPositions)
        {
            var style = !position.Sold ? Style.Parse("blue") : Style.Plain;
            table.AddRow(
                new Markup(Markup.Escape(position.Currency), style), // Asset
                new Markup($"{position.Date:d}", style), // Opened Date
                new Markup($"{position.ClosingDate:d}"), // Closing date
                new Markup(Markup.Escape(position.TotalAmount), style), // Total Qty
                new Markup(Markup.Escape(position.RemainingAmount), style), // Remaining Qty
                new Markup($"{position.AvgPrice:C}", style), // Avg Price
                new Markup(position.LastPrice.HasValue ? $"{position.LastPrice:C}" : "???", style), // Market / Close Price
                new Markup($"{position.CostBasis:C}", style), // Cost
                new Markup(position.GainLoss.HasValue ? $"{position.GainLoss:C}" : "???", style), // Gain/Loss
                new Markup(position.ROI.HasValue ? $"{position.ROI:P}" : "???", style), // ROI%
                new Markup(position.Annotation ?? string.Empty) // Annotation
            );
        }

        AnsiConsole.Write(table);

        while (true)
        {
            var selectionPrompt = new SelectionPrompt<string>()
                .Title("What's would you like to do?")
                .AddChoices(
                    "add annotation",
                    "print unmatched spends",
                    "export to CSV",
                    "exit");
            var action = AnsiConsole.Prompt(selectionPrompt);
            if (action == "exit") break;

            switch (action)
            {
                case "add annotation":
                {
                    var date = AnsiConsole.Ask<DateTime>("Enter position date");
                    var positionType = AnsiConsole.Prompt(new SelectionPrompt<PositionType>()
                        .Title("Enter position type")
                        .AddChoices(Enum.GetValues<PositionType>()));
                    var currency = AnsiConsole.Ask<string>("Enter currency");
                    var key = $"{date:yyyy-MM-dd} {positionType} {currency}";

                    var annotation = AnsiConsole.Ask<string>("Enter annotation");
                    var link = AnsiConsole.Prompt(new TextPrompt<string>("Add link?").AllowEmpty());
                    var value = link == string.Empty ? annotation : $"[link={link}]{annotation}[/]";
                    
                    _settingsRepository.AddSetting(new Setting
                    {
                        Type = SettingType.PositionAnnotation,
                        Key = key,
                        Value = value
                    });
                    break;
                }
                case "print unmatched spends":
                {
                    PrintUnmatchedSpends(unmatchedSpends);
                    break;
                }
                case "export to CSV":
                {
                    WritePositionsToCsv(outputPositions, "positions.csv");
                    break;
                }
            }
        }
    }

    private static void WritePositionsToCsv<T>(IEnumerable<T> positions, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
        csv.WriteRecords(positions);
    }

}