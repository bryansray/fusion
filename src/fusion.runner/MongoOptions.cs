namespace Fusion.Runner;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string? ConnectionString { get; set; }

    public string DatabaseName { get; set; } = "fusion";

    public string QuotesCollectionName { get; set; } = "quotes";
}
