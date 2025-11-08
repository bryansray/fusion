using Fusion.Runner.Persistence.Models;

namespace Fusion.Runner.Persistence;

public interface IQuoteRepository
{
    Task InsertAsync(QuoteDocument quote, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteDocument>> FindByAuthorAsync(string author, CancellationToken cancellationToken = default);
}
