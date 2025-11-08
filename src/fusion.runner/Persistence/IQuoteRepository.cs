using Fusion.Runner.Persistence.Models;

namespace Fusion.Runner.Persistence;

public interface IQuoteRepository
{
    Task InsertAsync(QuoteDocument quote, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteDocument>> FindByAuthorAsync(string author, CancellationToken cancellationToken = default);

    Task<QuoteDocument?> GetByShortIdAsync(string shortId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteDocument>> GetFuzzyShortIdAsync(string shortIdPrefix, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteDocument>> SearchAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task IncrementUsesAsync(string shortId, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(string shortId, ulong deletedBy, CancellationToken cancellationToken = default);
}
