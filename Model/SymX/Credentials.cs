using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class Credentials
{
    [XmlAttribute(AttributeName = "ProcessorUser", Namespace = "http://www.symxchange.generated.symitar.com/common/dto/common")]
    [JsonPropertyName("processorUser")]
    public string ProcessorUser { get; set; }

    [XmlElement(ElementName = "AdministrativeCredentials")]
    [JsonPropertyName("administrativeCredentials")]
    public AdministrativeCredentials AdministrativeCredentials { get; set; }
    
}