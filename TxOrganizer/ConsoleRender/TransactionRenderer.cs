using Spectre.Console;
using TxOrganizer.DTO;

namespace TxOrganizer.ConsoleRender;

public class TransactionRenderer
{
    public readonly string? TargetCurrency;
    protected readonly string? LocationFilter;

    public TransactionRenderer()
    {
        TargetCurrency = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Base tracking currency")
            .AddChoices("ETH", "BTC", "USDC", "USDT", "DAI", "COMP", "MKR", "UNI", "MATIC", "SOL"));
        
        LocationFilter = AnsiConsole.Ask("Location filter:", string.Empty);
    }

    protected void RenderTx(Transaction tx, double remaining, double sold)
    {
        var txBatchMark = tx is TransactionBatch ? Markup.Escape(" [batch]") : string.Empty;
        AnsiConsole.MarkupLine($"Date: {tx.Date}{txBatchMark}\n");

        switch (tx.Type)
        {
            case TxType.Trade:
            case TxType.Spend:
            case TxType.Lost:
            case TxType.Gift:
            case TxType.Stolen:
                if (tx.BuyCurrency == TargetCurrency)
                {
                    AnsiConsole.MarkupLine(
                        $"[blue]Buy tx[/]: {tx.Type} {tx.SellAmount} {tx.SellCurrency} => [blue]{tx.BuyAmount} {tx.BuyCurrency}[/] on {tx.Location}");
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[darkorange]Sell tx[/]: {tx.Type} [darkorange]{tx.SellAmount} {tx.SellCurrency}[/] => {tx.BuyAmount} {tx.BuyCurrency} from {tx.Location}");
                    if (remaining > 0 || sold > 0)
                    {
                        AnsiConsole.MarkupLine($"Sold in the highlighted tax lot: {sold} {tx.SellCurrency}");
                        AnsiConsole.MarkupLine($"Remainder to sell: {remaining} {tx.SellCurrency}");
                    }
                }

                break;
            case TxType.Deposit:
                AnsiConsole.MarkupLine($"[blue]{tx.Type}: {tx.BuyAmount} {tx.BuyCurrency}[/] on {tx.Location}");
                break;
            case TxType.Withdrawal:
                AnsiConsole.MarkupLine(
                    $"[darkorange]{tx.Type}: {tx.SellAmount} {tx.SellCurrency}[/] from {tx.Location}");
                break;
            default:
                AnsiConsole.MarkupLine($"{tx}");
                break;
        }

        if (tx.FeeCurrency == TargetCurrency)
        {
            AnsiConsole.MarkupLine($"Fees spent: [darkorange]{tx.Fee} {tx.FeeCurrency}[/]");
        }

        if (!string.IsNullOrEmpty(tx.TxHash) && tx.TxHash.StartsWith("0x"))
        {
            AnsiConsole.MarkupLine($"Link: https://etherscan.io/tx/{tx.TxHash}");
        }
    }
}