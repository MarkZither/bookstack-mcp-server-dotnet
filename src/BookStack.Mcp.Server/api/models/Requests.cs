namespace BookStack.Mcp.Server.Api.Models;

public sealed class ListQueryParams
{
    public int? Count { get; set; }
    public int? Offset { get; set; }
    public string? Sort { get; set; }
}

public sealed class CreateBookRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public IList<Tag>? Tags { get; set; }
    public int? ImageId { get; set; }
    public int? DefaultTemplateId { get; set; }
}

public sealed class UpdateBookRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public IList<Tag>? Tags { get; set; }
    public int? ImageId { get; set; }
    public int? DefaultTemplateId { get; set; }
}

public sealed class CreateChapterRequest
{
    public int BookId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public IList<Tag>? Tags { get; set; }
}

public sealed class UpdateChapterRequest
{
    public int? BookId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public IList<Tag>? Tags { get; set; }
}

public sealed class CreatePageRequest
{
    public int? BookId { get; set; }
    public int? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Html { get; set; }
    public string? Markdown { get; set; }
    public IList<Tag>? Tags { get; set; }
}

public sealed class UpdatePageRequest
{
    public int? BookId { get; set; }
    public int? ChapterId { get; set; }
    public string? Name { get; set; }
    public string? Html { get; set; }
    public string? Markdown { get; set; }
    public IList<Tag>? Tags { get; set; }
}

public sealed class CreateShelfRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public IList<Tag>? Tags { get; set; }
    public IList<int>? Books { get; set; }
}

public sealed class UpdateShelfRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public IList<Tag>? Tags { get; set; }
    public IList<int>? Books { get; set; }
}

public sealed class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? Language { get; set; }
    public IList<int>? Roles { get; set; }
    public bool SendInvite { get; set; }
}

public sealed class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Language { get; set; }
    public IList<int>? Roles { get; set; }
}

public sealed class CreateRoleRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool MfaEnforced { get; set; }
    public string? ExternalAuthId { get; set; }
    public IList<string>? Permissions { get; set; }
}

public sealed class UpdateRoleRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool? MfaEnforced { get; set; }
    public string? ExternalAuthId { get; set; }
    public IList<string>? Permissions { get; set; }
}

public sealed class CreateAttachmentRequest
{
    public string Name { get; set; } = string.Empty;
    public int UploadedTo { get; set; }
    public string? Link { get; set; }
}

public sealed class UpdateAttachmentRequest
{
    public string? Name { get; set; }
    public int? UploadedTo { get; set; }
    public string? Link { get; set; }
}

public sealed class CreateImageRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "gallery";
    public int? UploadedTo { get; set; }
}

public sealed class UpdateImageRequest
{
    public string? Name { get; set; }
}

public sealed class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int? Page { get; set; }
    public int? Count { get; set; }
}

public sealed class UpdateContentPermissionsRequest
{
    public bool? Inheriting { get; set; }
    public IList<ContentPermissionEntry>? Permissions { get; set; }
}
