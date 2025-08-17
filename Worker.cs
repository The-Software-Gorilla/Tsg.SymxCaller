using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Tsg.Rdc.Model.SymX;

namespace Tsg.SymxCaller;

public class Worker : BackgroundService
{
    private const string PartitionKey = "symxCall";
    private readonly ILogger<Worker> _logger;
    private readonly QueueClient _queue;
    private readonly QueueClient _poisonQueue;
    private readonly TimeSpan _visibility;
    private readonly TimeSpan _pollDelay;
    private readonly int _maxDequeue;
    private readonly TableClient _tableClient;
    private readonly string _endpointKey;
    private readonly string _storageConnectionString;

    public Worker(ILogger<Worker> logger, IConfiguration cfg)
    {
        _logger = logger;
        _storageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
                   ?? cfg["Queue:ConnectionString"]
                   ?? throw new InvalidOperationException("QUEUE_CONN not set");

        var queueName = Environment.GetEnvironmentVariable("QUEUE_NAME")
                   ?? cfg["Queue:Name"] ?? "deposit-inbound";
        
        var tableName = Environment.GetEnvironmentVariable("TABLE_NAME")
                   ?? cfg["Table:Name"] ?? "depositTransaction";
        
        _endpointKey = Environment.GetEnvironmentVariable("ENDPOINT_KEY")
                       ?? cfg["SymX:EndpointKey"] ?? string.Empty;

        _visibility = TimeSpan.FromSeconds(
            int.TryParse(Environment.GetEnvironmentVariable("VISIBILITY_TIMEOUT_SEC"),
                out var vts) ? vts : int.Parse(cfg["Queue:VisibilityTimeoutSeconds"] ?? "60"));

        _pollDelay = TimeSpan.FromMilliseconds(
            int.TryParse(Environment.GetEnvironmentVariable("POLL_DELAY_MS"),
                out var pd) ? pd : int.Parse(cfg["Queue:PollDelayMilliseconds"] ?? "1500"));

        _maxDequeue = int.TryParse(Environment.GetEnvironmentVariable("MAX_DEQUEUE"),
            out var md) ? md : int.Parse(cfg["Queue:MaxDequeueBeforePoison"] ?? "5");

        _queue = new QueueClient(_storageConnectionString, queueName, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        _poisonQueue = new QueueClient(_storageConnectionString, $"{queueName}-poison", new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        _tableClient = new TableClient(_storageConnectionString, tableName);

        _queue.CreateIfNotExists();
        _poisonQueue.CreateIfNotExists();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue reader started. queue={queue}", _queue.Name);

        // main loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Up to 16 at a time, with a visibility lock
                QueueMessage[] messages = await _queue.ReceiveMessagesAsync(
                    maxMessages: 16,
                    visibilityTimeout: _visibility,
                    cancellationToken: stoppingToken);

                if (messages.Length == 0)
                {
                    await Task.Delay(_pollDelay, stoppingToken);
                    continue;
                }

                foreach (var msg in messages)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        // Your message is a transactionId string (per your upstream function)
                        var callId = msg.MessageText;
                        _logger.LogInformation("Received: {txId} (dequeue={cnt})", callId, msg.DequeueCount);

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
                            await _queue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, stoppingToken);
                            continue;
                        }
                        
                        if (!entity.TryGetValue("Call", out var raw) || raw is not string callJson || string.IsNullOrWhiteSpace(callJson))
                        {
                            _logger.LogError("Entity found but 'Xml' property is missing/empty. CallId={CallId}", callId);
                            continue;
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
                            await callbackQueue.SendMessageAsync(response ?? string.Empty, stoppingToken);
                            await _queue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, stoppingToken);
                        }
                        else
                        {
                            if (msg.DequeueCount + 1 >= _maxDequeue)
                            {
                                await _poisonQueue.SendMessageAsync(msg.MessageText, stoppingToken);
                            }
                        }   
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing msgId={Id} dequeue={Count}", msg.MessageId, msg.DequeueCount);

                        // Move to poison if it’s looping too many times
                        if (msg.DequeueCount + 1 >= _maxDequeue)
                        {
                            await _poisonQueue.SendMessageAsync(msg.MessageText, stoppingToken);
                            await _queue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, stoppingToken);
                            _logger.LogWarning("Moved message to poison queue: {Id}", msg.MessageId);
                        }
                    }
                }
            }
            catch (TaskCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queue poll failed; backing off");
                // brief backoff on unexpected failures
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Queue reader stopping.");
        
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
