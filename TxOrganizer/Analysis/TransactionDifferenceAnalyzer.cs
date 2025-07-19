using Spectre.Console;
using TxOrganizer.DTO;

namespace TxOrganizer.Analysis;

public static class TransactionDifferenceAnalyzer
{
    /// <summary>
    /// Finds all differences between CSV and DB transactions, grouped by date
    /// </summary>
    /// <param name="csvTransactions">Transactions from CSV source</param>
    /// <param name="dbTransactions">Transactions from database</param>
    /// <returns>List of differences with CSV and DB transactions side by side</returns>
    public static List<(Transaction? Csv, Transaction? Db, DateTime Date)> FindGroupedTransactionDifferences(
        IEnumerable<Transaction> csvTransactions, 
        IEnumerable<Transaction> dbTransactions)
    {
        string Key(Transaction t) => $"{t.Type}:{t.Date:yyyy-MM-dd}|{t.BuyCurrency}|{Math.Round(t.BuyAmount,2)}|{t.SellCurrency}|{Math.Round(t.SellAmount,2)}:{t.Comment}";
        
        var csvByDate = csvTransactions.GroupBy(t => t.Date.Date).ToDictionary(g => g.Key, g => g.ToList());
        var dbByDate = dbTransactions.GroupBy(t => t.Date.Date).ToDictionary(g => g.Key, g => g.ToList());
        var allDates = csvByDate.Keys.Union(dbByDate.Keys).OrderBy(d => d);
        var result = new List<(Transaction?, Transaction?, DateTime)>();
        var cutoff = new DateTime(2025, 1, 1);
        
        foreach (var date in allDates)
        {
            if (date >= cutoff) continue;
            
            var csvList = csvByDate.ContainsKey(date) ? csvByDate[date] : new List<Transaction>();
            var dbList = dbByDate.ContainsKey(date) ? dbByDate[date] : new List<Transaction>();
            var csvKeys = new HashSet<string>(csvList.Select(Key));
            var dbKeys = new HashSet<string>(dbList.Select(Key));
            
            // All CSV transactions not in DB for this date
            foreach (var t in csvList.Where(t => !dbKeys.Contains(Key(t))))
                result.Add((t, null, date));
            
            // All DB transactions not in CSV for this date
            foreach (var t in dbList.Where(t => !csvKeys.Contains(Key(t))))
                result.Add((null, t, date));
        }
        
        return result;
    }

    /// <summary>
    /// Displays the differences between CSV and database transactions in a formatted tree view
    /// </summary>
    /// <param name="differences">The differences to display</param>
    public static void DisplayTransactionDifferences(List<(Transaction? Csv, Transaction? Db, DateTime Date)> differences)
    {
        if (!differences.Any())
        {
            AnsiConsole.MarkupLine("[green]No differences found between CSV and database transactions![/]");
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Found {differences.Count} differences (grouped by date):[/]");
        var groupedByDate = differences.GroupBy(x => x.Date).OrderBy(g => g.Key);
        var root = new Tree("[bold]Differences by Date[/]");
        
        foreach (var group in groupedByDate)
        {
            var dateNode = root.AddNode($"{group.Key:yyyy-MM-dd} ({group.Count()} differences)");
            foreach (var (csv, db, _) in group)
            {
                var entryNode = dateNode.AddNode("");
                if (csv != null) 
                {
                    var csvDisplay = Markup.Escape(csv.ToString());
                    if (!string.IsNullOrEmpty(csv.Comment))
                        csvDisplay += $" - Comment: {Markup.Escape(csv.Comment)}";
                    entryNode.AddNode($"[green]CSV:[/] {csvDisplay}");
                }
                if (db != null) 
                {
                    var dbDisplay = Markup.Escape(db.ToString());
                    if (!string.IsNullOrEmpty(db.Comment))
                        dbDisplay += $" - Comment: {Markup.Escape(db.Comment)}";
                    entryNode.AddNode($"[red]DB :[/] {dbDisplay}");
                }
            }
        }
        AnsiConsole.Write(root);
    }
}
