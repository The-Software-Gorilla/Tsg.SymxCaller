using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class RGUserChr
{
    [XmlElement(ElementName = "ID")]
    [JsonPropertyName("id")]
    public int ID { get; set; }

    [XmlElement(ElementName = "Value")]
    [JsonPropertyName("value")]
    public string Value { get; set; }
}