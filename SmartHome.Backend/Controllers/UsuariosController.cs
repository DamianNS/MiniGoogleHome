using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Persistence;

namespace SmartHome.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsuariosController(IDbContextFactory<SmartHomeDbContext> dbContextFactory) : ControllerBase
    {
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "sub");
            if (userIdClaim == null)
            {
                return Unauthorized();
            }
            var userId = int.Parse(userIdClaim.Value);

            using var context = dbContextFactory.CreateDbContext();

            var user = context.Usuarios
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.Username
                })
                .FirstOrDefault();
            return Ok(user);
        }

        [HttpGet("{id:int}")]
        public IActionResult GetUser(int id)
        {
            using var context = dbContextFactory.CreateDbContext();

            var user = context.Usuarios.Find(id);
            if (user == null)
            {
                return NotFound();
            }

            var ret = new 
            {
                user.Id,
                user.Username
            };
            return Ok(ret);
        }        
    }
}
