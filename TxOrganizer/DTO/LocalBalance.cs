namespace TxOrganizer.DTO;

public class LocalBalance
{
    public enum ProcessingStatus
    {
        Processed,
        NotRelevant,
        NegativeBalance
    }

    private List<Transaction> _transactions;

    public string Location { get; private set; }

    public string Currency { get; private set; }

    public double Balance { get; private set; }

    public double Deposited { get; set; }

    public double Withdrawn { get; set; }
    
    public double Bought { get; set; }

    public double Sold { get; set; }

    public double Fees { get; set; }

    public LocalBalance(string? location, string currency)
    {
        Location = location;
        Currency = currency;
        _transactions = new List<Transaction>();
    }

    public ProcessingStatus Process(Transaction tx)
    {
        if (tx.BuyCurrency != Currency && tx.SellCurrency != Currency && tx.FeeCurrency != Currency)
            return ProcessingStatus.NotRelevant;

        if (tx.BuyCurrency == Currency || tx.SellCurrency == Currency)
        {
            double balanceDiff;
            switch (tx.Type)
            {
                case TxType.Trade:
                case TxType.Migration:
                    balanceDiff = tx.BuyCurrency == Currency ? tx.BuyAmount : - tx.SellAmount;
                    Bought += tx.BuyCurrency == Currency ? tx.BuyAmount : 0;
                    Sold += tx.SellCurrency == Currency ? tx.SellAmount : 0;
                    break;
                
                case TxType.Deposit:
                case TxType.Income:
                case TxType.Airdrop:
                case TxType.Borrow:
                    balanceDiff = tx.BuyAmount;
                    Deposited += tx.BuyAmount;
                    break;
                
                case TxType.Withdrawal:
                case TxType.Spend:
                case TxType.Gift:
                case TxType.Lost:
                case TxType.Stolen:
                case TxType.Repay:
                    balanceDiff = - tx.SellAmount;
                    Withdrawn += tx.SellAmount;
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Balance += balanceDiff;
        }

        if (tx.FeeCurrency == Currency && IncludeFees(tx))
        {
            Balance -= tx.Fee;
            Fees += tx.Fee;
        }

        _transactions.Add(tx);

        return Balance >= -1e-5 ? ProcessingStatus.Processed : ProcessingStatus.NegativeBalance;
    }

    private static bool IncludeFees(Transaction tx)
    {
        return tx switch
        {
            { Location: "Poloniex" } => tx.Type != TxType.Withdrawal,
            { Location: "Kraken"} => false,
            { Location: "Gemini"} => false,
            { Location: "cex.io", FeeCurrency: "BTC"} => false,
            { Location: "Jaxx"} => false,
            { Location: "Coinbase wallet"} => false,
            { Location: "Ledger" } => false,
            { Location: "Trezor" } => false,
            { Location: "coinbase"} => false,
            _ => true
        };
    }
}