using DataAnamolyDetection;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

public class InvoiceAnomalyClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _key;

    public InvoiceAnomalyClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _endpoint = configuration["AzureML:Endpoint"];
        _key = configuration["AzureML:Key"];

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _key);
    }

    public async Task<List<AnomalyResult>> ScoreAsync(List<InvoiceFeature> invoices)
    {
        var payload = invoices.Select(i => i.ToMlVector()).ToList();

        var json = JsonConvert.SerializeObject(payload);

        var response = await _httpClient.PostAsync(
            _endpoint,
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        response.EnsureSuccessStatusCode();

        var resultJson = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<List<AnomalyResult>>(resultJson);
    }
}