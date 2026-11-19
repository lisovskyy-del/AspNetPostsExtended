using System.Net;
using System.Text;
using AspNetPosts.Models;
using AspNetPosts.Services;

namespace AspNetPosts.Helpers;

public static class HtmlBuilder
{
    public static string Layout(string title, string body) => $@"
<!DOCTYPE html>
<html lang=""uk"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{WebUtility.HtmlEncode(title)}</title>
</head>
<body>
    <nav>
        <a href=""/posts"">All posts</a>
        <a href=""/categories"">Categories</a>
        <a href=""/posts/add"">+ Add post</a>
        <a href=""/categories/add"">+ Add Category</a>
        <a href=""/posts/delete"">Delete Post</a>
        <a href=""/categories/delete"">Delete Category</a>
        <a href=""/about"">About Blog</a>
    </nav>
    <form action=""/posts/q"" method=""get"" style=""display:flex; gap:10px; margin-bottom:20px;"">
        <input type=""text"" name=""search"" placeholder=""Search posts by headers or text..."" style=""margin:0;"">
        <button type=""submit"">Search</button>
    </form>
    {body}
</body>
</html>";

    public static string RenderPostsList(string heading, IEnumerable<Post> posts, BlogStorage db)
    {
        var sb = new StringBuilder($"<h1>{WebUtility.HtmlEncode(heading)}</h1>");
        if (!posts.Any())
        {
            sb.Append("<p>Posts not found.</p>");
            return Layout(heading, sb.ToString());
        }

        foreach (var p in posts)
        {
            var catsHtml = string.Join("", p.CategoryIds
                .Select(id => db.Categories.FirstOrDefault(c => c.Id == id))
                .Where(c => c != null)
                .Select(c => $"<a class='badge' href='/posts/category/{c!.Slug}/'>{WebUtility.HtmlEncode(c.Name)}</a>"));

            sb.Append($@"
            <div class='card'>
                <img src='{p.ImageUrl}' alt='Cover'>
                <h2><a href='/posts/{p.Slug}'>{WebUtility.HtmlEncode(p.Title)}</a></h2>
                <div>{catsHtml}</div>
                <p>{WebUtility.HtmlEncode(p.Content.Length > 150 ? p.Content[..150] + "..." : p.Content)}</p>
                <a class='btn' href='/posts/{p.Slug}'>Read (Slug)</a>
                <a class='btn btn-secondary' href='/posts/{p.Id}'>Read (ID: {p.Id})</a>
                <a class='btn btn-secondary' href='/posts/edit?id={p.Id}'>Edit</a>
            </div>");
        }
        return Layout(heading, sb.ToString());
    }
}