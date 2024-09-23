﻿using System.Text.RegularExpressions;
using Spectre.Console;
using TxOrganizer;
using TxOrganizer.ConsoleRender;
using TxOrganizer.Database;
using TxOrganizer.DataSource;
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
const string fetchBinanceTxHistory = "Fetch Binance Transaction History";

var action = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("What's would you like to do?")
        .AddChoices(traceBalances, traceTaxLots, positionHistory, importTransactions, fetchBinanceTxHistory));

try
{
    switch (action)
    {
        case traceBalances:
        {
            var transactions = await ReadAllTransactions();
            
            var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("Start date?").AllowEmpty());
            var balancesRenderer = new BalancesRenderer(startDate);
            var balanceProcessor = new BalanceTxProcessor();
            
            balanceProcessor.AnalyzeBalances(transactions, balancesRenderer);
            balanceProcessor.WriteTotalQuantityHistoryToCsv("total_quantity_history_balances.csv");
            break;
        }
        case traceTaxLots:
        {
            var transactions = await ReadAllTransactions();
            
            var startDate = AnsiConsole.Prompt(new TextPrompt<DateTime?>("Start date?").AllowEmpty());
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
        case fetchBinanceTxHistory:
        {
            var fetcher = new BinanceTxHistoryFetcher();

            var lines = new List<string>();
            while (true)
            {
                var line = AnsiConsole.Prompt(new TextPrompt<string>("Enter your headers (or 'END' to finish):"));
                if (line.ToUpper() == "END")
                {
                    break;
                }
                lines.Add(line);
            }
            var rawHeaders = string.Join("\n", lines);
            var matches = new Regex("-H '([^:;]+)[:;] ([^']+)").Matches(rawHeaders);
            var headers = matches.ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            var withdrawals = await fetcher.FetchWithdrawalHistory(headers);
            fetcher.WriteTransactionHistoryToCsv("binance-withdrawals.csv", withdrawals);
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