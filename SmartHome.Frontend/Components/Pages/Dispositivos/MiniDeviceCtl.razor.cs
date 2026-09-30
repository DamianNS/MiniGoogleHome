using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Frontend.Components.Pages.Dispositivos
{
    public partial class MiniDeviceCtl : ComponentBase
    {
        [Parameter]
        public int DeviceId { get; set; }

        [Inject]
        IDbContextFactory<SmartHomeDbContext> DbContextFactory { get; set; } = default!;

        private MiniDTO? DeviceData { get; set; }

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if(firstRender)
            {
                // Load the device data from the database
                using var context = DbContextFactory.CreateDbContext();
                var device = context.Minis.Include(m => m.Usuarios).FirstOrDefault(m => m.Id == DeviceId);
                if (device != null)
                {                    
                    this.DeviceData = device;
                    StateHasChanged();
                }
            }
            return base.OnAfterRenderAsync(firstRender);
        }
    }
}
