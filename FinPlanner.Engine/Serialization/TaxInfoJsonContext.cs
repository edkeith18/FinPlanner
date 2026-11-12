using System.Text.Json.Serialization;

namespace FinPlanner.Engine;

// Generate collection factories explicitly so browser Release builds do not need
// reflection to discover ImmutableArray<Bracket>.CreateRange support.
[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(TaxInfo))]
internal partial class TaxInfoJsonContext : JsonSerializerContext
{
}
