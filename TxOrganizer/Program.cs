using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TxOrganizer;
using TxOrganizer.ConsoleRender;
using TxOrganizer.Database;
using TxOrganizer.DataSource;
using TxOrganizer.DTO;
using TxOrganizer.Processors;

const string fetchTokenTaxLineItems = "Fetch TokenTax line items";
const string traceBalances = "Trace balances";
const string traceTaxLots = "Trace tax lots";
const string positionHistory = "Position history";
const string importTransactions = "Import transactions";
const string fetchBinanceTxHistory = "Fetch Binance Transaction History";
const string analyzeUnmatchedDepositWithdrawals = "Analyze deposit/withdrawal missmatch";
const string findDuplicateTransactions = "Find duplicate transactions";
const string addSetting = "Add setting";
const string exit = "Exit";

try
{
    var dbContextFactory = new AppDbContextFactory();
    await using var dbContext = dbContextFactory.CreateDbContext(Array.Empty<string>());

    AnsiConsole.WriteLine("Initializing local database...");
    var migrationsAssembly = dbContext.GetInfrastructure().GetService<IMigrationsAssembly>();
    AnsiConsole.WriteLine($"Migrations assembly: {migrationsAssembly?.Assembly} with {migrationsAssembly?.Migrations.Count} migrations");
    await dbContext.Database.MigrateAsync();

    var repository = new FinancialDatabaseRepository(dbContext);
    var settingsRepository = new SettingsRepository(dbContext);
    var coinGeckoPriceFetcher = new CoinGeckoPriceFetcher(dbContext);
    var csvSource = new TxSource(settingsRepository);
    
    string? action = null;
    while (action != exit)
    {
        var selectionPrompt = new SelectionPrompt<string>()
            .Title("What's would you like to do?")
            .AddChoices(
                findDuplicateTransactions,
                fetchTokenTaxLineItems,
                traceBalances,
                traceTaxLots,
                positionHistory,
                importTransactions,
                analyzeUnmatchedDepositWithdrawals,
                fetchBinanceTxHistory,
                addSetting,
                exit);
        action = AnsiConsole.Prompt(selectionPrompt);
        AnsiConsole.Clear();

        switch (action)
        {
            case traceBalances:
            {
                var transactions = await ReadAllTransactions(repository);

                var startDate =
                    AnsiConsole.Prompt(new TextPrompt<DateTime?>(Markup.Escape("Enter start date or [space]"))
                        .AllowEmpty());
                var balancesRenderer = new BalancesRenderer(startDate);
                var balanceProcessor = new BalanceTxProcessor();

                balanceProcessor.AnalyzeBalances(transactions, balancesRenderer);
                balanceProcessor.WriteTotalQuantityHistoryToCsv("total_quantity_history_balances.csv");
                break;
            }
            case traceTaxLots:
            {
                var transactions = await ReadAllTransactions(repository);

                var startDate =
                    AnsiConsole.Prompt(new TextPrompt<DateTime?>(Markup.Escape("Enter start date or [space]"))
                        .AllowEmpty());
                var taxLotsRenderer = new TaxLotsRenderer(startDate);
                var taxLotsProcessor = new TaxLotProcessor();

                if (taxLotsRenderer.Trace)
                {
                    taxLotsProcessor.BuildTaxLots(transactions, taxLotsRenderer, null, taxLotsRenderer.TargetCurrency);
                }
                else
                {
                    AnsiConsole.Progress()
                        .Start(ctx =>
                        {
                            var task = ctx.AddTask("Building tax lots...", maxValue: 1.0);
                            taxLotsProcessor.BuildTaxLots(transactions, taxLotsRenderer, task,
                                taxLotsRenderer.TargetCurrency);
                            task.Value = task.MaxValue;
                        });
                }

                taxLotsProcessor.WriteTotalQuantityHistoryToCsv("total_quantity_history_taxlots.csv");

                break;
            }
            case positionHistory:
            {
                var transactions = await ReadAllTransactions(repository);
                var positionsRenderer = new PositionsRenderer(settingsRepository);
                var positionProcessor = new PositionProcessor(coinGeckoPriceFetcher, settingsRepository);
                
                var (positions, unmatchedSpends) = await AnsiConsole.Progress()
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Building positions...", maxValue: 1.0);
                        var res = await positionProcessor.BuildPositions(transactions, () =>
                        {
                            task.Increment(1.0 / transactions.Count);
                        });
                        task.Value = task.MaxValue;
                        return res;
                    });

                
                positionsRenderer.PrintPositions(positions, unmatchedSpends);
                break;
            }
            case importTransactions:
            {
                var transactions = csvSource.LoadTransactions();
                repository.ImportTransactions(transactions);
                break;
            }
            case fetchBinanceTxHistory:
            {
                await DataProcessor.DownloadBinanceData();
                break;
            }
            case fetchTokenTaxLineItems:
            {
                var reportId = AnsiConsole.Ask<uint>("Please enter TokenTax report ID:");
                await DataProcessor.DownloadTokenTaxLineItems(reportId);
                break;
            }
            case analyzeUnmatchedDepositWithdrawals:
            {
                var targetCurrency = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Base tracking currency")
                    .AddChoices("ETH", "BTC", "USDC", "USDT", "DAI", "COMP", "MKR", "UNI", "MATIC", "SOL"));

                var processor = new DepositWithdrawalMatchProcessor();
                var renderer = new UnmatchedDepoistWithdrawalsRenderer();
                var transactions = await ReadAllTransactions(repository);

                var (unmatchedDeposits, unmatchedWithdrawals) =
                    await processor.AnalyzeDepositWithdrawals(transactions, targetCurrency);

                renderer.RenderUnmatchedDepositsAndWithdrawals(unmatchedDeposits, unmatchedWithdrawals);
                break;
            }
            case findDuplicateTransactions:
            {
                var transactions = csvSource.LoadTransactions().ToList();
                FindAndHighlightDuplicates(transactions);
                break;
            }
            case addSetting:
            {
                var type = AnsiConsole.Prompt(new SelectionPrompt<SettingType>()
                    .Title("Select setting type")
                    .AddChoices(Enum.GetValues<SettingType>()));
                var key = AnsiConsole.Prompt(new TextPrompt<string>("Enter key:"));
                var value = AnsiConsole.Prompt(new TextPrompt<string>("Enter value:").AllowEmpty());
                var setting = new Setting
                {
                    Type = type,
                    Key = key,
                    Value = value
                };
                settingsRepository.AddSetting(setting);
                break;
            }
        }
    }
}
catch (Exception e)
{
    AnsiConsole.WriteException(e);
}

