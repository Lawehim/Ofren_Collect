using OfrenCollect.Application.Assistant;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// The provider-agnostic language-model boundary. Its only job is to map a natural-language
/// question to one <see cref="CollectionsIntent"/> — it never sees tenant data, never runs a
/// query, and never returns a figure. Implementations are swappable by configuration (FR-7.4);
/// anything it cannot confidently classify must return <see cref="CollectionsIntent.Unknown"/>.
/// </summary>
public interface IIntentClassifier
{
    Task<CollectionsIntent> ClassifyAsync(string question, CancellationToken cancellationToken);
}
