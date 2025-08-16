using System.Text.Json.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class SymXCall
{
    [JsonPropertyName("symXCallId")]
    public string SymXCallId { get; set; }
    
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; }
    
    [JsonPropertyName("callbackQueue")]
    public string CallbackQueue { get; set; }
    
    [JsonPropertyName("symXInstanceUrl")]
    public string SymXInstanceUrl { get; set; }
    
    [JsonPropertyName("symXPowerOn")]
    public string SymXPowerOn { get; set; }
    
    [JsonPropertyName("symXEnvelope")]
    public SymXSoapEnvelope SymXEnvelope { get; set; }
}