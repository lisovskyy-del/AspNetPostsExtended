using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using AspNetPosts.Helpers;
using AspNetPosts.Services;
using AspNetPostsExtended.Endpoints;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BlogStorage>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "AspNetPosts API";
        document.Info.Description = "A simple blog API built with ASP.NET Core Minimal APIs.";
        document.Info.Version = "v1";

        document.Servers = new List<OpenApiServer>
        {
            new() { Url = "https://localhost:7234", Description = "HTTPS local" },
            new() { Url = "http://localhost:5234", Description = "HTTP local" }
        };

        return Task.CompletedTask;
    });

    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        if (context.JsonTypeInfo.Type == typeof(decimal))
        {
            schema.Format = "decimal";
            schema.Description = string.IsNullOrWhiteSpace(schema.Description)
                ? "Decimal monetary value."
                : schema.Description + " Decimal monetary value.";
        }


        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        operation.Responses.TryAdd("500", new OpenApiResponse
        {
            Description = "Unexpected server error"
        });

        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json")
        .AllowAnonymous();

    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("Minimal API Scalar Demo")
               .WithOpenApiRoutePattern("/openapi/{documentName}.json")
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
               .ShowOperationId()
               .ExpandAllTags()
               .SortTagsAlphabetically()
               .SortOperationsByMethod()
               .PreserveSchemaPropertyOrder();
    })
    .AllowAnonymous();
}

app.MapGet("/_internal/ping", () => Results.Ok(new
{
    Status = "OK",
    Timestamp = DateTime.UtcNow
}))
.ExcludeFromDescription();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/html; charset=utf-8";
        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var html = HtmlBuilder.Layout("Server Error",
            $"<div class='error'><h2>An internal server error occurred</h2><p>{WebUtility.HtmlEncode(exceptionFeature?.Error.Message)}</p></div>");
        await context.Response.WriteAsync(html);
    });
});

app.UseStaticFiles();

var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);

app.MapGet("/", () => Results.Redirect("/posts"));

app.MapGet("/about", (IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.WebRootPath, "about.html");
    return File.Exists(filePath)
        ? Results.File(filePath, "text/html; charset=utf-8")
        : Results.NotFound("File about.html not found");
});

app.MapPostsEndpoints();
app.MapCategoriesEndpoints();

app.Run();
