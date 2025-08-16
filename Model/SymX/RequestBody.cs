using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class RequestBody
{
    [XmlElement(ElementName = "File")]
    [JsonPropertyName("file")]
    public string File { get; set; }

    [XmlElement(ElementName = "RGSession")]
    [JsonPropertyName("rgSession")]
    public int RGSession { get; set; }

    [XmlElement(ElementName = "UserDefinedParameters")]
    [JsonPropertyName("userDefinedParameters")]
    public UserDefinedParameters UserDefinedParameters { get; set; }

    [XmlElement(ElementName = "User")]
    [JsonPropertyName("user")]
    public string User { get; set; }
}
