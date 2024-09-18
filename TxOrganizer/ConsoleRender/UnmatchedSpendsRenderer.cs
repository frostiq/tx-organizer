using Spectre.Console;
using TxOrganizer.DTO;

namespace TxOrganizer.ConsoleRender;

public class UnmatchedSpendsRenderer
{
    protected static void PrintUnmatchedSpends(IEnumerable<TxSpend> unmatchedSpends)
    {
        var showUnmatchedSpends = AnsiConsole.Confirm("Show unmatched spends?", false);
        if (showUnmatchedSpends)
        {
            AnsiConsole.WriteLine();

            var root = new Tree("Unmatched spends");

            var valuedUnmatchedSpends = unmatchedSpends.GroupBy(x => x.Tx.SellCurrency)
                .Select(group => (group,
                    usdValue: group.Sum(spend => spend.Amount * spend.Tx.USDEquivalent / spend.Tx.SellAmount)));
            foreach (var (unmatchedSpendGroup, usdValue) in valuedUnmatchedSpends.OrderByDescending(x => x.Item2))
            {
                var earliestDate = unmatchedSpendGroup.Min(x => x.Tx.Date);
                var latestDate = unmatchedSpendGroup.Max(x => x.Tx.Date);
                var group =
                    $" {unmatchedSpendGroup.Sum(x => x.Amount)} {unmatchedSpendGroup.Key} ({usdValue:C}) from {earliestDate} to {latestDate}";
                var groupNode = root.AddNode(Markup.Escape(group));
                foreach (var unmatchedSpend in unmatchedSpendGroup)
                {
                    groupNode.AddNode(Markup.Escape(unmatchedSpend.ToString()));
                }
            }

            AnsiConsole.Write(root);
        }
    }
}