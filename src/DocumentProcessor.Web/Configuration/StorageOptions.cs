using System.ComponentModel.DataAnnotations;

namespace DocumentProcessor.Web.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string RootPath { get; init; } = "uploads";

    [Range(1, 500)]
    public int MaxFileSizeMegabytes { get; init; } = 50;

    [Range(1, 50)]
    public int MaxFilesPerUpload { get; init; } = 10;

    public string[] AllowedExtensions { get; init; } = [".pdf", ".txt", ".log"];

    public long MaxFileSizeBytes => MaxFileSizeMegabytes * 1024L * 1024L;
}
