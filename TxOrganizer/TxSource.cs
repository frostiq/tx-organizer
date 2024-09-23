using Microsoft.VisualBasic.FileIO;
using TxOrganizer.Database;
using TxOrganizer.DTO;

namespace TxOrganizer;

public class TxSource
{
    private readonly IEnumerable<Setting> _settings;

    public TxSource(SettingsRepository settingsRepository)
    {
        _settings = settingsRepository.GetSettings(SettingType.LocationSourceMapping);
    }

    public IEnumerable<Transaction> LoadTransactions()
    {
        var transactions = new List<Transaction>();
        const string fileName = "data/transactions.csv";
        using var parser = new TextFieldParser(fileName);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.ReadLine();
        
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            
            var tx = BuildTransaction(fields);
            transactions.Add(tx);
        }

        return transactions;
    }
    
    private Transaction BuildTransaction(string[] tokens)
    {
        double ParseNum(string str) => string.IsNullOrEmpty(str) ? 
            0 : 
            double.Parse(str.Replace("$", string.Empty));

        var exchange = tokens[7];
        var import = tokens[10];
        var txHash = tokens[8].Split('-').First();
    
        return new Transaction
        {
            Date = DateTime.Parse(tokens[12]),
            Type = Enum.Parse<TxType>(tokens[0]),
            BuyAmount = ParseNum(tokens[1]),
            BuyCurrency = tokens[2],
            SellAmount = ParseNum(tokens[3]),
            SellCurrency = tokens[4],
            Fee = ParseNum(tokens[5]),
            FeeCurrency = tokens[6],
            Location = ParseLocation(exchange, import),
            TxHash = txHash,
            USDEquivalent = ParseNum(tokens[13]),
            Comment = tokens[11]
        };
    }

    private string ParseLocation(string exchange, string import)
    {
        var setting = _settings.FirstOrDefault(x => import.Contains(x.Key));
        
        var result = setting switch
        {
            {Value: "exchange"} => exchange,
            _ => import
        };

        return result.ToLower();
    }
}