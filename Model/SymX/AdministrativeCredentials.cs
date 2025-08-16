using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Tsg.Rdc.Model.SymX;

public class AdministrativeCredentials
{
    [XmlElement(ElementName = "Password")]
    [JsonPropertyName("password")]
    public string Password { get; set; }
    
}