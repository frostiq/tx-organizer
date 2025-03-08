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