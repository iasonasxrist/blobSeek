namespace BlobSeek.Domain;

public enum IndexingStatus
{
    Pending = 0,
    Indexed = 1,
    Skipped = 2,
    Failed = 3,
    Deleted = 4
}
