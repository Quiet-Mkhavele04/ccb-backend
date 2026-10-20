using System.Collections.Concurrent;
using System.Security.Claims;
using CCB.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly
        ConcurrentDictionary<int, int>
        ConnectionCounts = new();

    private readonly CCBDbContext _context;

    public ChatHub(CCBDbContext context)
    {
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var profileId = GetCurrentProfileId();

        if (profileId != null)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetProfileGroup(profileId.Value)
            );

            var connectionCount =
                ConnectionCounts.AddOrUpdate(
                    profileId.Value,
                    1,
                    (_, currentCount) =>
                        currentCount + 1
                );

            // Only broadcast when the user's first
            // active connection is opened.
            if (connectionCount == 1)
            {
                await SetPresenceAsync(
                    profileId.Value,
                    true
                );
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception
    )
    {
        var profileId = GetCurrentProfileId();

        if (profileId != null)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                GetProfileGroup(profileId.Value)
            );

            var finalConnectionClosed =
                RemoveConnection(
                    profileId.Value
                );

            // A user stays online while another phone,
            // browser or app connection remains open.
            if (finalConnectionClosed)
            {
                await SetPresenceAsync(
                    profileId.Value,
                    false
                );
            }
        }

        await base.OnDisconnectedAsync(
            exception
        );
    }

    private async Task SetPresenceAsync(
        int profileId,
        bool isOnline
    )
    {
        var contactRows =
            await _context.Contacts
                .Where(contact =>
                    contact.ProfileId ==
                        profileId &&
                    contact.IsOnline !=
                        isOnline
                )
                .ToListAsync();

        foreach (var contact in contactRows)
        {
            contact.IsOnline = isOnline;
        }

        if (contactRows.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        await Clients.All.SendAsync(
            "PresenceChanged",
            new
            {
                profileId,
                isOnline
            }
        );
    }

    private static bool RemoveConnection(
        int profileId
    )
    {
        while (
            ConnectionCounts.TryGetValue(
                profileId,
                out var currentCount
            )
        )
        {
            if (currentCount <= 1)
            {
                if (
                    ((ICollection<
                        KeyValuePair<int, int>
                    >)ConnectionCounts).Remove(
                        new KeyValuePair<int, int>(
                            profileId,
                            currentCount
                        )
                    )
                )
                {
                    return true;
                }

                continue;
            }

            if (
                ConnectionCounts.TryUpdate(
                    profileId,
                    currentCount - 1,
                    currentCount
                )
            )
            {
                return false;
            }
        }

        return false;
    }

    private int? GetCurrentProfileId()
    {
        var profileIdValue =
            Context.User?.FindFirstValue(
                "profileId"
            );

        return int.TryParse(
            profileIdValue,
            out var profileId
        )
            ? profileId
            : null;
    }

    private static string GetProfileGroup(
        int profileId
    )
    {
        return $"profile-{profileId}";
    }
}
