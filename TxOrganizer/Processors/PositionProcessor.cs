using System.Text.RegularExpressions;
using TxOrganizer.DataSource;
using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class PositionProcessor
{
    private readonly CoinGeckoPriceFetcher _coinGeckoPriceFetcher;
    private static readonly Regex CurrencyRegex = new Regex(@"^(.+)-\d", RegexOptions.Compiled);
    
    private readonly TxType[] _buyTxTypes = { TxType.Trade, TxType.Migration, TxType.Airdrop, TxType.Income };
    private readonly TxType[] _sellTxTypes = { TxType.Trade, TxType.Migration, TxType.Spend, TxType.Lost, TxType.Gift, TxType.Stolen };

    public PositionProcessor(CoinGeckoPriceFetcher coinGeckoPriceFetcher)
    {
        _coinGeckoPriceFetcher = coinGeckoPriceFetcher;
    }

    public async Task<(IEnumerable<Position>, IEnumerable<TxSpend>)> BuildPositions(
        IEnumerable<Transaction> transactions)
    {
        var positions = new List<Position>();
        var unmatchedSpends = new List<TxSpend>();
        var selectedTransactions = transactions
            .Where(x => x.BuyCurrency != "USDTPROFIT" && x.SellCurrency != "USDTPROFIT")
            .OrderBy(x => x.Date);

        foreach (var tx in selectedTransactions)
        {
            tx.BuyCurrency = MapCurrency(tx.BuyCurrency);
            tx.SellCurrency = MapCurrency(tx.SellCurrency);

            if (_buyTxTypes.Contains(tx.Type) && tx.BuyCurrency != "USD")
            {
                var currentPosition = positions.SingleOrDefault(x => x.Currency == tx.BuyCurrency && !x.Sold);

                if (currentPosition is null)
                {
                    currentPosition = new Position(tx);
                    positions.Add(currentPosition);
                }
                else
                {
                    currentPosition.Buy(tx);
                }
            }
            
            var remainingSell = tx.SellAmount;
            if (_sellTxTypes.Contains(tx.Type) && tx.SellCurrency != "USD")
            {
                while (remainingSell > 0)
                {
                    var position = FindHiFoPosition(tx.SellCurrency, positions);
                    if (position is null)
                    {
                        unmatchedSpends.Add(new TxSpend(tx, remainingSell));
                        break;
                    }

                    (remainingSell, var sold) = position.Sell(tx, remainingSell);
                }
            }

            var remainingFee = tx.Fee;
            if (tx.FeeCurrency != "USD")
            {
                while (remainingFee > 0)
                {
                    var position = FindHiFoPosition(tx.FeeCurrency, positions);
                    if (position is null)
                    {
                        break;
                    }

                    (remainingFee, var sold) = position.SpendFee(tx, remainingFee);
                }
            }
        }

        var tokenSymbolsToFetch = positions.Where(x => !x.Sold).Select(x => x.Currency).Distinct().ToList();
        var currencyRates = await _coinGeckoPriceFetcher.GetPricesAsync(tokenSymbolsToFetch);

        foreach (var position in positions)
        {
            if (currencyRates.TryGetValue(position.Currency, out var price))
            {
                position.CurrentPrice = price;
            }
        }

        return (positions, unmatchedSpends);
    }

    private static string MapCurrency(string currency)
    {
        // if match the regex replace with the first group
        var match = CurrencyRegex.Match(currency);
        if (match.Success)
        {
            return match.Groups[1].Value + " [NFT]";
        }

        return currency;
    }

    private static Position? FindHiFoPosition(string? currency, IEnumerable<Position> positions)
    {
        return positions
            .Where(x => x.Currency == currency && !x.Sold)
            .MaxBy(x => x.InitialPrice);
    }
}