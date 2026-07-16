using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class PermissionsController : ControllerBase
{
    private readonly PermissionCatalogService _catalog;

    public PermissionsController(
        PermissionCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(
            ApiResponse<IReadOnlyList<PermissionDefinition>>.Ok(
                _catalog.GetAll(),
                "Permisos obtenidos correctamente"
            )
        );
    }
}
