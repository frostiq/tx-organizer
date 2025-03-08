using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace TxOrganizer.DataSource;

public class TokenTaxLineItemsFetcher
{
    private readonly HttpClient _client = new HttpClient();

    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<List<LineItem>> FetchTokenTaxLineItems(uint txnReportId, Dictionary<string, string> headers)
    {
        const string url = "https://api.app.tokentax.co/graph/graphql?opname=lineItems";

        foreach (var header in headers.Where(header => header.Key.ToUpper() != "CONTENT-TYPE"))
        {
            _client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        var lineItems = new List<LineItem>();
        var pageSize = 50;
        var skip = 0;

        while (true)
        {
            var body = new
            {
                operationName = "lineItems",
                variables = new
                {
                    txnReportId,
                    pagination = new { first = pageSize, skip },
                    sortBy = "sellDate",
                    sortDirection = "asc",
                    filterQuery = new { },
                    isAdmin = false,
                    filterDust = false
                },
                query =
                    "query lineItems($txnReportId: Int!, $pagination: PaginationOptions!, $sortBy: LineItemSortOptions!, $sortDirection: SortDirectionOptions!, $filterQuery: LineItemsFilterQuery, $isAdmin: Boolean!, $filterDust: Boolean) " +
                    "{ lineItems(lineItemInput: {txnReportId: $txnReportId, pagination: $pagination, sortBy: $sortBy, sortDirection: $sortDirection, filterQuery: $filterQuery, filterDust: $filterDust}) " +
                    "{ pageInfo { filteredCount __typename } edges { id buyId sellId sellCurrency buyCurrency unitsSold feeUnitsSold feeCurrency buyDate sellDate proceedsIncludingFees " +
                    "costBasisIncludingFees gainLossIncludingFees term missingCostBasis splitBuyId txnReportId isFee accountId account { id name __typename } txnLineItemSellIdTotxn " +
                    "{ ...Txn __typename } __typename } __typename } } fragment Txn on Txn { id associatedExchangeAddress blocksvcHash credentialId buyCurrency buyTokenId buyAddress buyNftId description exchangeId exchangeName feeCurrency feeTokenId feeQuantity feePrice feeAddress buyPrice sellPrice buyQuantity sellCurrency sellTokenId sellAddress sellNftId sellQuantity txnTimestamp txnType unitPrice priceFetchingSide usdSpotPrice blocksvcHash blocksvcMethodId blocksvcToAddress blocksvcFromAddress createdAt updatedAt isEdited isSpam reviewed editedByReconGuideJob reconIdentifier @include(if: $isAdmin) bkpVendorId integrationId toIntegrationId bkpVendor { id bkpIntegrationId bkpIntegrationDisplayName __typename } bkpAccountDebitId bkpAccountDebit { id bkpIntegrationId name __typename } bkpAccountCreditId bkpAccountCredit { id bkpIntegrationId name __typename } credential { accountId credentialType source name address integrationId __typename } specIdMatchesAsComponent { ...SpecIdMatch __typename } specIdMatchesAsSell { ...SpecIdMatch __typename } credentialId accountId toAccountId hasMovement __typename } fragment SpecIdMatch on SpecIdMatch { componentId sellId componentQuantity componentOrder __typename }"
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStringAsync();
            var responseModel = JsonSerializer.Deserialize<ResponseModel>(responseStream, _jsonOptions);

            if (responseModel.Data.LineItems.Edges.Count == 0)
            {
                break;
            }

            lineItems.AddRange(responseModel.Data.LineItems.Edges);
            skip += pageSize;
            AnsiConsole.WriteLine($"Fetched {lineItems.Count} line items. Skip = {skip}");
        }

        return lineItems;
    }

    private class ResponseModel
    {
        public DataModel Data { get; set; }
    }

    private class DataModel
    {
        public LineItemsModel LineItems { get; set; }
    }

    private class LineItemsModel
    {
        public List<LineItem> Edges { get; set; }
    }

    public class LineItem
    {
        public ulong Id { get; set; }
        public string BuyId { get; set; }
        public string SellId { get; set; }
        public string SellCurrency { get; set; }
        public string BuyCurrency { get; set; }
        public double? UnitsSold { get; set; }
        public double? FeeUnitsSold { get; set; }
        public string FeeCurrency { get; set; }
        public DateTime? BuyDate { get; set; }
        public DateTime? SellDate { get; set; }
        public double? ProceedsIncludingFees { get; set; }
        public double? CostBasisIncludingFees { get; set; }
        public double? GainLossIncludingFees { get; set; }
        public string Term { get; set; }
        public bool? MissingCostBasis { get; set; }
        public string SplitBuyId { get; set; }
        public int? TxnReportId { get; set; }
        public bool? IsFee { get; set; }
        public int? AccountId { get; set; }
        public Account Account { get; set; }
        public Txn TxnLineItemSellIdTotxn { get; set; }
    }

    public class Account
    {
        public int? Id { get; set; }
        public string Name { get; set; }
    }

    public class Txn
    {
        public string Id { get; set; }
        public string AssociatedExchangeAddress { get; set; }
        public string BlocksvcHash { get; set; }
        public int? CredentialId { get; set; }
        public string BuyCurrency { get; set; }
        public string BuyTokenId { get; set; }
        public string BuyAddress { get; set; }
        public string BuyNftId { get; set; }
        public string Description { get; set; }
        public string ExchangeId { get; set; }
        public string ExchangeName { get; set; }
        public string FeeCurrency { get; set; }
        public string FeeTokenId { get; set; }
        public double? FeeQuantity { get; set; }
        public string FeePrice { get; set; }
        public string FeeAddress { get; set; }
        public string BuyPrice { get; set; }
        public string SellPrice { get; set; }
        public string BuyQuantity { get; set; }
        public string SellCurrency { get; set; }
        public string SellTokenId { get; set; }
        public string SellAddress { get; set; }
        public string SellNftId { get; set; }
        public string SellQuantity { get; set; }
        public DateTime? TxnTimestamp { get; set; }
        public string TxnType { get; set; }
        public double? UnitPrice { get; set; }
        public double? UsdSpotPrice { get; set; }
        public string BlocksvcMethodId { get; set; }
        public string BlocksvcToAddress { get; set; }
        public string BlocksvcFromAddress { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool? IsEdited { get; set; }
        public bool IsSpam { get; set; }
        public bool Reviewed { get; set; }
        public bool EditedByReconGuideJob { get; set; }
        public int? BkpVendorId { get; set; }
        public string IntegrationId { get; set; }
        public string ToIntegrationId { get; set; }
        public BkpVendor BkpVendor { get; set; }
        public int? BkpAccountDebitId { get; set; }
        public BkpAccount BkpAccountDebit { get; set; }
        public int? BkpAccountCreditId { get; set; }
        public BkpAccount BkpAccountCredit { get; set; }
        public Credential Credential { get; set; }
        public List<SpecIdMatch> SpecIdMatchesAsComponent { get; set; }
        public List<SpecIdMatch> SpecIdMatchesAsSell { get; set; }
        public int? AccountId { get; set; }
        public int? ToAccountId { get; set; }
        public bool? HasMovement { get; set; }
    }

    public class BkpVendor
    {
        public int? Id { get; set; }
        public int? BkpIntegrationId { get; set; }
        public string BkpIntegrationDisplayName { get; set; }
    }

    public class BkpAccount
    {
        public int? Id { get; set; }
        public int? BkpIntegrationId { get; set; }
        public string Name { get; set; }
    }

    public class Credential
    {
        public int? AccountId { get; set; }
        public string CredentialType { get; set; }
        public string Source { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string IntegrationId { get; set; }
    }

    public class SpecIdMatch
    {
        public int? ComponentId { get; set; }
        public int? SellId { get; set; }
        public double? ComponentQuantity { get; set; }
        public int? ComponentOrder { get; set; }
    }
}