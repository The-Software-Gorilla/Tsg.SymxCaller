using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

[XmlRoot(ElementName = "Envelope", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
public class SymXSoapEnvelope
{
    [XmlElement(ElementName = "Header", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    [JsonPropertyName("header")]
    public SymXSoapHeader Header { get; set; }

    [XmlElement(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    [JsonPropertyName("body")]
    public SymXSoapBody Body { get; set; }
}