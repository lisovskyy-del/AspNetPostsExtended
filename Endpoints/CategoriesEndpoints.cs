namespace AspNetPostsExtended.Endpoints;

using System.Net;
using System.Text;
using AspNetPosts.Helpers;
using AspNetPosts.Services;

public static class CategoriresEndpoints
{
    public static RouteGroupBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories")
                        .WithTags("Categories");

        
        group.MapGet("/", (BlogStorage db) =>
        {
            var sb = new StringBuilder("<h1>All Categories</h1><ul>");
            foreach (var c in db.Categories)
            {
                var count = db.Posts.Count(p => p.CategoryIds.Contains(c.Id));
                sb.Append($@"
                <li style='margin-bottom:10px;'>
                <a href='/posts/{c.Slug}/'><strong>{WebUtility.HtmlEncode(c.Name)}</strong></a> ({count} posts)
                — <a href='/categories/edit?id={c.Id}'>[Edit]</a>
                </li>");
            }
            sb.Append("</ul>");
            return Results.Content(HtmlBuilder.Layout("Categories", sb.ToString()), "text/html; charset=utf-8");
        });

        group.MapGet("/add", () =>
            Results.Content(RenderCategoryForm("/categories/add", "New Category", "", null), "text/html; charset=utf-8"));
        
        group.MapPost("/add", async (HttpRequest req, BlogStorage db) =>
        {
            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();

            if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
                return Results.Content(
                    RenderCategoryForm("/categories/add", "New Category", name, "Category name must be at least 2 characters long."),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status400BadRequest);

            if (db.Categories.Any(c => c.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Results.Content(
                    RenderCategoryForm("/categories/add", "New Category", name, "This category already exists."),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status400BadRequest);

            db.AddCategory(name);
            return Results.Redirect("/categories");
        }).DisableAntiforgery();

        group.MapGet("/edit", (int id, BlogStorage db) =>
        {
            var cat = db.Categories.FirstOrDefault(c => c.Id == id);
            if (cat is null) return Results.NotFound("Category not found");
            return Results.Content(RenderCategoryForm($"/categories/edit?id={id}", "Edit Category", cat.Name, null), "text/html; charset=utf-8");
        });

        group.MapPost("/edit", async (int id, HttpRequest req, BlogStorage db) =>
        {
            var cat = db.Categories.FirstOrDefault(c => c.Id == id);
            if (cat is null) return Results.NotFound("Category not found");

            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();

            if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
                return Results.Content(
                    RenderCategoryForm($"/categories/edit?id={id}", "Edit Category", name, "Minimum 2 characters required."),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status400BadRequest);

            cat.Name = name.Trim();
            cat.Slug = BlogStorage.GenerateSlug(name, includeSuffix: false);
            return Results.Redirect("/categories");
        }).DisableAntiforgery();

        group.MapGet("/delete", (BlogStorage db) =>
        {
            var sb = new StringBuilder("<h1>Delete Categories</h1>");
            foreach (var c in db.Categories)
            {
                sb.Append($@"
                <div class='card' style='display:flex; justify-content:space-between; align-items:center;'>
                    <span><strong>{WebUtility.HtmlEncode(c.Name)}</strong></span>
                    <form method='post' action='/categories/delete?id={c.Id}' style='margin:0;'>
                        <button type='submit' class='btn-danger'>Delete</button>
                    </form>
                </div>");
            }
            return Results.Content(HtmlBuilder.Layout("Delete Categories", sb.ToString()), "text/html; charset=utf-8");
        });

        group.MapPost("/delete", (int id, BlogStorage db) =>
        {
            var cat = db.Categories.FirstOrDefault(c => c.Id == id);
            if (cat != null)
            {
                db.Categories.Remove(cat);
                foreach (var p in db.Posts) p.CategoryIds.Remove(id);
            }
            return Results.Redirect("/categories/delete");
        }).DisableAntiforgery();

        static string RenderCategoryForm(string action, string heading, string currentName, string? error)
        {
            var errBlock = string.IsNullOrEmpty(error) ? "" : $"<div class='error'>{error}</div>";
            var body = $@"
            <h1>{heading}</h1>
            {errBlock}
            <form class='card' method='post' action='{action}'>
                <label>Category Name:</label>
                <input type='text' name='name' value='{WebUtility.HtmlEncode(currentName)}'>
                <button type='submit'>Save</button>
            </form>";
            return HtmlBuilder.Layout(heading, body);
        }

        return group;
    }
}