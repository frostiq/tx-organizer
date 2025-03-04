using System.Text.RegularExpressions;
using TxOrganizer.Database;
using TxOrganizer.DataSource;
using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class PositionProcessor
{
    private readonly CoinGeckoPriceFetcher _coinGeckoPriceFetcher;
    private readonly SettingsRepository _settingsRepository;
    private static readonly Regex NftCurrencyRegex = new Regex(@"^(.+)-\d", RegexOptions.Compiled);

    private readonly TxType[] _buyTxTypes = { TxType.Trade, TxType.Migration, TxType.Airdrop, TxType.Income, TxType.Borrow };

    private readonly TxType[] _sellTxTypes =
        { TxType.Trade, TxType.Migration, TxType.Spend, TxType.Lost, TxType.Gift, TxType.Stolen, TxType.Repay };

    public PositionProcessor(CoinGeckoPriceFetcher coinGeckoPriceFetcher, SettingsRepository settingsRepository)
    {
        _coinGeckoPriceFetcher = coinGeckoPriceFetcher;
        _settingsRepository = settingsRepository;
    }

    public async Task<(IEnumerable<Position>, IEnumerable<TxSpend>)> BuildPositions(
        IEnumerable<Transaction> transactions, Action progressCallback)
    {
        var txGroups = transactions
            // Filter out perp transactions
            .GroupBy(x => x is not { BuyCurrency: "USDTPROFIT" } &&
                          x is not { SellCurrency: "USDTPROFIT" } &&
                          x is not { Type: TxType.Trade, SellCurrency: "USDT", BuyAmount: 0, BuyCurrency: "USD" });

        var regularTransactions = txGroups.First(x => x.Key).OrderBy(x => x.Date).ToList();
        var perpTransactions = txGroups.First(x => !x.Key).OrderBy(x => x.Date).ToList();
        
        var (positions, unmatchedSpends) = ProcessRegularTransaction(regularTransactions, progressCallback);
        var perpPositions = ProcessPerpTransaction(perpTransactions, progressCallback);
        positions.AddRange(perpPositions);
        
        // Arb positions post-processing
        var arbCurrencies = _settingsRepository.GetSettings(SettingType.ArbPositionCurrency).Select(x => x.Key);
        foreach (var targetPosition in positions.Where(x => arbCurrencies.Contains(x.Currency)).ToList())
        {
            var (arbPosition, deficit) = ExtractArbitragePosition(targetPosition);
            if (arbPosition is not null)
            {
                positions.Add(arbPosition);
            }
        }
        
        // Price updates and annotation
        var tokenSymbolsToFetch = positions.Where(x => !x.Sold).Select(x => x.Currency).Distinct().ToList();
        var currencyRates = await _coinGeckoPriceFetcher.GetPricesAsync(tokenSymbolsToFetch);
        var annotations = _settingsRepository.GetSettings(SettingType.PositionAnnotation);

        foreach (var position in positions)
        {
            if (currencyRates.TryGetValue(position.Currency, out var price))
            {
                position.CurrentPrice = price;
            }
            
            var annotation = annotations.FirstOrDefault(x => x.Key == $"{position.Date:yyyy-MM-dd} {Enum.GetName(position.PositionType)} {position.Currency}");
            if (annotation is not null)
            {
                position.Annotation = annotation.Value;
            }
        }

        return (positions, unmatchedSpends);
    }

    private (List<Position>, List<TxSpend>) ProcessRegularTransaction(List<Transaction> transactions, Action progressCallback)
    {
        var positions = new List<Position>();
        var unmatchedSpends = new List<TxSpend>();
        foreach (var tx in transactions)
        {
            tx.BuyCurrency = MapCurrency(tx.BuyCurrency);
            tx.SellCurrency = MapCurrency(tx.SellCurrency);

            if (_buyTxTypes.Contains(tx.Type) && tx.BuyCurrency != "USD")
            {
                var currentPosition = positions.SingleOrDefault(x => x.Currency == tx.BuyCurrency && !x.Sold);

                if (currentPosition is null)
                {
                    var positionType = tx.Type is TxType.Borrow ? PositionType.Loan : PositionType.Investment;
                    currentPosition = new Position(tx, positionType);
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

            progressCallback();
        }

        return (positions, unmatchedSpends);
    }
    
    private IEnumerable<Position> ProcessPerpTransaction(List<Transaction> transactions, Action progressCallback)
    {
        var positions = new List<Position>();
        foreach (var tx in transactions)
        {
            if (tx.Type != TxType.Trade)
            {
                throw new ApplicationException("Unsupported transaction type for perp trading: " + tx.Type);
            }
            
            var currentPosition = positions.SingleOrDefault(x => (tx.Date - x.Date).Duration() < TimeSpan.FromDays(1));
            
            if (currentPosition is null)
            {
                currentPosition = new Position(new Transaction
                    {
                        Date = tx.Date,
                        Location = tx.Location,
                        BuyCurrency = "USDT",
                        BuyAmount = 0,
                    }, PositionType.Perpetuals);
                
                positions.Add(currentPosition);
            }
           
            if (tx is { SellCurrency: "USDTPROFIT" })
            {
                currentPosition.Buy(new Transaction
                {
                    Date = tx.Date,
                    Location = tx.Location,
                    BuyCurrency = "USDT",
                    BuyAmount = tx.BuyAmount,
                    USDEquivalent = 0,
                });
                currentPosition.Sell(new Transaction
                {
                    Date = tx.Date,
                    Location = tx.Location,
                    SellCurrency = "USDT",
                    SellAmount = tx.BuyAmount,
                    USDEquivalent = tx.BuyAmount,
                }, tx.BuyAmount);
            }
            else if (tx is {SellCurrency: "USDT", BuyAmount: 0, BuyCurrency: "USD"})
            {
                currentPosition.Buy(new Transaction
                {
                    Date = tx.Date,
                    Location = tx.Location,
                    BuyCurrency = "USDT",
                    BuyAmount = tx.SellAmount,
                    USDEquivalent = tx.SellAmount,
                });

                if (tx.Comment?.Contains("Fee") ?? false)
                {
                    currentPosition.SpendFee(new Transaction
                    {
                        Date = tx.Date,
                        Location = tx.Location,
                        FeeCurrency = "USDT",
                        Fee = tx.SellAmount,
                        USDEquivalent = 0,
                    }, tx.SellAmount);
                }
                else
                {
                    currentPosition.Sell(new Transaction
                    {
                        Date = tx.Date,
                        Location = tx.Location,
                        SellCurrency = "USDT",
                        SellAmount = tx.SellAmount,
                        USDEquivalent = 0,
                    }, tx.SellAmount);
                }
            }
            else if (tx is { SellAmount: 0, SellCurrency: "USD", BuyCurrency: "USDTPROFIT" })
            {
                
            }
            else
            {
                throw new ApplicationException("Unsupported perp trading transaction: " + tx);
            }

            if (tx.Fee > 0)
            {
                throw new ApplicationException("Fee in perp trading is not supported: " + tx);
            }

            progressCallback();
        }

        return positions;
    }

    public (Position? arbPosition, double deficit) ExtractArbitragePosition(Position position)
    {
        var sellTransactions = position.TxSpends.Select(x => x.Tx).OrderBy(x => x.Date).ToList();
        var lostTransactions = sellTransactions.Where(x => x.Type == TxType.Lost).ToList();

        // Merge Lost and Trade transactions
        foreach (var lostTx in lostTransactions)
        {
            var tradeTx = sellTransactions.FirstOrDefault(x =>
                x.Type == TxType.Trade &&
                x.Date == lostTx.Date &&
                x.SellCurrency == lostTx.SellCurrency);

            if (tradeTx is not null)
            {
                sellTransactions.Remove(lostTx);
                position.RemoveTxSpends(lostTx);
                tradeTx.SellAmount += lostTx.SellAmount;
            }
        }

        Position? arbPosition = null;
        var totalDeficit = 0.0;
        // Identify arbitrage trades, when roughly same amount bought and sold within a day
        foreach (var sellTx in sellTransactions)
        {
            var buyTx = position.BuyTransactions.Where(x =>
                Math.Abs(x.BuyAmount - sellTx.SellAmount) / sellTx.SellAmount < 0.03 &&
                (x.Date - sellTx.Date).Duration() < TimeSpan.FromDays(1))
                .MinBy(x => (x.Date - sellTx.Date).Duration());

            if (buyTx is null) continue;

            if (arbPosition is null)
            {
                arbPosition = new Position(buyTx, PositionType.Arbitrage);
            }
            else
            {
                arbPosition.Buy(buyTx);
            }

            var (remainingSell, _) = arbPosition.Sell(sellTx, sellTx.SellAmount);
            totalDeficit += remainingSell;

            position.RemoveBuyTransaction(buyTx);
            position.RemoveTxSpends(sellTx);
        }

        return (arbPosition, totalDeficit);
    }

    private static string MapCurrency(string currency)
    {
        // if match the regex replace with the first group
        var match = NftCurrencyRegex.Match(currency);
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