// Read transactions function
async Task<List<Transaction>> ReadAllTransactions(FinancialDatabaseRepository repository)
{
    return await AnsiConsole.Status()
        .StartAsync("Loading transactions from database...", _ => repository.ReadAllTransactions());
}

// Helper function to count decimal places
int GetDecimalPlaces(double value)
{
    var str = value.ToString("G5");
    if (!str.Contains('.')) return 0;
    
    // Remove trailing zeros
    str = str.TrimEnd('0');
    if (str.EndsWith('.')) return 0;
    
    return str.Length - str.IndexOf('.') - 1;
}

// Find and highlight duplicates
void FindAndHighlightDuplicates(List<Transaction> transactions)
{
    // Group transactions by date, buy currency, and sell currency
    var duplicateGroups = transactions
        .Where(x => !(x.BuyCurrency.Contains("USDT") && x.SellCurrency.Contains("USDT"))
                    && !string.IsNullOrWhiteSpace(x.BuyCurrency)
                    && !string.IsNullOrWhiteSpace(x.SellCurrency)
                    && x.BuyAmount > 0
                    && x.SellAmount > 0)
        .GroupBy(t => 
        {
            var buyDecimals = GetDecimalPlaces(t.BuyAmount);
            var sellDecimals = GetDecimalPlaces(t.SellAmount);
            var minDecimals = Math.Min(buyDecimals, sellDecimals) - 1;
            if (minDecimals < 0) minDecimals = 0;
            if (minDecimals > 15) minDecimals = 15;
            
            return new
            {
                Date = t.Date.ToString("MM/dd/yyyy"),
                BuyCurrency = t.BuyCurrency?.ToUpperInvariant(),
                SellCurrency = t.SellCurrency?.ToUpperInvariant(),
                BuyAmount = Math.Round(t.BuyAmount, minDecimals),
                SellAmount = Math.Round(t.SellAmount, minDecimals),
            };
        });
    
    var test = duplicateGroups.Where(x => x.Key.Date.Contains("03/12/2024")).ToList();
    duplicateGroups = duplicateGroups.Where(g => g.Select(x => x.Location).Distinct().Count() > 1)
        .OrderBy(g => g.First().Date)
        .ToList();

    if (!duplicateGroups.Any())
    {
        AnsiConsole.MarkupLine("[green]No duplicates found![/]");
        return;
    }

    AnsiConsole.MarkupLine($"[yellow]Found {duplicateGroups.Count()} groups of duplicate transactions:[/]");
    AnsiConsole.WriteLine();

    var table = new Table();
    table.AddColumn("Date");
    table.AddColumn("Buy Currency");
    table.AddColumn("Buy Amount");
    table.AddColumn("Sell Currency");
    table.AddColumn("Sell Amount");
    table.AddColumn("Location");
    table.AddColumn("Type");

    foreach (var group in duplicateGroups)
    {
        AnsiConsole.MarkupLine($"[bold]Duplicate group: {group.Key.Date:yyyy-MM-dd} - {group.Key.BuyCurrency ?? "N/A"}/{group.Key.SellCurrency ?? "N/A"}[/]");
        
        foreach (var transaction in group.OrderBy(t => t.Location))
        {
            table.AddRow(
                transaction.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                transaction.BuyCurrency ?? "N/A",
                transaction.BuyAmount.ToString("F8"),
                transaction.SellCurrency ?? "N/A",
                transaction.SellAmount.ToString("F8"),
                $"[red]{transaction.Location ?? "N/A"}[/]",
                transaction.Type.ToString()
            );
        }
        
        table.AddEmptyRow();
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"[yellow]Total duplicate transactions: {duplicateGroups.Sum(g => g.Count())}[/]");
}