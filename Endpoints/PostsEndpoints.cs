namespace AspNetPostsExtended.Endpoints;

using System.Net;
using System.Text;
using AspNetPosts.Helpers;
using AspNetPosts.Models;
using AspNetPosts.Services;

public static class PostsEndpoints
{
    private static readonly string uploadsPath =
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

    public static RouteGroupBuilder MapPostsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/posts")
                        .WithTags("Posts");
        Directory.CreateDirectory(uploadsPath);
        

        group.MapGet("/", (BlogStorage db) =>
        {
            var html = HtmlBuilder.RenderPostsList("All Posts", db.Posts, db);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        group.MapGet("/q", (string? search, BlogStorage db) =>
        {
            var query = search?.Trim() ?? string.Empty;
            var filtered = string.IsNullOrEmpty(query)
                ? db.Posts
                : db.Posts.Where(p =>
                    p.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Content.Contains(query, StringComparison.OrdinalIgnoreCase));

            var html = HtmlBuilder.RenderPostsList($"Search results for: \"{query}\"", filtered, db);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        group.MapGet("/{id:int}", (int id, BlogStorage db) =>
        {
            var post = db.Posts.FirstOrDefault(p => p.Id == id);
            if (post is null) return Results.NotFound(HtmlBuilder.Layout("404", "<h1>Post not found</h1>"));
            return RenderSinglePost(post, db);
        });

        group.MapGet("/{slugOrCategory}", (HttpContext ctx, string slugOrCategory, BlogStorage db) =>
        {
            bool hasTrailingSlash = ctx.Request.Path.Value?.EndsWith('/') ?? false;
            var category = db.Categories.FirstOrDefault(c => c.Slug.Equals(slugOrCategory, StringComparison.OrdinalIgnoreCase));

            if (hasTrailingSlash || category != null)
            {
                if (category is null)
                    return Results.NotFound(HtmlBuilder.Layout("404", "<h1>Category not found</h1>"));

                var postsInCat = db.Posts.Where(p => p.CategoryIds.Contains(category.Id));
                var html = HtmlBuilder.RenderPostsList($"Category: {category.Name}", postsInCat, db);
                return Results.Content(html, "text/html; charset=utf-8");
            }

            var post = db.Posts.FirstOrDefault(p => p.Slug.Equals(slugOrCategory, StringComparison.OrdinalIgnoreCase));
            if (post is null)
                return Results.NotFound(HtmlBuilder.Layout("404", "<h1>Post not found</h1>"));

            return RenderSinglePost(post, db);
        });

        group.MapGet("/category/{categoryName}", (string categoryName, BlogStorage db) =>
        {
            return Results.Redirect($"/posts/{categoryName}");
        });

        group.MapGet("/add", (BlogStorage db) =>
        {
            return Results.Content(RenderPostForm("/posts/add", "Add Post", null, db, null), "text/html; charset=utf-8");
        });

        
        group.MapPost("/add", async (HttpRequest req, BlogStorage db) =>
        {
            var form = await req.ReadFormAsync();
            var title = form["title"].ToString();
            var content = form["content"].ToString();
            var selectedCategories = form["categoryIds"]
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id != 0)
                .ToList();
            var imageFile = form.Files["image"];

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(title) || title.Length < 3) errors.Add("Title must be at least 3 characters long.");
            if (string.IsNullOrWhiteSpace(content)) errors.Add("Post content cannot be empty.");
            if (!selectedCategories.Any()) errors.Add("Select at least one category.");
            if (imageFile is null || imageFile.Length == 0) errors.Add("An image is required for the post.");

            if (errors.Any())
            {
                var errHtml = string.Join("<br>", errors);
                return Results.Content(
                    RenderPostForm("/posts/add", "Add Post", null, db, errHtml),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var ext = Path.GetExtension(imageFile!.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var savePath = Path.Combine(uploadsPath, fileName);
            await using (var stream = File.Create(savePath))
            {
                await imageFile.CopyToAsync(stream);
            }

            var created = db.AddPost(title, content, $"/uploads/{fileName}", selectedCategories);
            return Results.Redirect($"/posts/{created.Slug}");
        }).DisableAntiforgery();

        
        group.MapGet("/edit", (int id, BlogStorage db) =>
        {
            var post = db.Posts.FirstOrDefault(p => p.Id == id);
            if (post is null) return Results.NotFound("Post not found");
            return Results.Content(RenderPostForm($"/posts/edit?id={id}", "Edit Post", post, db, null), "text/html; charset=utf-8");
        });

        group.MapPost("/edit", async (int id, HttpRequest req, BlogStorage db) =>
        {
            var post = db.Posts.FirstOrDefault(p => p.Id == id);
            if (post is null) return Results.NotFound("Post not found");

            var form = await req.ReadFormAsync();
            var title = form["title"].ToString();
            var content = form["content"].ToString();
            var selectedCategories = form["categoryIds"]
                .Select(value => int.TryParse(value, out var categoryId) ? categoryId : 0)
                .Where(categoryId => categoryId != 0)
                .ToList();
            var imageFile = form.Files["image"];

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(title) || title.Length < 3) errors.Add("Title must be at least 3 characters long.");
            if (string.IsNullOrWhiteSpace(content)) errors.Add("Post content cannot be empty.");
            if (!selectedCategories.Any()) errors.Add("Select at least one category.");

            if (errors.Any())
            {
                return Results.Content(
                    RenderPostForm($"/posts/edit?id={id}", "Edit Post", post, db, string.Join("<br>", errors)),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (imageFile is { Length: > 0 })
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                var savePath = Path.Combine(uploadsPath, fileName);
                await using var stream = File.Create(savePath);
                await imageFile.CopyToAsync(stream);
                post.ImageUrl = $"/uploads/{fileName}";
            }

            post.Title = title.Trim();
            post.Content = content.Trim();
            post.CategoryIds = selectedCategories;

            return Results.Redirect($"/posts/{post.Slug}");
        }).DisableAntiforgery();

        group.MapGet("/delete", (BlogStorage db) =>
        {
            var sb = new StringBuilder("<h1>Delete Posts</h1>");
            foreach (var p in db.Posts)
            {
                sb.Append($@"
                <div class='card' style='display:flex; justify-content:space-between; align-items:center;'>
                    <span><strong>{WebUtility.HtmlEncode(p.Title)}</strong> (ID: {p.Id})</span>
                    <form method='post' action='/posts/delete?id={p.Id}' style='margin:0;'>
                        <button type='submit' class='btn-danger'>Delete</button>
                    </form>
                </div>");
            }
            return Results.Content(HtmlBuilder.Layout("Delete Posts", sb.ToString()), "text/html; charset=utf-8");
        });

        group.MapPost("/delete", (int id, BlogStorage db) =>
        {
            var post = db.Posts.FirstOrDefault(p => p.Id == id);
            if (post != null) db.Posts.Remove(post);
            return Results.Redirect("/posts/delete");
        }).DisableAntiforgery();


        static IResult RenderSinglePost(Post post, BlogStorage db)
        {
            var catsHtml = string.Join("", post.CategoryIds
                .Select(id => db.Categories.FirstOrDefault(c => c.Id == id))
                .Where(c => c != null)
                .Select(c => $"<a class='badge' href='/posts/{c!.Slug}/'>{WebUtility.HtmlEncode(c.Name)}</a>"));

            var body = $@"
            <div class='card'>
                <img src='{post.ImageUrl}' alt='Cover' style='height:350px;'>
                <h1>{WebUtility.HtmlEncode(post.Title)}</h1>
                <p><small>Slug: <code>{post.Slug}</code> | ID: {post.Id}</small></p>
                <div style='margin-bottom:15px;'>{catsHtml}</div>
                <p style='white-space: pre-wrap;'>{WebUtility.HtmlEncode(post.Content)}</p>
                <hr>
                <a class='btn btn-secondary' href='/posts/edit?id={post.Id}'>Edit Post</a>
            </div>";

            return Results.Content(HtmlBuilder.Layout(post.Title, body), "text/html; charset=utf-8");
        }

        static string RenderPostForm(string action, string heading, Post? existing, BlogStorage db, string? error)
        {
            var errBlock = string.IsNullOrEmpty(error) ? "" : $"<div class='error'>{error}</div>";
            var checkboxes = new StringBuilder("<div style='margin: 10px 0 15px;'>");
            foreach (var c in db.Categories)
            {
                var isChecked = existing?.CategoryIds.Contains(c.Id) == true ? "checked" : "";
                checkboxes.Append($"<label style='margin-right:15px;'><input type='checkbox' name='categoryIds' value='{c.Id}' {isChecked}> {WebUtility.HtmlEncode(c.Name)}</label>");
            }
            checkboxes.Append("</div>");

            var imgNote = existing != null ? "<small>(Leave empty to keep current image)</small>" : "<small>(Required)</small>";

            var body = $@"
            <h1>{heading}</h1>
            {errBlock}
            <form class='card' method='post' action='{action}' enctype='multipart/form-data'>
                <label>Title:</label>
                <input type='text' name='title' value='{WebUtility.HtmlEncode(existing?.Title ?? "")}'>
                
                <label>Categories:</label>
                {checkboxes}

                <label>Post Image {imgNote}:</label><br>
                <input type='file' name='image' accept='image/*' style='margin: 8px 0 15px;'><br>

                <label>Content:</label>
                <textarea name='content' rows='6'>{WebUtility.HtmlEncode(existing?.Content ?? "")}</textarea>

                <button type='submit'>Save</button>
            </form>";

            return HtmlBuilder.Layout(heading, body);
        }

        return group;
    }
}