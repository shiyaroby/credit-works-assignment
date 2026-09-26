using CreditWorks.Api.Contracts;
using CreditWorks.Core.Models;
using CreditWorks.Core.Validation;
using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll()
    {
        var cats = await _db.VehicleCategories.AsNoTracking()
            .OrderBy(c => c.MinWeightKg).ToListAsync();
        return Ok(cats.Select(ToDto));
    }

    [HttpPost]
    public Task<ActionResult<CategoryDto>> Create(UpsertCategoryRequest req)
        => Upsert(null, req);

    [HttpPut("{id:int}")]
    public Task<ActionResult<CategoryDto>> Update(int id, UpsertCategoryRequest req)
        => Upsert(id, req);

    private async Task<ActionResult<CategoryDto>> Upsert(int? id, UpsertCategoryRequest req)
    {
        var basic = new List<string>();
        if (string.IsNullOrWhiteSpace(req.Name)) basic.Add("Category name is required.");
        if (string.IsNullOrWhiteSpace(req.IconName)) basic.Add("Category icon is required.");
        if (req.MinWeightKg < 0) basic.Add("Minimum weight cannot be negative.");
        if (req.MaxWeightKg.HasValue && req.MaxWeightKg.Value <= req.MinWeightKg)
            basic.Add("Maximum weight must be greater than minimum.");
        if (basic.Any()) return BadRequest(new { errors = basic });

        var all = await _db.VehicleCategories.ToListAsync();
        var candidate = new VehicleCategory
        {
            Id = id ?? 0,
            Name = req.Name.Trim(),
            MinWeightKg = req.MinWeightKg,
            MaxWeightKg = req.MaxWeightKg,
            IconName = req.IconName.Trim()
        };

        var workingSet = all.Where(c => c.Id != (id ?? 0)).ToList();
        workingSet.Add(candidate);

        var rangeErrors = CategoryRangeValidator.Validate(workingSet);
        if (rangeErrors.Any()) return BadRequest(new { errors = rangeErrors });

        if (id is null) _db.VehicleCategories.Add(candidate);
        else
        {
            var existing = all.FirstOrDefault(c => c.Id == id);
            if (existing is null) return NotFound();
            existing.Name = candidate.Name;
            existing.MinWeightKg = candidate.MinWeightKg;
            existing.MaxWeightKg = candidate.MaxWeightKg;
            existing.IconName = candidate.IconName;
            candidate = existing;
        }

        await _db.SaveChangesAsync();
        return Ok(ToDto(candidate));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.VehicleCategories.FindAsync(id);
        if (cat is null) return NotFound();

        var remaining = await _db.VehicleCategories.Where(c => c.Id != id).ToListAsync();
        var errors = CategoryRangeValidator.Validate(remaining);
        if (errors.Any())
            return BadRequest(new { errors = new[] {
                "Deleting this category would leave an invalid configuration." }
                .Concat(errors) });

        _db.VehicleCategories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("icons")]
    public IActionResult Icons() => Ok(new[]
    { "light.svg", "medium.svg", "heavy.svg", "car.svg", "truck.svg", "van.svg" });

    private static CategoryDto ToDto(VehicleCategory c) =>
        new(c.Id, c.Name, c.MinWeightKg, c.MaxWeightKg, c.IconName);
}