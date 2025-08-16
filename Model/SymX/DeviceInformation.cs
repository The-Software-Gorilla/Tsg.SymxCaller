using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class DeviceInformation
{
    [XmlAttribute(AttributeName = "DeviceType")]
    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; }

    [XmlAttribute(AttributeName = "DeviceNumber")]
    [JsonPropertyName("deviceNumber")]
    public int DeviceNumber { get; set; }
}
