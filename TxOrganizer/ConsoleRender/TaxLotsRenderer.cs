using Spectre.Console;
using TxOrganizer.DTO;

namespace TxOrganizer.ConsoleRender;

public class TaxLotsRenderer: TransactionRenderer
{
    private readonly bool _trace;

    private readonly DateTime? _startDate;

    public TaxLotsRenderer(DateTime? startDate)
    {
        _startDate = startDate;
        var response1 = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Do you want to trace balances or see the final snapshot?")
                .AddChoices("Trace", "Snapshot"));
        _trace = string.Equals(response1, "Trace");
    }

    public bool Trace => _trace;

    public void TraceTaxLotsAction(Transaction tx, double remaining, double sold, TaxLot currentLot,
        IEnumerable<TaxLot> taxLots)
    {
        if (!Trace) return;
        if (tx.BuyCurrency != TargetCurrency && tx.SellCurrency != TargetCurrency) return;
        if (currentLot.Currency != TargetCurrency) return;
        if (_startDate.HasValue && tx.Date < _startDate) return;
        
        AnsiConsole.Clear();
        RenderTaxLots(currentLot, taxLots);
        RenderTx(tx, remaining, sold);
        
        if (!string.IsNullOrEmpty(LocationFilter))
        {
            var locationMatch = tx.Location?.Contains(LocationFilter) ?? false;
            if (!locationMatch) return;
        }

        var input = AnsiConsole.Confirm("Continue");

        if (!input)
        {
            throw new Exception();
        }
    }
    
    public void RenderTaxLots(TaxLot? currentLot, IEnumerable<TaxLot> allLots)
    {
        var outstanding = CalculateOutstandingAmount(allLots);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("DateTime");
        table.AddColumn("Initial");
        table.AddColumn("Remaining", column => { column.Footer($"{outstanding} {TargetCurrency}"); });
        table.AddColumn("Price");
        table.AddColumn("Location");

        foreach (var taxLot in allLots.Where(x => x.Currency == TargetCurrency && (!x.Sold || x.Equals(currentLot))))
        {
            var current = taxLot.Equals(currentLot);
            var style = current ? Style.Parse("blue") : Style.Plain;
            table.AddRow(
                new Markup(taxLot.Date.ToString(), style),
                new Markup($"{taxLot.InitialAmount} {taxLot.Currency}", style),
                new Markup($"{taxLot.RemainingAmount} {taxLot.Currency}", style),
                new Markup(taxLot.InitialPrice.ToString(), style),
                new Markup(taxLot.Location, style));
        }

        AnsiConsole.Write(table);
    }
    
    private double CalculateOutstandingAmount(IEnumerable<TaxLot> allLots)
    {
        var assetSlots = allLots.Where(x => x.Currency == TargetCurrency && !x.Sold);
        return assetSlots.Sum(x => x.RemainingAmount);
    }
}