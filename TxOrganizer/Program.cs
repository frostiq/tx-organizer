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
const string addMultipleAddressLabels = "Add multiple address labels";
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
    var addressLabelRepository = new AddressLabelRepository(dbContext);
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
                addMultipleAddressLabels,
                addSetting,
                exit);
        action = AnsiConsole.Prompt(selectionPrompt);
        AnsiConsole.Clear();

        switch (action)
        {
            case traceBalances:
            {
                var transactions = await TransactionLoader.ReadAllTransactionsAsync(repository);

                var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("[[Optional]] Enter start date (YYYY-MM-DD)")
                        .DefaultValue(null)
                        .AllowEmpty());
                var balancesRenderer = new BalancesRenderer(startDate);
                var balanceProcessor = new BalanceTxProcessor(dbContext);

                await balanceProcessor.AnalyzeBalances(transactions, balancesRenderer);
                balanceProcessor.WriteTotalQuantityHistoryToCsv("total_quantity_history_balances.csv");

                AnsiConsole.MarkupLine("Last transaction date: " +
                                      $"{transactions.Max(x => x.Date).ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
                break;
            }
            case traceTaxLots:
            {
                var transactions = await TransactionLoader.ReadAllTransactionsAsync(repository);

                var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("[[Optional]] Enter start date (YYYY-MM-DD)")
                        .DefaultValue(null)
                        .AllowEmpty());
                var taxLotsRenderer = new TaxLotsRenderer(startDate);
                var taxLotsProcessor = new TaxLotProcessor();

                if (taxLotsRenderer.Trace)
                {
                    taxLotsProcessor.BuildTaxLots(transactions, taxLotsRenderer, null!, taxLotsRenderer.TargetCurrency);
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
                // Check if API keys are configured
                if (string.IsNullOrWhiteSpace(apiKeysConfig.Etherscan))
                {
                    AnsiConsole.MarkupLine("[red]Error: Etherscan API key not configured in appsettings.json[/]");
                    AnsiConsole.MarkupLine("[yellow]Please set your Etherscan API key in appsettings.json under ApiKeys:Etherscan[/]");
                    break;
                }
                
                if (string.IsNullOrWhiteSpace(apiKeysConfig.QuickNodeUrl))
                {
                    AnsiConsole.MarkupLine("[red]Error: QuickNode URL not configured in appsettings.json[/]");
                    AnsiConsole.MarkupLine("[yellow]Please set your QuickNode endpoint URL in appsettings.json under ApiKeys:QuickNodeUrl[/]");
                    break;
                }

                var ethAddresses = await addressLabelRepository.GetAllLabelsAsync();

                if (!ethAddresses.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No Ethereum addresses found in AddressLabel database.[/]");
                    AnsiConsole.MarkupLine("[gray]Ethereum addresses should be 42 characters long and start with '0x'.[/]");
                    break;
                }

                AnsiConsole.MarkupLine($"[blue]Found {ethAddresses.Count} Ethereum addresses to process:[/]");
                foreach (var addr in ethAddresses)
                {
                    AnsiConsole.MarkupLine($"[gray]  {addr.Address} -> {addr.Label}{(addr.Category != null ? $" ({addr.Category})" : "")}[/]");
                }

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

                var confirm = AnsiConsole.Confirm($"Process historical balances for {ethAddresses.Count} addresses?");
                if (!confirm)
                {
                    AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                    break;
                }

                try
                {
                    var httpClient = new HttpClient();
                    var etherscanApi = new EtherscanApiService(httpClient, apiKeysConfig.Etherscan, apiKeysConfig.QuickNodeUrl);
                    var ethProcessor = new EthHistoricalBalanceProcessor(dbContext, etherscanApi);

                    var processedCount = 0;
                    var errorCount = 0;

                    await AnsiConsole.Progress()
                        .StartAsync(async ctx =>
                        {
                            var overallTask = ctx.AddTask("Processing all addresses", maxValue: ethAddresses.Count);
                            
                            foreach (var addressLabel in ethAddresses)
                            {
                                AnsiConsole.MarkupLine($"[cyan]Processing {addressLabel.Label} ({addressLabel.Address})...[/]");
                                
                                try
                                {
                                    await ethProcessor.ProcessHistoricalBalancesAsync(addressLabel.Address, startBlock, endBlock);
                                    processedCount++;
                                    AnsiConsole.MarkupLine($"[green]✓ Completed {addressLabel.Label} ({addressLabel.Address})[/]");
                                }
                                catch (Exception ex)
                                {
                                    errorCount++;
                                    AnsiConsole.MarkupLine($"[red]✗ Error processing {addressLabel.Label} ({addressLabel.Address}): {ex.Message}[/]");
                                }
                                
                                overallTask.Increment(1);
                            }
                        });

                    AnsiConsole.MarkupLine($"[blue]Processing completed:[/]");
                    AnsiConsole.MarkupLine($"[green]  Successfully processed: {processedCount}[/]");
                    if (errorCount > 0)
                    {
                        AnsiConsole.MarkupLine($"[red]  Errors encountered: {errorCount}[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error during processing: {ex.Message}[/]");
                }
                break;
            }
            case addMultipleAddressLabels:
            {
                await AddMultipleAddressLabels(addressLabelRepository);
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

// Function to add multiple address labels
static async Task AddMultipleAddressLabels(AddressLabelRepository addressLabelRepository)
{
    AnsiConsole.MarkupLine("[blue]Add Multiple Address Labels[/]");
    AnsiConsole.MarkupLine("[gray]Enter address-label pairs. Type 'done' when finished.[/]");
    AnsiConsole.MarkupLine("[gray]Format: address,label,category (category is optional)[/]");
    AnsiConsole.WriteLine();

    var addressLabels = new List<(string address, string label, string? category)>();

    while (true)
    {
        var input = AnsiConsole.Prompt(new TextPrompt<string>($"Entry {addressLabels.Count + 1} (or 'done' to finish):")
            .AllowEmpty());

        if (string.IsNullOrWhiteSpace(input) || input.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        var parts = input.Split(',', StringSplitOptions.TrimEntries);
        
        if (parts.Length < 2)
        {
            AnsiConsole.MarkupLine("[red]Invalid format. Use: address,label,category[/]");
            continue;
        }

        var address = parts[0];
        var label = parts[1];
        var category = parts.Length > 2 ? parts[2] : null;

        // Basic address validation
        if (string.IsNullOrWhiteSpace(address))
        {
            AnsiConsole.MarkupLine("[red]Address cannot be empty[/]");
            continue;
        }

        if (address.Length == 42 && address.StartsWith("0x"))
        {
            // Ethereum address format
        }
        else if (address.Length < 6)
        {
            AnsiConsole.MarkupLine("[red]Address too short[/]");
            continue;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            AnsiConsole.MarkupLine("[red]Label cannot be empty[/]");
            continue;
        }

        addressLabels.Add((address, label, category));
        AnsiConsole.MarkupLine($"[green]Added: {address} -> {label}{(category != null ? $" ({category})" : "")}[/]");
    }

    if (addressLabels.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No address labels to add.[/]");
        return;
    }

    // Confirm before saving
    var confirm = AnsiConsole.Confirm($"Save {addressLabels.Count} address labels?");
    if (!confirm)
    {
        AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
        return;
    }

    // Save all labels
    var savedCount = 0;
    foreach (var (address, label, category) in addressLabels)
    {
        try
        {
            await addressLabelRepository.AddOrUpdateLabelAsync(address, label, category);
            savedCount++;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error saving {address}: {ex.Message}[/]");
        }
    }

    AnsiConsole.MarkupLine($"[green]Successfully saved {savedCount} of {addressLabels.Count} address labels.[/]");
}