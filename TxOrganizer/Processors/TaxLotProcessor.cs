using CsvHelper;
using Spectre.Console;
using TxOrganizer.ConsoleRender;
using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class TaxLotProcessor
{
    private readonly TxType[] _buyTxTypes = { TxType.Trade, TxType.Migration, TxType.Airdrop, TxType.Income };
    private readonly TxType[] _sellTxTypes = { TxType.Trade, TxType.Migration, TxType.Spend, TxType.Lost, TxType.Gift, TxType.Stolen };
    
    private readonly IList<(DateTime, double)> _totalQuantityHistory = new List<(DateTime, double)>();
    
    public void BuildTaxLots(IEnumerable<Transaction> transactions, TaxLotsRenderer taxLotsRenderer, ProgressTask task, string? targetCurrency)
    {
        var taxLots = new Dictionary<string, List<TaxLot>>();
        var unmatchedSpends = new List<TxSpend>();

        var selectedTransactions = transactions
            .Where(x => _buyTxTypes.Contains(x.Type) || _sellTxTypes.Contains(x.Type))
            .OrderBy(x => x.Date).ToList();
        
        foreach (var tx in selectedTransactions)
        {
            var remaining = tx.SellAmount;
            if ((targetCurrency == null || targetCurrency == tx.BuyCurrency) && _buyTxTypes.Contains(tx.Type))
            {
                var newLot = new TaxLot(tx);
                if (!taxLots.TryGetValue(tx.BuyCurrency, out var taxLotsList))
                {
                    taxLotsList = new List<TaxLot>();
                    taxLots[tx.BuyCurrency] = taxLotsList;
                }
                taxLotsList.Add(newLot);
                
                var @continue = taxLotsRenderer.TraceTaxLotsAction(tx, 0, 0, newLot, taxLots.SelectMany(x => x.Value));
                if (!@continue) break;
            }

            if ((targetCurrency == null || tx.SellCurrency == targetCurrency) && tx.SellCurrency != "USD" && _sellTxTypes.Contains(tx.Type))
            {
                while (remaining > 0)
                {
                    var taxLot = FindHiFoTaxLot(tx.SellCurrency, taxLots);
                    if (taxLot is null)
                    {
                        unmatchedSpends.Add(new TxSpend(tx, remaining));
                        break;
                    }

                    (remaining, var sold) = taxLot.Sell(tx, remaining);

                    taxLotsRenderer.TraceTaxLotsAction(tx, remaining, sold, taxLot, taxLots.SelectMany(x => x.Value));
                }
            }

            task?.Increment(1.0 / selectedTransactions.Count);
            var totalQuantity = taxLots.Values.Sum(x => x.Sum(y => y.RemainingAmount));
            _totalQuantityHistory.Add((tx.Date, totalQuantity));
        }
        
        taxLotsRenderer.RenderTaxLots(null, taxLots.SelectMany(x => x.Value));
    }
    
    public void WriteTotalQuantityHistoryToCsv(string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
        csv.WriteRecords(_totalQuantityHistory.Select(x => new
        {
            Time = x.Item1,
            Quantity = x.Item2
        }));
    }

    private static TaxLot? FindHiFoTaxLot(string currency, Dictionary<string, List<TaxLot>> taxLots)
    {
        if (taxLots.TryGetValue(currency, out var taxLotsList))
        {
            return taxLotsList
                .Where(x => !x.Sold)
                .MaxBy(x => x.InitialPrice);
        }

        return null;
    }
}