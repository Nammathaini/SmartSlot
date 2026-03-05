using Microsoft.AspNetCore.SignalR;

namespace SmartSlot.Hubs
{
    public class ParkingHub : Hub
    {
        public async Task JoinMap()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "map");
        }
    }
}