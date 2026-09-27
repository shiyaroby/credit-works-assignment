using CreditWorks.Api.Contracts;
using CreditWorks.Core.Models;
using CreditWorks.Core.Validation;
using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

        // For edits, check whether the range actually changed. If only name/icon changed,
        // the update will always be valid — skip the conflict redirect path.
        var existing = id is null ? null : all.FirstOrDefault(c => c.Id == id);

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
        if (rangeErrors.Any())
        {
            var isExistingEdit = existing is not null;
            var rangeChanged = isExistingEdit &&
                (existing!.MinWeightKg != candidate.MinWeightKg ||
                 existing.MaxWeightKg != candidate.MaxWeightKg);

            if (isExistingEdit && rangeChanged)
            {
                return Conflict(new
                {
                    errors = new[] {
                        "This boundary change can't be saved on its own because it would leave "
                        + "a gap or overlap until a neighbouring category is adjusted too.",
                        "Use 'Edit all' to change both rows in a single save." }
                        .Concat(rangeErrors),
                    suggestedAction = "bulk-edit"
                });
            }

            return BadRequest(new { errors = rangeErrors });
        }

        if (id is null)
        {
            _db.VehicleCategories.Add(candidate);
        }
        else
        {
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

        var remaining = await _db.VehicleCategories
            .Where(c => c.Id != id)
            .ToListAsync();

        if (remaining.Count == 0)
        {
            return BadRequest(new
            {
                errors = new[] {
                    "Cannot delete the only category. Add a replacement first." }
            });
        }

        var errors = CategoryRangeValidator.Validate(remaining);
        if (errors.Any())
        {
            return Conflict(new
            {
                errors = new[] {
                    "Deleting this category would leave an invalid configuration.",
                    "Use 'Edit all' to remove it and redistribute the weight range." }
                    .Concat(errors),
                suggestedAction = "bulk-edit"
            });
        }

        _db.VehicleCategories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("bulk")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> ReplaceAll(BulkCategoryRequest req)
    {
        var incoming = req.Categories?.ToList() ?? new List<UpsertCategoryRequest>();

        if (incoming.Count == 0)
            return BadRequest(new { errors = new[] { "At least one category is required." } });

        var basicErrors = new List<string>();
        for (var i = 0; i < incoming.Count; i++)
        {
            var r = incoming[i];
            if (string.IsNullOrWhiteSpace(r.Name))
                basicErrors.Add($"Row {i + 1}: category name is required.");
            if (string.IsNullOrWhiteSpace(r.IconName))
                basicErrors.Add($"Row {i + 1}: category icon is required.");
            if (r.MinWeightKg < 0)
                basicErrors.Add($"Row {i + 1} ('{r.Name}'): minimum weight cannot be negative.");
            if (r.MaxWeightKg.HasValue && r.MaxWeightKg.Value <= r.MinWeightKg)
                basicErrors.Add($"Row {i + 1} ('{r.Name}'): max weight must exceed min weight.");
        }
        if (basicErrors.Any()) return BadRequest(new { errors = basicErrors });

        var candidates = incoming.Select((r, i) => new VehicleCategory
        {
            Id = i,
            Name = r.Name.Trim(),
            MinWeightKg = r.MinWeightKg,
            MaxWeightKg = r.MaxWeightKg,
            IconName = r.IconName.Trim()
        }).ToList();

        var rangeErrors = CategoryRangeValidator.Validate(candidates);
        if (rangeErrors.Any()) return BadRequest(new { errors = rangeErrors });

        // InMemory (used by tests) does not support transactions.
        // SQL Server (production / local dev) does — wrap the two saves so a
        // failure in the second rolls back the first.
        var supportsTransactions = _db.Database.IsRelational();

        IDbContextTransaction? tx = supportsTransactions
            ? await _db.Database.BeginTransactionAsync()
            : null;

        try
        {
            var existing = await _db.VehicleCategories.ToListAsync();
            _db.VehicleCategories.RemoveRange(existing);
            await _db.SaveChangesAsync();

            foreach (var c in candidates)
            {
                c.Id = 0;
                _db.VehicleCategories.Add(c);
            }
            await _db.SaveChangesAsync();

            if (tx is not null) await tx.CommitAsync();
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        var saved = await _db.VehicleCategories
            .AsNoTracking()
            .OrderBy(c => c.MinWeightKg)
            .ToListAsync();

        return Ok(saved.Select(ToDto));
    }

    [HttpGet("icons")]
    public IActionResult Icons() => Ok(new[]
    { "light.svg", "medium.svg", "heavy.svg", "car.svg", "truck.svg", "van.svg" });

    private static CategoryDto ToDto(VehicleCategory c) =>
        new(c.Id, c.Name, c.MinWeightKg, c.MaxWeightKg, c.IconName);
}