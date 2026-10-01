using CreditWorks.Api.Contracts;
using CreditWorks.Core.Models;
using CreditWorks.Core.Services;
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

    [HttpGet("icons")]
    public IActionResult Icons() => Ok(CategoryIcons.All);

    /// <summary>
    /// Creates a category by splitting the existing category that contains MinWeightKg.
    /// The new category takes over the upper part of that range.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest req)
    {
        var fieldErrors = CategoryFieldValidator.Validate(req.Name, req.IconName, req.MinWeightKg, null);
        if (fieldErrors.Count > 0) return BadRequest(new { errors = fieldErrors });

        var all = await _db.VehicleCategories.ToListAsync();
        var nameErrors = CategoryFieldValidator.ValidateNameUnique(req.Name, null, all);
        if (nameErrors.Count > 0) return BadRequest(new { errors = nameErrors });
        var result = CategoryRangeEditor.Split(
            all, req.Name.Trim(), req.IconName.Trim(), req.MinWeightKg);

        if (!result.Succeeded) return BadRequest(new { errors = result.Errors });

        _db.VehicleCategories.Add(result.Category!);
        await _db.SaveChangesAsync();
        return Ok(ToDto(result.Category!));
    }

    /// <summary>
    /// Updates a category. Name/icon changes always succeed. A range change that would leave
    /// a gap or overlap is answered with 409 so the client can offer the bulk editor.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, UpsertCategoryRequest req)
    {
        var basic = CategoryFieldValidator.Validate(req.Name, req.IconName, req.MinWeightKg, req.MaxWeightKg);
        if (basic.Count > 0) return BadRequest(new { errors = basic });

        var all = await _db.VehicleCategories.ToListAsync();
        var existing = all.FirstOrDefault(c => c.Id == id);
        if (existing is null) return NotFound();

        var nameErrors = CategoryFieldValidator.ValidateNameUnique(req.Name, id, all);
        if (nameErrors.Count > 0) return BadRequest(new { errors = nameErrors });

        var candidate = new VehicleCategory
        {
            Id = id,
            Name = req.Name.Trim(),
            MinWeightKg = req.MinWeightKg,
            MaxWeightKg = req.MaxWeightKg,
            IconName = req.IconName.Trim()
        };

        var workingSet = all.Where(c => c.Id != id).Append(candidate).ToList();
        var rangeErrors = CategoryRangeValidator.Validate(workingSet);

        if (rangeErrors.Count > 0)
        {
            var rangeChanged =
                existing.MinWeightKg != candidate.MinWeightKg ||
                existing.MaxWeightKg != candidate.MaxWeightKg;

            if (rangeChanged)
            {
                return Conflict(new
                {
                    errors = new[]
                    {
                        "This boundary change can't be saved on its own because it would leave " +
                        "a gap or overlap until a neighbouring category is adjusted too.",
                        "Use 'Edit all' to change both rows in a single save."
                    }.Concat(rangeErrors),
                    suggestedAction = "bulk-edit"
                });
            }

            return BadRequest(new { errors = rangeErrors });
        }

        existing.Name = candidate.Name;
        existing.MinWeightKg = candidate.MinWeightKg;
        existing.MaxWeightKg = candidate.MaxWeightKg;
        existing.IconName = candidate.IconName;

        await _db.SaveChangesAsync();
        return Ok(ToDto(existing));
    }

    /// <summary>Deletes a category; a neighbour absorbs its weight range.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var all = await _db.VehicleCategories.ToListAsync();
        var result = CategoryRangeEditor.Remove(all, id);

        if (result.NotFound) return NotFound();
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors });

        _db.VehicleCategories.Remove(result.Category!);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Replaces the whole category set. Removal and insertion share one SaveChanges call,
    /// which EF Core executes in a single database transaction.
    /// </summary>
    [HttpPut("bulk")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> ReplaceAll(BulkCategoryRequest req)
    {
        var incoming = req.Categories?.ToList() ?? new List<UpsertCategoryRequest>();
        if (incoming.Count == 0)
            return BadRequest(new { errors = new[] { "At least one category is required." } });

        var fieldErrors = incoming
            .SelectMany((r, i) => CategoryFieldValidator
                .Validate(r.Name, r.IconName, r.MinWeightKg, r.MaxWeightKg)
                .Select(e => $"Row {i + 1}: {e}"))
            .ToList();
        if (fieldErrors.Count > 0) return BadRequest(new { errors = fieldErrors });

        var candidates = incoming.Select(r => new VehicleCategory
        {
            Name = r.Name.Trim(),
            MinWeightKg = r.MinWeightKg,
            MaxWeightKg = r.MaxWeightKg,
            IconName = r.IconName.Trim()
        }).ToList();

        var duplicateNames = candidates
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"Duplicate category name: '{g.Key}'.")
            .ToList();
        if (duplicateNames.Count > 0) return BadRequest(new { errors = duplicateNames });

        var rangeErrors = CategoryRangeValidator.Validate(candidates);
        if (rangeErrors.Count > 0) return BadRequest(new { errors = rangeErrors });

        _db.VehicleCategories.RemoveRange(await _db.VehicleCategories.ToListAsync());
        _db.VehicleCategories.AddRange(candidates);
        await _db.SaveChangesAsync();

        return Ok(candidates.OrderBy(c => c.MinWeightKg).Select(ToDto));
    }

    private static CategoryDto ToDto(VehicleCategory c) =>
        new(c.Id, c.Name, c.MinWeightKg, c.MaxWeightKg, c.IconName);
}