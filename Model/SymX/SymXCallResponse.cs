using System.Text.Json.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class SymXCallResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; }
    
    [JsonPropertyName("httpStatusCode")]
    public int HttpStatusCode { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; }
    
    [JsonPropertyName("symxCallId")]
    public string SymxCallId { get; set; }
    
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; }
    
}