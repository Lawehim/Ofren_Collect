using MediatR;
using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Application.Dashboard.GetDashboard;

public sealed class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardResponse>
{
    private readonly IDashboardReader _reader;

    public GetDashboardQueryHandler(IDashboardReader reader) => _reader = reader;

    public Task<DashboardResponse> Handle(GetDashboardQuery query, CancellationToken cancellationToken) =>
        _reader.GetAsync(cancellationToken);
}
