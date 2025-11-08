using Bogus;
using Fusion.Persistence.Models;
using Xunit;

namespace Fusion.Persistence.Tests;

public sealed class QuoteDocumentTests
{
    private static readonly Faker Faker = new();

    [Fact]
    public void ConstructorGeneratesShortId()
    {
        var document = new QuoteDocument
        {
            Person = Faker.Name.FullName(),
            Message = Faker.Lorem.Sentence()
        };

        Assert.False(string.IsNullOrWhiteSpace(document.ShortId));
    }
}
