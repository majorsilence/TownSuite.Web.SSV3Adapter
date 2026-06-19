using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

public class AllPropertiesResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        if (property.Ignored)
        {
            // The member was explicitly excluded (e.g. [JsonIgnore]). Surface it for SS v3
            // backwards-compatible output, but never let it be populated from request input -
            // doing so reintroduces overposting / mass-assignment of server-controlled fields.
            property.Ignored = false;
            property.Writable = false;
        }

        return property;
    }
}