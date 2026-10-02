using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using SmartHome.Frontend.Services;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Frontend.Components.Pages.Dispositivos
{
    public partial class MiniDeviceCtl : ComponentBase
    {
        [Parameter]
        public int DeviceId { get; set; }

        [Inject]
        ApiService api { get; set; } = default!;

        [Inject]
        IDbContextFactory<SmartHomeDbContext> DbContextFactory { get; set; } = default!;

        private MiniDTO? DeviceData { get; set; }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if(firstRender)
            {
                try
                {
                    this.DeviceData = await api.GetDevice(DeviceId);
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    // Manejo de la excepción, por ejemplo hacer log o mostrar un mensaje
                    Console.WriteLine($"Error al obtener el dispositivo: {ex.Message}");
                }
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        protected async Task PlayCommand()
        {
            if (DeviceData == null) return;

            try
            {
                using var context = await DbContextFactory.CreateDbContextAsync();
                context.Attach(DeviceData);
                DeviceData.Estado = SmartHome.Shared.Constantes.EstadoEnum.Play;
                await context.SaveChangesAsync();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar el estado a Play: {ex.Message}");
            }
        }

        private bool isEditingName = false;
        private string editNameValue = "";

        private void StartEditing()
        {
            if (DeviceData != null)
            {
                editNameValue = DeviceData.Nombre;
                isEditingName = true;
            }
        }

        private void CancelEditing()
        {
            isEditingName = false;
        }

        private async Task SaveName()
        {
            if (DeviceData == null || string.IsNullOrWhiteSpace(editNameValue))
            {
                isEditingName = false;
                return;
            }

            try
            {
                using var context = await DbContextFactory.CreateDbContextAsync();
                context.Attach(DeviceData);
                DeviceData.Nombre = editNameValue;
                await context.SaveChangesAsync();
                
                isEditingName = false;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar el nombre: {ex.Message}");
            }
        }
    }
}
