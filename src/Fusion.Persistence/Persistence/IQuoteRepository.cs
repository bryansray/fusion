using Fusion.Persistence.Models;

namespace Fusion.Persistence;

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

    Task<int?> IncrementLikesAsync(string shortId, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(string shortId, ulong deletedBy, CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(string shortId, ulong restoredBy, CancellationToken cancellationToken = default);
}
