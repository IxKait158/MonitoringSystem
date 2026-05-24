using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.ApiKeys;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/keys")]
public class ApiKeysController(IApiKeysService apiKeysService) : ControllerBase
{
    /// <summary>
    /// Генерує новий API ключ для сервісу
    /// POST /api/keys
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request)
    {
        try
        {
            var key = await apiKeysService.CreateAsync(request);
            return Ok(new
            {
                apiKey = key,
                serviceName = request.ServiceName,
                message = "Збережіть цей ключ — він більше не буде показаний повністю",
                usage = $"Додавати до запитів заголовок: X-API-KEY: {key}"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Список всіх ключів (без показу самого ключа повністю)
    /// GET /api/keys
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        try
        {
            var keys = apiKeysService.GetAll();
            return Ok(keys);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Деактивувати ключ
    /// DELETE /api/keys/{id}
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Revoke(int id)
    {
        try
        {
            var key = await apiKeysService.DeactivateApiKeyAsync(id);
            return Ok(new
            {
                message = $"Ключ для {key.ServiceName} деактивовано"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}