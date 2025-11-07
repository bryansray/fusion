using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace Fusion.Runner;

public static class MongoConfiguration
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true)
        };

        ConventionRegistry.Register("fusion-conventions", pack, _ => true);

        _configured = true;
    }
}
