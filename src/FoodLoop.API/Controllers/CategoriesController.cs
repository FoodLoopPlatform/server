using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("categories")]
[AllowAnonymous]
public class CategoriesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public CategoriesController(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /categories — lists all available product categories.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                NameAr = c.NameAr,
                Icon = c.Icon
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(categories));
    }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Icon { get; set; }
}
