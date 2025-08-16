using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class Request
{
    [XmlAttribute(AttributeName = "BranchId")]
    [JsonPropertyName("branchId")]
    public int BranchId { get; set; }

    [XmlElement(ElementName = "Credentials")]
    [JsonPropertyName("credentials")]
    public Credentials Credentials { get; set; }

    [XmlElement(ElementName = "DeviceInformation")]
    [JsonPropertyName("deviceInformation")]
    public DeviceInformation DeviceInformation { get; set; }

    [XmlElement(ElementName = "Header")]
    [JsonPropertyName("header")]
    public RequestHeader Header { get; set; }

    [XmlElement(ElementName = "Body")]
    [JsonPropertyName("body")]
    public RequestBody Body { get; set; }
    
}