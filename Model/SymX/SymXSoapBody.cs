using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class SymXSoapBody
{
    [XmlElement(ElementName = "executePowerOnReturnArray", Namespace = "http://www.symxchange.generated.symitar.com/poweron")]
    [JsonPropertyName("executePowerOnReturnArray")]
    public ExecutePowerOnReturnArray ExecutePowerOnReturnArray { get; set; }
}