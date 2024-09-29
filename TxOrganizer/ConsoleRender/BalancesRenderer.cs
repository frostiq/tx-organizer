using Spectre.Console;
using TxOrganizer.DTO;

namespace TxOrganizer.ConsoleRender;

public class BalancesRenderer : TransactionRenderer
{
    private readonly bool _onlyCritical;

    private readonly bool _trace;

    private readonly DateTime? _startDate;

    public BalancesRenderer(DateTime? startDate)
    {
        this._startDate = startDate;
        var response1 = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Do you want to trace balances or see the final snapshot?")
                .AddChoices("Snapshot", "Trace"));
        _trace = string.Equals(response1, "Trace");
        
        if (!_trace) return;
        
        
        var response2 = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Do you want to display only critical transactions?")
            .AddChoices("All transactions", "Critical transactions"));
        _onlyCritical = string.Equals(response2, "Critical transactions");
    }

    public bool TraceBalancesAction(Transaction tx, LocalBalance.ProcessingStatus status, LocalBalance current,
        IEnumerable<LocalBalance> balances)
    {
        if (!_trace || status == LocalBalance.ProcessingStatus.NotRelevant) return true;

        if (!string.IsNullOrEmpty(LocationFilter))
        {
            var exactTokens = LocationFilter.Split(',');

            if (exactTokens.Length > 1)
            {
                var locationMatch = exactTokens.Contains(tx.Location);
                if (!locationMatch) return true;
            }
            else
            {
                var locationMatch = string.Equals(tx.Location, LocationFilter);
                if (!locationMatch) return true;
            }
        }

        if (_onlyCritical && status == LocalBalance.ProcessingStatus.Processed) return true;
        if (_startDate.HasValue && tx.Date < _startDate) return true;

        AnsiConsole.Clear();
        RenderBalances(current, balances);
        RenderTx(tx, 0, 0);

        if (status == LocalBalance.ProcessingStatus.NegativeBalance)
        {
            AnsiConsole.MarkupLine($"[red]Negative balance![/]");
        }

        Console.WriteLine();
        var @continue = AnsiConsole.Confirm("Continue");
        return @continue;
    }

    public void RenderBalances(LocalBalance? currentBalance, IEnumerable<LocalBalance> allBalances)
    {
        var table = new Table();
        var total = allBalances.Sum(x => x.Balance);
        table.Border(TableBorder.Rounded);
        table.AddColumn("Location");
        table.AddColumn("Balance", column => { column.Footer($"{total} {TargetCurrency}"); });
        table.AddColumn("Deposited");
        table.AddColumn("Bought");
        table.AddColumn("Withdrawn");
        table.AddColumn("Sold");
        table.AddColumn("Fees");

        allBalances = allBalances
            .Where(x => x.Currency == TargetCurrency && (x.Balance != 0 || x.Equals(currentBalance)))
            .OrderBy(x => x.Balance);

        foreach (var balance in allBalances)
        {
            var current = balance.Equals(currentBalance);
            var style = current
                ? Style.Parse("blue")
                : balance.Balance switch
                {
                    > 1e-4 => Style.Parse("green"),
                    < -1e-4 => Style.Parse("red"),
                    _ => Style.Plain
                };
            table.AddRow(
                new Markup(balance.Location, style),
                new Markup($"{balance.Balance} {balance.Currency}", style),
                new Markup($"{balance.Deposited} {balance.Currency}", style),
                new Markup($"{balance.Bought} {balance.Currency}", style),
                new Markup($"{balance.Withdrawn} {balance.Currency}", style),
                new Markup($"{balance.Sold} {balance.Currency}", style),
                new Markup($"{balance.Fees} {balance.Currency}", style)
            );
        }

        AnsiConsole.Write(table);
    }
}