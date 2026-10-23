using Microsoft.EntityFrameworkCore;

namespace OpenQMS.Models
{
    /// <summary>
    /// Common shape of the soft-deletable attachment / document tables. Deleted files keep their
    /// row and stored file (GMP traceability); a global query filter hides them from normal use.
    /// </summary>
    public interface ISoftDeletedFile
    {
        int Id { get; }
        string FileName { get; }
        bool IsDeleted { get; }
        string? DeletedBy { get; }
        DateTime? DeletedOn { get; }
        string? DeletionReason { get; }
    }

    public static class SoftDeletedFileQueries
    {
        /// <summary>
        /// Source for a download: live files normally (global filter applies); when
        /// <paramref name="deleted"/> is true, only soft-deleted files (retained-files view).
        /// </summary>
        public static IQueryable<T> ForDownload<T>(this DbSet<T> set, bool deleted)
            where T : class, ISoftDeletedFile
            => deleted ? set.IgnoreQueryFilters().AsNoTracking().Where(f => f.IsDeleted) : set;

        /// <summary>
        /// Soft-deleted files, newest deletion first (for the "Deleted attachments" section).
        /// Read-only on purpose: tracked entities would be fixed up into the parent's live
        /// attachment collection and reappear in the current list.
        /// </summary>
        public static IQueryable<T> DeletedOnly<T>(this DbSet<T> set)
            where T : class, ISoftDeletedFile
            => set.IgnoreQueryFilters().AsNoTracking().Where(f => f.IsDeleted).OrderByDescending(f => f.DeletedOn);
    }

    /// <summary>Model for the shared _DeletedAttachments partial.</summary>
    public record DeletedFilesView(
        IEnumerable<ISoftDeletedFile>? Items,
        string DownloadAction,
        string RouteKey = "id",
        string Noun = "attachments");
}
