using CreditWorks.Api.Middleware;
using CreditWorks.Core.Interfaces;
using CreditWorks.Core.Services;
using CreditWorks.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<ICategoryResolver, CategoryResolver>();

builder.Services.AddCors(o => o.AddPolicy("spa", p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var messages = context.ModelState
            .SelectMany(kv => kv.Value!.Errors)
            .Select(e =>
                !string.IsNullOrWhiteSpace(e.ErrorMessage)
                    ? e.ErrorMessage
                    : "The request body is malformed or contains invalid values.")
            .Distinct()
            .ToList();

        if (messages.Count == 0)
            messages.Add("The request body is malformed or contains invalid values.");

        return new BadRequestObjectResult(new { errors = messages });
    };
});

var app = builder.Build();

app.UseCors("spa");
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();
app.Run();

public partial class Program { }