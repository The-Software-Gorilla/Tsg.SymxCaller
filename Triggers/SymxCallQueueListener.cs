using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tsg.Models.SymX;

namespace Tsg.SymxCaller.Triggers;

public class SymxCallQueueListener
{
    private const string PartitionKey = "symxCall";
    private const string TableName = "symxOutbound";
    private const string QueueName = "symx-outbound";
    private const string StorageConnectionStringEnvVar = "AzureWebJobsStorage";
    private readonly ILogger<SymxCallQueueListener> _logger;
    private readonly TableClient _tableClient;
    private readonly string _endpointKey;
    private readonly string _storageConnectionString;

    public SymxCallQueueListener(ILogger<SymxCallQueueListener> logger, IConfiguration cfg)
    {
        _logger = logger;
        _storageConnectionString = Environment.GetEnvironmentVariable(StorageConnectionStringEnvVar)
                   ?? cfg["Queue:ConnectionString"]
                   ?? throw new InvalidOperationException("QUEUE_CONN not set");

        
        _endpointKey = Environment.GetEnvironmentVariable("ENDPOINT_KEY")
                       ?? cfg["SymX:EndpointKey"] ?? string.Empty;
        
        _tableClient = new TableClient(_storageConnectionString, TableName);
    }
    
    [Function(nameof(SymxCallQueueListener))]
    public async Task Run([QueueTrigger(QueueName, Connection = StorageConnectionStringEnvVar)] QueueMessage queueMessage)
    {
        var callId = queueMessage.MessageText;
        _logger.LogInformation("Received: {txId} (dequeue={cnt})", callId, queueMessage.DequeueCount);

        // Get the corresponding Table entity
        TableEntity entity;
        try
        {
            var resp = _tableClient.GetEntity<TableEntity>( PartitionKey, callId);
            entity = resp.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError(ex, "No table entity found for PK={PK} RK={RK}", PartitionKey, callId);
            // If the entity is not found, we might want to consider this message processed
            // to avoid infinite retries. Adjust based on your requirements.
            return;
        }
        
        if (!entity.TryGetValue("Call", out var raw) || raw is not string callJson || string.IsNullOrWhiteSpace(callJson))
        {
            _logger.LogError("Entity found but 'Xml' property is missing/empty. CallId={CallId}", callId);
            return;
        }

        var symxCall = JsonSerializer.Deserialize<SymXCall>(callJson);
        
        _logger.LogInformation("Processing SymXCallId={SymXCallId} CorrelationId={CorrelationId}",
            symxCall.SymXCallId, symxCall.CorrelationId);
        
        var soapMessage = ConvertToSoap(symxCall.SymXEnvelope);
        _logger.LogInformation("Converted SOAP message:\n{Soap}", soapMessage);

        string response;
        bool success = false;
        try
        {
            response = await CallSymxApiAsync(symxCall, soapMessage);
            success = true;
            _logger.LogInformation("SymX API response length: {Len}\n{Response}", response?.Length ?? 0, response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to SymX API failed {ErrorCode} - {ErrorMessage} for SymXCallId={SymXCallId}", ex.StatusCode, ex.Message, symxCall.SymXCallId);
            success = false;
            var errorObj = new SymXCallResponse()
            {
                Status = "error",
                HttpStatusCode = (int) ex.StatusCode!,
                Timestamp = DateTime.UtcNow,
                Message = ex.Message,
                SymxCallId = symxCall.SymXCallId,
                CorrelationId = symxCall.CorrelationId
            };
            response = JsonSerializer.Serialize(errorObj);
        }
        
        // Update Table entity with response/status
        if (!entity.TryGetValue("Attempts", out var attemptsObj) || attemptsObj is not int attempts)
        {
            attempts = 1;
        }
        else
        {
            attempts += 1;
        }
        entity["Attempts"] = attempts;
        entity["LastUpdatedUtc"] = DateTime.UtcNow;
        entity["Status"] = success ? "processed" : "error";
        entity["Response"] = response;
        await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
        
        
        // Queue a message to the callback queue
        if (success)
        {
            var callbackQueue = new QueueClient(
                _storageConnectionString,
                symxCall.CallbackQueue,
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
            await callbackQueue.CreateIfNotExistsAsync();
            await callbackQueue.SendMessageAsync(response ?? string.Empty);
            // await _queue.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
        }
    }
    
    private string ConvertToSoap(SymXSoapEnvelope envelope)
    {
        var serializer = new XmlSerializer(typeof(SymXSoapEnvelope));
        string soap;
        
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), // false = no BOM
            Indent = true
        };
        using (var stream = new MemoryStream())
        using (var writer = XmlWriter.Create(stream, settings))
        {
            serializer.Serialize(writer, envelope);
            soap = Encoding.UTF8.GetString(stream.ToArray());
        }
        return soap;
    }
    
    private async Task<string> CallSymxApiAsync(SymXCall call, string soapMessage)
    {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, call.SymXInstanceUrl)
        {
            Content = new StringContent(soapMessage, Encoding.UTF8, "application/xml")
        };
        request.Headers.Add("x-Correlation-Id", call.CorrelationId ?? string.Empty);
        request.Headers.Add("x-SymX-Call-ID", call.SymXCallId ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(_endpointKey))
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", _endpointKey);
        }

        _logger.LogInformation("Calling SymX API at {url} with SOAP message of length {len}", call.SymXInstanceUrl, soapMessage.Length);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
