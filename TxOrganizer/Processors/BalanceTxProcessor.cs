using System.Text.RegularExpressions;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using TxOrganizer.ConsoleRender;
using TxOrganizer.Database;
using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class BalanceTxProcessor
{
    private readonly IList<(DateTime, double)> _totalQuantityHistory = new List<(DateTime, double)>();
    private readonly AppDbContext? _dbContext;
    
    public BalanceTxProcessor(AppDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }
    
    public async Task AnalyzeBalances(
        IEnumerable<Transaction> transactions,
        BalancesRenderer balancesRenderer)
    {
        foreach (var tx in transactions)
        {
            if (tx.Location == "coinbasepro")
            {
                tx.Location = "coinbase";
            }
        }
        
        var txs = BatchSimilarTransactions(transactions).OrderBy(x => x.Date);
        var balances = new Dictionary<string, LocalBalance>();

        foreach (var tx in txs)
        {
            if (!balances.TryGetValue(tx.Location, out var balance))
            {
                balance = new LocalBalance(tx.Location, balancesRenderer.TargetCurrency);
                balances.Add(tx.Location, balance);
            }
            
            // If this is an ETH transaction with a hash, try to lookup the corresponding ETH balance
            var ethBalanceInfo = await TryLookupEthBalance(tx);
            
            var status = ProcessTransaction(tx, balance, ethBalanceInfo);
            var @continue = balancesRenderer.TraceBalancesAction(tx, status, balance, balances.Values, ethBalanceInfo);
            if (!@continue) break;
            
            var totalQuantity = balances.Sum(x => x.Value.Balance);
            _totalQuantityHistory.Add((tx.Date, totalQuantity));
        }
        
        balancesRenderer.RenderBalances(null, balances.Values);
    }

    private static IEnumerable<Transaction> BatchSimilarTransactions(IEnumerable<Transaction> transactions)
    {
        var batchTxTypes = new[] { TxType.Trade, TxType.Spend };
        return transactions
            .GroupBy(x => (x.Type, x.Location, x.BuyCurrency, x.SellCurrency, x.FeeCurrency, x.Date.Date))
            .SelectMany(txGroup =>
            {
                if (txGroup.Count() == 1 || !batchTxTypes.Contains(txGroup.Key.Type))
                    return txGroup;
                
                var result = new TransactionBatch
                {
                    Date = txGroup.Max(x => x.Date),
                    Type = txGroup.Key.Type,
                    BuyCurrency = txGroup.Key.BuyCurrency,
                    SellCurrency = txGroup.Key.SellCurrency,
                    FeeCurrency = txGroup.Key.FeeCurrency,
                    Location = txGroup.Key.Location,
                    BuyAmount = txGroup.Sum(x => x.BuyAmount),
                    SellAmount = txGroup.Sum(x => x.SellAmount),
                    Fee = txGroup.Sum(x => x.Fee),
                    USDEquivalent = txGroup.Sum(x => x.USDEquivalent)
                };

                return Enumerable.Empty<Transaction>().Append(result);

            });
    }

    private static LocalBalance.ProcessingStatus ProcessTransaction(Transaction tx, LocalBalance balance, EthBalanceInfo? ethBalanceInfo)
    {
        if (tx.BuyCurrency != balance.Currency && tx.SellCurrency != balance.Currency && tx.FeeCurrency != balance.Currency)
            return LocalBalance.ProcessingStatus.NotRelevant;

        if (tx.BuyCurrency == balance.Currency || tx.SellCurrency == balance.Currency)
        {
            double balanceDiff;
            switch (tx.Type)
            {
                case TxType.Trade:
                case TxType.Migration:
                    balanceDiff = tx.BuyCurrency == balance.Currency ? tx.BuyAmount : -tx.SellAmount;
                    balance.UpdateBought(tx.BuyCurrency == balance.Currency ? tx.BuyAmount : 0);
                    balance.UpdateSold(tx.SellCurrency == balance.Currency ? tx.SellAmount : 0);
                    break;

                case TxType.Deposit:
                case TxType.Income:
                case TxType.Airdrop:
                case TxType.Borrow:
                    balanceDiff = tx.BuyAmount;
                    balance.UpdateDeposited(tx.BuyAmount);
                    break;

                case TxType.Withdrawal:
                case TxType.Spend:
                case TxType.Gift:
                case TxType.Lost:
                case TxType.Stolen:
                case TxType.Repay:
                    balanceDiff = -tx.SellAmount;
                    balance.UpdateWithdrawn(tx.SellAmount);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            balance.UpdateBalance(balanceDiff);
        }

        if (tx.FeeCurrency == balance.Currency)
        {
            if (IncludeFees(tx)) balance.UpdateBalance(-tx.Fee);
            balance.UpdateFees(tx.Fee);
        }

        balance.AddTransaction(tx);

        var margin = 1e-5;
        var status = balance.Balance >= -margin ?
            LocalBalance.ProcessingStatus.Processed :
            LocalBalance.ProcessingStatus.NegativeBalance;

        if (ethBalanceInfo == null)
        {
            return status;
        }

        var divergence = Math.Abs((double)ethBalanceInfo.BalanceEth - balance.Balance);
        var addressComponent = balance.Location.Contains('_')
            ? balance.Location.Split('_').Last()
            : null;

        if (addressComponent is null || !ethBalanceInfo.Address.ToLower().StartsWith(addressComponent))
        {
            return status;
        }
        
        // Update last divergence when comparing with actual ETH balance
        balance.UpdateLastDivergence((double)ethBalanceInfo.BalanceEth);

        if (divergence > 0.1)
        {
            status = LocalBalance.ProcessingStatus.Diverged;
        }

        return status;
    }

    private static bool IncludeFees(Transaction tx)
    {
        return tx switch
        {
            { Location: "Poloniex" } => tx.Type != TxType.Withdrawal,
            { Location: "Kraken"} => false,
            { Location: "kraken"} => false,
            { Location: "Gemini"} => false,
            { Location: "cex.io", FeeCurrency: "BTC"} => false,
            { Location: "Jaxx"} => false,
            { Location: "Coinbase wallet"} => false,
            { Location: "Ledger" } => false,
            { Location: "Trezor" } => false,
            { Location: "coinbase"} => false,
            // { Location: "localbitcoins"} => false,
            _ => true
        };
    }

    private async Task<EthBalanceInfo?> TryLookupEthBalance(Transaction tx)
    {
        // Only lookup ETH balances if we have a database context and the transaction has a hash
        if (_dbContext == null || string.IsNullOrEmpty(tx.TxHash))
            return null;
            
        // Only lookup for ETH-related transactions
        if (tx.BuyCurrency != "ETH" && tx.SellCurrency != "ETH")
            return null;
            
        try
        {
            var ethBalance = await _dbContext.EthHistoricalBalances
                .Where(b => b.TransactionHash == tx.TxHash)
                .FirstOrDefaultAsync();
                
            if (ethBalance != null)
            {
                // Return the structured ETH balance information
                return new EthBalanceInfo
                {
                    BalanceEth = ethBalance.BalanceEth,
                    BlockNumber = ethBalance.BlockNumber,
                    TransactionHash = ethBalance.TransactionHash,
                    BlockTimestamp = ethBalance.BlockTimestamp,
                    Address = ethBalance.Address
                };
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the transaction processing
            Console.WriteLine($"Warning: Could not lookup ETH balance for transaction {tx.TxHash}: {ex.Message}");
        }
        
        return null;
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
}