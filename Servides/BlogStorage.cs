using System.Text.RegularExpressions;
using AspNetPosts.Models;

namespace AspNetPosts.Services;

public class BlogStorage
{
    public List<Category> Categories { get; } = new();
    public List<Post> Posts { get; } = new();

    private int _nextPostId = 1;
    private int _nextCategoryId = 1;

    public BlogStorage()
    {
        var csharp = AddCategory("C# & .NET");
        var docker = AddCategory("Docker");
        var web = AddCategory("Web Dev");

        AddPost(
            "Navigating Docker with Captains",
            "Docker allows you to package applications into isolated containers...",
            "https://placehold.co/600x300/2563eb/white?text=Docker+Post",
            new List<int> { docker.Id, web.Id }
        );
    }

    public Category AddCategory(string name)
    {
        var category = new Category
        {
            Id = _nextCategoryId++,
            Name = name.Trim(),
            Slug = GenerateSlug(name, includeSuffix: false)
        };
        Categories.Add(category);
        return category;
    }

    public Post AddPost(string title, string content, string imageUrl, List<int> categoryIds)
    {
        var post = new Post
        {
            Id = _nextPostId++,
            Title = title.Trim(),
            Slug = GenerateSlug(title, includeSuffix: true),
            Content = content.Trim(),
            ImageUrl = imageUrl,
            CategoryIds = categoryIds
        };
        Posts.Add(post);
        return post;
    }

    public static string GenerateSlug(string phrase, bool includeSuffix)
    {
        string str = phrase.ToLowerInvariant();
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"\s+", " ").Trim();
        str = Regex.Replace(str, @"\s", "-");

        if (includeSuffix)
        {
            var suffix = Guid.NewGuid().ToString("N")[..4];
            return $"{str}-{suffix}";
        }
        return str;
    }
}