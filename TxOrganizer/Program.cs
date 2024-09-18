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
            var balancesRenderer = new BalancesRenderer();
            var transactions = await ReadAllTransactions();
            new BalanceTxProcessor().AnalyzeBalances(transactions, balancesRenderer);
            break;
        }
        case traceTaxLots:
        {
            var transactions = await ReadAllTransactions();
            var consoleRenderer = new TaxLotsRenderer();
            var (taxLots, unmatchedSpends) = new TaxLotProcessor().BuildTaxLots(transactions, consoleRenderer);
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
async Task<IEnumerable<Transaction>> ReadAllTransactions(){
    return await AnsiConsole.Status()
        .StartAsync("Loading transactions from database...", _ => repository.ReadAllTransactions());
}

