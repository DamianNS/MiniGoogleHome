using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Backend.Controllers
{
    [Route("api/devices")]
    [ApiController]
    public class DevicesController(IDbContextFactory<SmartHomeDbContext> dbContextFactory) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetDevices()
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            var devices = context.Minis.Include(m => m.Usuarios);

            var devicesList = devices
               .Select(device => new
               {
                   device.Id,
                   device.Nombre,
                   device.Estado,
                   Usuarios = device.Usuarios.Select(u => new
                   {
                       u.Id,
                       u.Username,
                       u.AgentUserId
                   }).ToList()
               });

            return Ok(devicesList.ToList());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDevice(int id)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            var device = await context.Minis.FindAsync(id);
            if (device == null)
            {
                return NotFound();
            }
            return Ok(device);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDevice([FromBody] MiniDTO device)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            context.Minis.Add(device);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, device);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDevice(int id, [FromBody] MiniDTO device)
        {
            if (id != device.Id)
            {
                return BadRequest();
            }
            using var context = await dbContextFactory.CreateDbContextAsync();
            context.Entry(device).State = EntityState.Modified;
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            var device = await context.Minis.FindAsync(id);
            if (device == null)
            {
                return NotFound();
            }
            context.Minis.Remove(device);
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/user/{userId}")]
        public async Task<IActionResult> AssignUserToDevice(int id, int userId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();

            // 1. Buscamos el dispositivo con sus usuarios
            var device = await context.Minis.Include(d => d.Usuarios).FirstOrDefaultAsync(d => d.Id == id);
            if (device == null) return NotFound("Dispositivo no encontrado.");

            // 2. Buscamos el usuario
            var user = await context.Usuarios.FindAsync(userId);
            if (user == null) return NotFound("Usuario no encontrado.");

            // 3. Validación: Evitamos duplicar la relación en memoria
            if (device.Usuarios.Any(u => u.Id == userId))
            {
                return BadRequest("El usuario ya está asignado a este dispositivo.");
            }

            // 4. Agregamos de forma segura (Usuarios nunca será null si se inicializó en la entidad)
            device.Usuarios.Add(user);

            // 5. ¡IMPORTANTE! Guardar en la base de datos
            await context.SaveChangesAsync();

            return Ok("Usuario asignado correctamente.");
        }
    }
}
