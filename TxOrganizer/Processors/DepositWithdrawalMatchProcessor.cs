using TxOrganizer.DTO;

namespace TxOrganizer.Processors;

public class DepositWithdrawalMatchProcessor
{
    private class TxPair
    {
        public TxPair(Transaction withdrawal)
        {
            Withdrawal = withdrawal;
        }

        public Transaction Withdrawal { get; }

        public Transaction? Deposit { get; set; }
    }

    public async Task<(IEnumerable<Transaction> unmatchedDeposits, IEnumerable<Transaction> unmatchedWithdrawals)>
        AnalyzeDepositWithdrawals(ICollection<Transaction> transactions, string targetCurrency)
    {
        var unmatchedDeposits = new List<Transaction>();

        var txPairs = transactions.Where(x => x.Type is TxType.Withdrawal && x.SellCurrency == targetCurrency)
            .Select(tx => new TxPair(tx))
            .ToList();

        foreach (var tx in transactions.Where(x => x.Type is TxType.Deposit && x.BuyCurrency == targetCurrency && x.BuyAmount > 0))
        {
            var pair = txPairs.FirstOrDefault(x =>
                x.Withdrawal.SellCurrency == tx.BuyCurrency
                && x.Deposit is null
                && Math.Abs(x.Withdrawal.SellAmount - tx.BuyAmount) / tx.BuyAmount < 0.01
                && (x.Withdrawal.Date - tx.Date).Duration() < TimeSpan.FromDays(1));

            if (pair is not null)
            {
                pair.Deposit = tx;
            }
            else
            {
                unmatchedDeposits.Add(tx);
            }
        }

        var matched = txPairs.Count(x => x.Deposit is not null);
        var unmatchedWithdrawals = txPairs.Where(x => x.Deposit is null).Select(x => x.Withdrawal).ToList();
        Console.WriteLine($"Matched {matched} deposits with withdrawals.");
        Console.WriteLine($"Unmatched deposits: {unmatchedDeposits.Count}");
        Console.WriteLine($"Unmatched withdrawals: {unmatchedWithdrawals.Count}");
        return (
            unmatchedDeposits,
            unmatchedWithdrawals
        );
    }
}