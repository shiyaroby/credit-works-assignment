using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ManufacturersController : ControllerBase
{
    private readonly AppDbContext _db;

    public ManufacturersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Manufacturers.AsNoTracking()
            .Where(m => m.IsActive).OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name }).ToListAsync());
}