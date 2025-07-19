using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TxOrganizer;
using TxOrganizer.Analysis;
using TxOrganizer.Configuration;
using TxOrganizer.ConsoleRender;
using TxOrganizer.Database;
using TxOrganizer.DataSource;
using TxOrganizer.DTO;
using TxOrganizer.Processors;
using TxOrganizer.Utilities;

const string fetchTokenTaxLineItems = "Fetch TokenTax line items";
const string traceBalances = "Trace balances";
const string traceTaxLots = "Trace tax lots";
const string positionHistory = "Position history";
const string importTransactions = "Import transactions";
const string fetchBinanceTxHistory = "Fetch Binance Transaction History";
const string analyzeUnmatchedDepositWithdrawals = "Analyze deposit/withdrawal missmatch";
const string findDuplicateTransactions = "Find duplicate transactions";
const string addSetting = "Add setting";
const string detectTransactionDifferences = "Detect transaction differences";
const string processEthHistoricalBalances = "Process ETH Historical Balances";
const string exit = "Exit";

try
{
    // Build configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build();

    // Bind API keys configuration
    var apiKeysConfig = new ApiKeysConfiguration();
    configuration.GetSection(ApiKeysConfiguration.SectionName).Bind(apiKeysConfig);

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
                // fetchTokenTaxLineItems,
                traceBalances,
                traceTaxLots,
                positionHistory,
                importTransactions,
                analyzeUnmatchedDepositWithdrawals,
                fetchBinanceTxHistory,
                detectTransactionDifferences,
                processEthHistoricalBalances,
                addSetting,
                exit);
        action = AnsiConsole.Prompt(selectionPrompt);
        AnsiConsole.Clear();

        switch (action)
        {
            case traceBalances:
            {
                var transactions = await TransactionLoader.ReadAllTransactionsAsync(repository);

                var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("[[Optional]] Enter start date")
                        .DefaultValue(null)
                        .AllowEmpty());
                var balancesRenderer = new BalancesRenderer(startDate);
                var balanceProcessor = new BalanceTxProcessor();

                balanceProcessor.AnalyzeBalances(transactions, balancesRenderer);
                balanceProcessor.WriteTotalQuantityHistoryToCsv("total_quantity_history_balances.csv");
                break;
            }
            case traceTaxLots:
            {
                var transactions = await TransactionLoader.ReadAllTransactionsAsync(repository);

                var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("[[Optional]] Enter start date")
                        .DefaultValue(null)
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
                var transactions = await TransactionLoader.ReadAllTransactionsAsync(repository);
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
                var transactions = await TransactionLoader.ReadAllTransactionsAsync(repository);

                var (unmatchedDeposits, unmatchedWithdrawals) =
                    await processor.AnalyzeDepositWithdrawals(transactions, targetCurrency);

                renderer.RenderUnmatchedDepositsAndWithdrawals(unmatchedDeposits, unmatchedWithdrawals);
                break;
            }
            case findDuplicateTransactions:
            {
                var transactions = csvSource.LoadTransactions().ToList();
                DuplicateTransactionAnalyzer.FindAndHighlightDuplicates(transactions);
                break;
            }
            case detectTransactionDifferences:
            {
                var csvTransactions = csvSource.LoadTransactions();
                var dbTransactions = await TransactionLoader.ReadAllTransactionsAsync(repository);
                var groupedDiffs = TransactionDifferenceAnalyzer.FindGroupedTransactionDifferences(csvTransactions, dbTransactions);

                TransactionDifferenceAnalyzer.DisplayTransactionDifferences(groupedDiffs);
                break;
            }
            case processEthHistoricalBalances:
            {
                // Check if API key is configured
                if (string.IsNullOrWhiteSpace(apiKeysConfig.Etherscan))
                {
                    AnsiConsole.MarkupLine("[red]Error: Etherscan API key not configured in appsettings.json[/]");
                    AnsiConsole.MarkupLine("[yellow]Please set your Etherscan API key in appsettings.json under ApiKeys:Etherscan[/]");
                    break;
                }

                var address = AnsiConsole.Prompt(new TextPrompt<string>("Enter Ethereum address:")
                    .Validate(addr => 
                    {
                        if (string.IsNullOrWhiteSpace(addr))
                            return ValidationResult.Error("Address cannot be empty");
                        if (addr.Length != 42 || !addr.StartsWith("0x"))
                            return ValidationResult.Error("Invalid Ethereum address format (must be 42 chars starting with 0x)");
                        return ValidationResult.Success();
                    }));

                var startBlock = AnsiConsole.Prompt(new TextPrompt<long>("Enter start block number:")
                    .DefaultValue(0L)
                    .Validate(block => block >= 0 ? ValidationResult.Success() : ValidationResult.Error("Block number must be >= 0")));

                var endBlock = AnsiConsole.Prompt(new TextPrompt<long?>("Enter end block number (optional):")
                    .DefaultValue(null)
                    .AllowEmpty()
                    .Validate(block => 
                    {
                        if (!block.HasValue) return ValidationResult.Success();
                        return block.Value >= startBlock ? ValidationResult.Success() : ValidationResult.Error("End block must be >= start block");
                    }));

                try
                {
                    var httpClient = new HttpClient();
                    var etherscanApi = new EtherscanApiService(httpClient, apiKeysConfig.Etherscan);
                    var ethProcessor = new EthHistoricalBalanceProcessor(dbContext, etherscanApi);

                    await AnsiConsole.Progress()
                        .StartAsync(async ctx =>
                        {
                            var task = ctx.AddTask($"Processing ETH balances for {address}", maxValue: 100);
                            task.IsIndeterminate = true;
                            
                            await ethProcessor.ProcessHistoricalBalancesAsync(address, startBlock, endBlock);
                            
                            task.Value = task.MaxValue;
                        });

                    AnsiConsole.MarkupLine($"[green]Successfully processed historical balances for address {address}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error processing historical balances: {ex.Message}[/]");
                }
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