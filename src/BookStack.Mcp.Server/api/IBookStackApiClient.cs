using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public interface IBookStackApiClient
{
    // Books
    Task<ListResponse<Book>> ListBooksAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<Book> CreateBookAsync(CreateBookRequest request, CancellationToken cancellationToken = default);
    Task<BookWithContents> GetBookAsync(int id, CancellationToken cancellationToken = default);
    Task<Book> UpdateBookAsync(int id, UpdateBookRequest request, CancellationToken cancellationToken = default);
    Task DeleteBookAsync(int id, CancellationToken cancellationToken = default);
    Task<string> ExportBookAsync(int id, ExportFormat format, CancellationToken cancellationToken = default);

    // Chapters
    Task<ListResponse<Chapter>> ListChaptersAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<Chapter> CreateChapterAsync(CreateChapterRequest request, CancellationToken cancellationToken = default);
    Task<ChapterWithPages> GetChapterAsync(int id, CancellationToken cancellationToken = default);
    Task<Chapter> UpdateChapterAsync(int id, UpdateChapterRequest request, CancellationToken cancellationToken = default);
    Task DeleteChapterAsync(int id, CancellationToken cancellationToken = default);
    Task<string> ExportChapterAsync(int id, ExportFormat format, CancellationToken cancellationToken = default);

    // Pages
    Task<ListResponse<Page>> ListPagesAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<Page> CreatePageAsync(CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<PageWithContent> GetPageAsync(int id, CancellationToken cancellationToken = default);
    Task<Page> UpdatePageAsync(int id, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task DeletePageAsync(int id, CancellationToken cancellationToken = default);
    Task<string> ExportPageAsync(int id, ExportFormat format, CancellationToken cancellationToken = default);

    // Shelves
    Task<ListResponse<Bookshelf>> ListShelvesAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<Bookshelf> CreateShelfAsync(CreateShelfRequest request, CancellationToken cancellationToken = default);
    Task<BookshelfWithBooks> GetShelfAsync(int id, CancellationToken cancellationToken = default);
    Task<Bookshelf> UpdateShelfAsync(int id, UpdateShelfRequest request, CancellationToken cancellationToken = default);
    Task DeleteShelfAsync(int id, CancellationToken cancellationToken = default);

    // Users
    Task<ListResponse<User>> ListUsersAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<User> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserWithRoles> GetUserAsync(int id, CancellationToken cancellationToken = default);
    Task<User> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(int id, int? migrateOwnershipId = null, CancellationToken cancellationToken = default);

    // Roles
    Task<ListResponse<Role>> ListRolesAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<Role> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleWithPermissions> GetRoleAsync(int id, CancellationToken cancellationToken = default);
    Task<Role> UpdateRoleAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(int id, CancellationToken cancellationToken = default);

    // Attachments
    Task<ListResponse<Attachment>> ListAttachmentsAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<Attachment> CreateAttachmentAsync(CreateAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<Attachment> GetAttachmentAsync(int id, CancellationToken cancellationToken = default);
    Task<Attachment> UpdateAttachmentAsync(int id, UpdateAttachmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAttachmentAsync(int id, CancellationToken cancellationToken = default);

    // Image Gallery
    Task<ListResponse<Image>> ListImagesAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task<Image> CreateImageAsync(CreateImageRequest request, CancellationToken cancellationToken = default);
    Task<Image> GetImageAsync(int id, CancellationToken cancellationToken = default);
    Task<Image> UpdateImageAsync(int id, UpdateImageRequest request, CancellationToken cancellationToken = default);
    Task DeleteImageAsync(int id, CancellationToken cancellationToken = default);

    // Search
    Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    // Recycle Bin
    Task<ListResponse<RecycleBinItem>> ListRecycleBinAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);
    Task RestoreFromRecycleBinAsync(int deletionId, CancellationToken cancellationToken = default);
    Task PermanentlyDeleteAsync(int deletionId, CancellationToken cancellationToken = default);

    // Content Permissions
    Task<ContentPermissions> GetContentPermissionsAsync(ContentType contentType, int contentId, CancellationToken cancellationToken = default);
    Task<ContentPermissions> UpdateContentPermissionsAsync(ContentType contentType, int contentId, UpdateContentPermissionsRequest request, CancellationToken cancellationToken = default);

    // Audit Log
    Task<ListResponse<AuditLogEntry>> ListAuditLogAsync(ListQueryParams? query = null, CancellationToken cancellationToken = default);

    // System
    Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default);
}
