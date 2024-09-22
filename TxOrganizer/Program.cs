using Spectre.Console;
using TxOrganizer;
using TxOrganizer.ConsoleRender;
using TxOrganizer.Database;
using TxOrganizer.DTO;
using TxOrganizer.Processors;

var dbContextFactory = new AppDbContextFactory();
var dbContext = dbContextFactory.CreateDbContext(Array.Empty<string>());
var repository = new FinancialDatabaseRepository(dbContext);
var settingsRepository = new SettingsRepository(dbContext);
var coinGeckoPriceFetcher = new CoinGeckoPriceFetcher(dbContext);
var csvSource = new TxSource(settingsRepository);

const string traceBalances = "Trace balances";
const string traceTaxLots = "Trace tax lots";
const string positionHistory = "Position history";
const string importTransactions = "Import transactions";

var action = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("What's would you like to do?")
        .AddChoices(traceBalances, traceTaxLots, positionHistory, importTransactions));

try
{
    switch (action)
    {
        case traceBalances:
        {
            var transactions = await ReadAllTransactions();
            
            var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("Start date?"));
            var balancesRenderer = new BalancesRenderer(startDate);
            var balanceProcessor = new BalanceTxProcessor();
            
            balanceProcessor.AnalyzeBalances(transactions, balancesRenderer);
            balanceProcessor.WriteTotalQuantityHistoryToCsv("total_quantity_history_balances.csv");
            break;
        }
        case traceTaxLots:
        {
            var transactions = await ReadAllTransactions();
            
            var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("Start date?"));
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
                        taxLotsProcessor.BuildTaxLots(transactions, taxLotsRenderer, task, taxLotsRenderer.TargetCurrency);
                        task.Value = task.MaxValue;
                    });
            }
            
            taxLotsProcessor.WriteTotalQuantityHistoryToCsv("total_quantity_history_taxlots.csv");

            break;
        }
        case positionHistory:
        {
            var transactions = await ReadAllTransactions();
            var positionsRenderer = new PositionsRenderer();
            var positionProcessor = new PositionProcessor(coinGeckoPriceFetcher);
            var (positions, unmatchedSpends) = await AnsiConsole.Status()
                .StartAsync("Building positions...", _ => positionProcessor.BuildPositions(transactions));
            positionsRenderer.PrintPositions(positions, unmatchedSpends);
            break;
        }
        case importTransactions:
        {
            var transactions = csvSource.LoadTransactions();
            repository.ImportTransactions(transactions);
            break;
        }
    }
}
catch (ApplicationException e)
{
}

// Read transactions function
async Task<IEnumerable<Transaction>> ReadAllTransactions()
{
    return await AnsiConsole.Status()
        .StartAsync("Loading transactions from database...", _ => repository.ReadAllTransactions());
}