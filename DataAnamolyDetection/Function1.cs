using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataAnamolyDetection;

public class Function1
{
    private readonly ILogger<Function1> _logger;
    private readonly InvoiceAnomalyClient _anomalyClient;

    public Function1(ILogger<Function1> logger, InvoiceAnomalyClient anomalyClient)
    {
        _logger = logger;
        _anomalyClient = anomalyClient;
    }


    [Function("Function1")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        try
        {

            string connectionString = $"AuthType=ClientSecret; url=https://end2endtesting.crm4.dynamics.com; ClientId=73a68c5f-0234-4d92-b110-4a82d6348076; ClientSecret=iD~8Q~W2WvdhHl3X_b36ZIHUdCMYc5nbhhll_c-F";
            var client = new ServiceClient(connectionString);

            if (!client.IsReady)
                throw new Exception("Failed to connect to CRM.");

            var query = new QueryExpression("invoice")
            {
                ColumnSet = new ColumnSet(true),
            };
            //query.AddOrder("modifiedon", OrderType.Ascending);
            var invoiceRecord = client.RetrieveMultiple(query).Entities;

            //var features = new List<InvoiceFeature>();

            // Pre-allocate for better performance
            var features = new List<InvoiceFeature>(invoiceRecord.Count);

            // 1?? Collect all invoice IDs
            var invoiceIds = invoiceRecord.Select(r => r.Id).ToList();

            // 2?? Fetch all line counts in ONE call
            var lineCounts = GetInvoiceLineCounts(client, invoiceIds);
            // Dictionary<Guid, int>

            foreach (var record in invoiceRecord)
            {
                // Cache attribute lookups once
                var totalMoney = record.GetAttributeValue<Money>("totalamount");
                var taxMoney = record.GetAttributeValue<Money>("taxamount");
                var discountMoney = record.GetAttributeValue<Money>("discountamount");

                var total = totalMoney?.Value ?? 0m;
                var tax = taxMoney?.Value ?? 0m;
                var discount = discountMoney?.Value ?? 0m;

                // Dictionary lookup instead of service call
                int lineCount = lineCounts.TryGetValue(record.Id, out var count)
                                    ? count
                                    : 0;

                decimal avgLine = lineCount == 0 ? 0m : total / lineCount;

                features.Add(new InvoiceFeature
                {
                    TotalAmount = total,
                    TaxAmount = tax,
                    DiscountAmount = discount,
                    LineCount = lineCount,
                    AvgLineAmount = avgLine
                });
            }
            var anomalyClient = new InvoiceAnomalyClient(
                new HttpClient(),
                config
            );

            var anomalyResults = await anomalyClient.ScoreAsync(features);

            // Merge results back
            for (int i = 0; i < features.Count; i++)
            {
                if (anomalyResults[i].IsAnomaly)
                {
                    _logger.LogWarning(
                        $"Anomalous invoice detected: {features[i].InvoiceId}, Score={anomalyResults[i].AnomalyScore}"
                    );

                    // 🔴 Optionally:
                    // - Update Dataverse invoice
                    // - Block approval
                    // - Raise alert
                }
            }
            Console.WriteLine($"Extracted features for {features.Count} invoices.");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to CRM");
            throw;
        }
        return new OkObjectResult("Welcome to Azure Functions!");
    }
    private Dictionary<Guid, int> GetInvoiceLineCounts(
    IOrganizationService service,
    List<Guid> invoiceIds)
    {
        var result = new Dictionary<Guid, int>();

        if (!invoiceIds.Any())
            return result;

        var fetchXml = $@"
    <fetch aggregate='true'>
      <entity name='invoicedetail'>
        <attribute name='invoiceid' groupby='true' alias='invoiceid' />
        <attribute name='invoicedetailid' aggregate='count' alias='linecount' />
        <filter>
          <condition attribute='invoiceid' operator='in'>
            {string.Join("", invoiceIds.Select(id => $"<value>{id}</value>"))}
          </condition>
        </filter>
      </entity>
    </fetch>";

        var response = service.RetrieveMultiple(new FetchExpression(fetchXml));

        foreach (var entity in response.Entities)
        {
            var invoiceRef = (EntityReference)((AliasedValue)entity["invoiceid"]).Value;
            var invoiceId = invoiceRef.Id;

            var count = Convert.ToInt32(((AliasedValue)entity["linecount"]).Value);

            result[invoiceId] = count;
        }

        return result;
    }

}