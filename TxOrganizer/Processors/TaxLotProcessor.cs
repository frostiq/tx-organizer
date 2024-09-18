using TxOrganizer.ConsoleRender;
using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class TaxLotProcessor
{
    public (List<TaxLot> taxLots, List<TxSpend> unmatchedSpends) BuildTaxLots(
        IEnumerable<Transaction> transactions, 
        TaxLotsRenderer taxLotsRenderer)
    {
        var taxLots = new List<TaxLot>();
        var unmatchedSpends = new List<TxSpend>();
        var txTypes = TaxLot.SupportedBuyTxTypes.Concat(new[] { TxType.Spend, TxType.Lost, TxType.Gift, TxType.Stolen });
        var selectedTransactions = transactions
            .Where(x => txTypes.Contains(x.Type))
            .OrderBy(x => x.Date);
        foreach (var tx in selectedTransactions)
        {
            var remaining = tx.SellAmount;
            var currency = tx.SellCurrency;
            if (new[] { TxType.Trade, TxType.Airdrop, TxType.Income}.Contains(tx.Type))
            {
                var newLot = new TaxLot(tx);
                taxLots.Add(newLot);
                taxLotsRenderer.TraceTaxLotsAction(tx, 0, 0, newLot, taxLots);
            }

            if (tx.SellCurrency == "USD") continue;
            while (remaining > 0)
            {
                var taxLot = FindHiFoTaxLot(currency, taxLots);
                if (taxLot is null)
                {
                    unmatchedSpends.Add(new TxSpend(tx, remaining));
                    break;
                }

                (remaining, var sold) = taxLot.Sell(tx, remaining);
                
                taxLotsRenderer.TraceTaxLotsAction(tx, remaining, sold, taxLot, taxLots);
            }
        }

        return (taxLots, unmatchedSpends);
    }

    private static TaxLot? FindHiFoTaxLot(string? currency, List<TaxLot> taxLots)
    {
        return taxLots
            .Where(x => x.Currency == currency && !x.Sold)
            .MaxBy(x => x.InitialPrice);
    }
}