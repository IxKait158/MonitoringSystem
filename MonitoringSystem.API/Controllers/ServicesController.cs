using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Services;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController(IServicesService servicesService) : ControllerBase
{
    private ApiKeyEntity CurrentApiKey =>
        (ApiKeyEntity)HttpContext.Items["ApiKey"]!;

    /// <summary>
    /// Зареєструвати новий сервіс під поточним API-ключем
    /// POST /api/services
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request)
    {
        try
        {
            var service = await servicesService.CreateAsync(CurrentApiKey, request);
            return Ok(service);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Список сервісів поточного користувача
    /// GET /api/services
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var services = await servicesService.GetAllAsync(CurrentApiKey);
            return Ok(services);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Видалити сервіс (тільки якщо належить поточному ключу)
    /// DELETE /api/services/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await servicesService.DeleteAsync(CurrentApiKey, id);
            return Ok(new { message = "Сервіс видалено" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
