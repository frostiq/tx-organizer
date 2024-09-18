namespace TxOrganizer.DTO;

public class TxSpend
{
    public TxSpend(Transaction tx, double amount)
    {
        Tx = tx;
        Amount = amount;
    }

    public Transaction Tx { get; set; }

    public double Amount { get; set; }

    public double PriceUsd => Tx.USDEquivalent / Tx.SellAmount;

    public override string ToString()
    {
        return $"{Amount} {Tx.SellCurrency} of {Tx}";
    }
}