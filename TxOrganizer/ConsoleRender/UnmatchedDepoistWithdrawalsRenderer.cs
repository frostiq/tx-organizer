using Spectre.Console;
using Spectre.Console.Rendering;
using TxOrganizer.DTO;

namespace TxOrganizer.ConsoleRender;

public class UnmatchedDepoistWithdrawalsRenderer
{
    // Render unmatched deposits and withdrawals
    public void RenderUnmatchedDepositsAndWithdrawals(
        IEnumerable<Transaction> unmatchedDeposits, 
        IEnumerable<Transaction> unmatchedWithdrawals)
    {
        var table = new Table();
        table.AddColumn("Deposits", column => { column.Footer(unmatchedDeposits.Sum(x => x.BuyAmount).ToString());  });
        table.AddColumn("Withdrawals", column => { column.Footer(unmatchedWithdrawals.Sum(x => x.SellAmount).ToString());  });

        var depositsByYear = unmatchedDeposits.GroupBy(x => x.Date.Year);
        var withdrawalsByYear = unmatchedWithdrawals.GroupBy(x => x.Date.Year);

        foreach (var year in Enumerable.Range(2010, DateTime.Now.Year))
        {
            var yearDeposits = depositsByYear.FirstOrDefault(x => x.Key == year);
            var yearWithdrawals = withdrawalsByYear.FirstOrDefault(x => x.Key == year);

            if (yearDeposits is not null || yearWithdrawals is not null)
            {
                Renderable depositsPanel = yearDeposits is not null
                    ? new Panel(string.Join("\n", yearDeposits.Select(x => x.ToString())))
                    {
                        Header = new PanelHeader(year.ToString())
                    }
                    : new Markup(string.Empty);

                Renderable withdrawalsPanel = yearWithdrawals is not null
                    ? new Panel(string.Join("\n", yearWithdrawals.Select(x => x.ToString())))
                    {
                        Header = new PanelHeader(year.ToString())
                    }
                    : new Markup(string.Empty);

                table.AddRow(depositsPanel, withdrawalsPanel);
            }
        }

        AnsiConsole.Write(table);
    }
}