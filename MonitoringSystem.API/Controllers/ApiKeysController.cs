using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.ApiKeys;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/keys")]
public class ApiKeysController(IApiKeysService apiKeysService) : ControllerBase
{
    private ApiKeyEntity? CurrentApiKey =>
        HttpContext.Items["ApiKeyDTO"] as ApiKeyEntity;

    /// <summary>
    /// Створює новий API ключ для користувача. Не потребує авторизації.
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
                owner = string.IsNullOrWhiteSpace(request.Owner) ? "unknown" : request.Owner,
                message = "Збережіть цей ключ — він більше не буде показаний повністю",
                usage = $"Додавайте до запитів заголовок: X-API-KEY: {key}"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Інформація про поточний API ключ (за заголовком X-API-KEY).
    /// GET /api/keys/me
    /// </summary>
    [HttpGet("me")]
    public IActionResult GetCurrent()
    {
        try
        {
            if (CurrentApiKey == null)
                return Unauthorized(new { message = "API ключ відсутній" });

            return Ok(apiKeysService.GetCurrent(CurrentApiKey));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Деактивувати поточний ключ (можна тільки свій).
    /// DELETE /api/keys/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id)
    {
        try
        {
            if (CurrentApiKey == null || CurrentApiKey.Id != id)
                return Forbid();

            var key = await apiKeysService.DeactivateApiKeyAsync(id);
            return Ok(new { message = $"Ключ #{key.Id} деактивовано" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
