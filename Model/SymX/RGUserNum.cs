using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class RGUserNum
{
    [XmlElement(ElementName = "ID")]
    [JsonPropertyName("id")]
    public int ID { get; set; }

    [XmlElement(ElementName = "Value")]
    [JsonPropertyName("value")]
    public int Value { get; set; }
}