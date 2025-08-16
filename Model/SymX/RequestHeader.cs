using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class RequestHeader
{
    [XmlElement(ElementName = "MessageID")]
    [JsonPropertyName("messageID")]
    public string MessageID { get; set; }
}