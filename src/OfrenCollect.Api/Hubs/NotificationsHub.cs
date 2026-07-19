using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OfrenCollect.Infrastructure.Auth;

namespace OfrenCollect.Api.Hubs;

/// <summary>
/// Real-time channel to the SPA. On connect, each client joins a per-tenant group (from its
/// token) so reconciliation events reach only the owning tenant (§11.3). Server-to-client push
/// only; the client subscribes and never invokes server methods.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public static string TenantGroup(Guid tenantId) => $"tenant-{tenantId}";

    public override async Task OnConnectedAsync()
    {
        var value = Context.User?.FindFirst(JwtTokenService.TenantIdClaim)?.Value;
        if (Guid.TryParse(value, out var tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
        }

        await base.OnConnectedAsync();
    }
}
