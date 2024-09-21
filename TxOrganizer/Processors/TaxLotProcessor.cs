using Spectre.Console;
using TxOrganizer.ConsoleRender;
using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class TaxLotProcessor
{
    private readonly TxType[] _buyTxTypes = { TxType.Trade, TxType.Migration, TxType.Airdrop, TxType.Income };
    private readonly TxType[] _sellTxTypes = { TxType.Trade, TxType.Migration, TxType.Spend, TxType.Lost, TxType.Gift, TxType.Stolen };
    
    public (List<TaxLot> taxLots, List<TxSpend> unmatchedSpends) BuildTaxLots(
        IEnumerable<Transaction> transactions,
        TaxLotsRenderer taxLotsRenderer, ProgressTask task)
    {
        var taxLots = new List<TaxLot>();
        var unmatchedSpends = new List<TxSpend>();

        var selectedTransactions = transactions
            .Where(x => _buyTxTypes.Contains(x.Type) || _sellTxTypes.Contains(x.Type))
            .OrderBy(x => x.Date).ToList();
        
        foreach (var tx in selectedTransactions)
        {
            var remaining = tx.SellAmount;
            var currency = tx.SellCurrency;
            if (_buyTxTypes.Contains(tx.Type))
            {
                var newLot = new TaxLot(tx);
                taxLots.Add(newLot);
                taxLotsRenderer.TraceTaxLotsAction(tx, 0, 0, newLot, taxLots);
            }

            if (tx.SellCurrency != "USD" && _sellTxTypes.Contains(tx.Type))
            {
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

            task?.Increment(1.0 / selectedTransactions.Count);
        }
        
        taxLotsRenderer.RenderTaxLots(null, taxLots);

        return (taxLots, unmatchedSpends);
    }

    private static TaxLot? FindHiFoTaxLot(string? currency, List<TaxLot> taxLots)
    {
        return taxLots
            .Where(x => x.Currency == currency && !x.Sold)
            .MaxBy(x => x.InitialPrice);
    }
}