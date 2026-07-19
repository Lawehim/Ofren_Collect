using System.Globalization;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Application.Assistant;

/// <summary>
/// Orchestrates a grounded answer: classify the question with the model, then run the real
/// tenant-scoped query and phrase the result. The model never touches data, so no answer is ever
/// invented; an unrecognised question is declined (FR-7.3).
/// </summary>
public sealed class AiAssistant : IAiAssistant
{
    private const string Declined =
        "I can't answer that yet — try asking how much you've collected this week, "
        + "or who's overdue or underpaid.";

    private readonly IIntentClassifier _classifier;
    private readonly IAssistantData _data;
    private readonly TimeProvider _clock;

    public AiAssistant(IIntentClassifier classifier, IAssistantData data, TimeProvider clock)
    {
        _classifier = classifier;
        _data = data;
        _clock = clock;
    }

    public async Task<AssistantAnswer> AskAsync(string question, CancellationToken cancellationToken)
    {
        var intent = await _classifier.ClassifyAsync(question, cancellationToken);

        return intent switch
        {
            CollectionsIntent.CollectedThisWeek => Grounded(
                intent,
                $"You've collected {Naira(await _data.CollectedSinceAsync(StartOfCurrentWeek(), cancellationToken))} so far this week."),

            CollectionsIntent.OverdueCustomers => Grounded(
                intent,
                Count(await _data.OverdueSubscriptionCountAsync(cancellationToken), "subscription", "overdue")),

            CollectionsIntent.UnderpaidCustomers => Grounded(
                intent,
                Count(await _data.UnderpaidInvoiceCountAsync(cancellationToken), "invoice", "underpaid")),

            CollectionsIntent.ActiveSubscriptions => Grounded(
                intent,
                Have(await _data.ActiveSubscriptionCountAsync(cancellationToken), "active subscription")),

            CollectionsIntent.UnmatchedPayments => Grounded(
                intent,
                Have(await _data.UnmatchedPaymentCountAsync(cancellationToken), "unmatched payment")),

            _ => new AssistantAnswer(Declined, Grounded: false, nameof(CollectionsIntent.Unknown)),
        };
    }

    private static AssistantAnswer Grounded(CollectionsIntent intent, string text) =>
        new(text, Grounded: true, intent.ToString());

    private static string Naira(decimal amount) => "₦" + amount.ToString("N0", CultureInfo.InvariantCulture);

    private static string Count(int count, string noun, string state) =>
        $"{count} {Pluralise(count, noun)} {(count == 1 ? "is" : "are")} {state}.";

    private static string Have(int count, string noun) =>
        $"You have {count} {Pluralise(count, noun)}.";

    private static string Pluralise(int count, string noun) => count == 1 ? noun : noun + "s";

    private DateTimeOffset StartOfCurrentWeek()
    {
        var now = _clock.GetUtcNow();
        var daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        return new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-daysSinceMonday);
    }
}
