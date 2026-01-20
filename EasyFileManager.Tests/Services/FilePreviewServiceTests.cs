using EasyFileManager.Core.Services;
using EasyFileManager.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EasyFileManager.Tests.Services;

/// <summary>
/// Unit tests for FilePreviewService
/// Tests cover: type detection, image preview, text preview, file info extraction
/// </summary>
public class FilePreviewServiceTests : IDisposable
{
    private readonly TestAppLogger<FilePreviewService> _logger;
    private readonly FilePreviewService _service;
    private readonly TestFileSystemHelper _fileSystem;

    public FilePreviewServiceTests()
    {
        _logger = TestLoggerFactory.CreateTest<FilePreviewService>();
        _service = new FilePreviewService(_logger);
        _fileSystem = new TestFileSystemHelper();
    }

    #region GetPreviewType Tests

    [Theory]
    [InlineData(".jpg", FilePreviewType.Image)]
    [InlineData(".jpeg", FilePreviewType.Image)]
    [InlineData(".png", FilePreviewType.Image)]
    [InlineData(".gif", FilePreviewType.Image)]
    [InlineData(".bmp", FilePreviewType.Image)]
    public void GetPreviewType_ImageExtensions_ReturnsImage(string extension, FilePreviewType expected)
    {
        var filePath = $@"C:\test\image{extension}";
        var result = _service.GetPreviewType(filePath);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".txt", FilePreviewType.Text)]
    [InlineData(".log", FilePreviewType.Text)]
    [InlineData(".xml", FilePreviewType.Text)]
    [InlineData(".json", FilePreviewType.Text)]
    [InlineData(".csv", FilePreviewType.Text)]
    [InlineData(".md", FilePreviewType.Text)]
    public void GetPreviewType_TextExtensions_ReturnsText(string extension, FilePreviewType expected)
    {
        var filePath = $@"C:\test\document{extension}";
        var result = _service.GetPreviewType(filePath);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".mp3", FilePreviewType.Audio)]
    [InlineData(".wav", FilePreviewType.Audio)]
    [InlineData(".flac", FilePreviewType.Audio)]
    public void GetPreviewType_AudioExtensions_ReturnsAudio(string extension, FilePreviewType expected)
    {
        var filePath = $@"C:\test\audio{extension}";
        var result = _service.GetPreviewType(filePath);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".mp4", FilePreviewType.Video)]
    [InlineData(".avi", FilePreviewType.Video)]
    [InlineData(".mkv", FilePreviewType.Video)]
    public void GetPreviewType_VideoExtensions_ReturnsVideo(string extension, FilePreviewType expected)
    {
        var filePath = $@"C:\test\video{extension}";
        var result = _service.GetPreviewType(filePath);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".dll")]
    [InlineData(".zip")]
    [InlineData(".pdf")]
    public void GetPreviewType_UnknownExtensions_ReturnsGeneric(string extension)
    {
        var filePath = $@"C:\test\file{extension}";
        var result = _service.GetPreviewType(filePath);
        result.Should().Be(FilePreviewType.Generic);
    }

    [Fact]
    public void GetPreviewType_CaseInsensitive()
    {
        _service.GetPreviewType(@"C:\test.TXT").Should().Be(FilePreviewType.Text);
        _service.GetPreviewType(@"C:\test.JPG").Should().Be(FilePreviewType.Image);
        _service.GetPreviewType(@"C:\test.Mp3").Should().Be(FilePreviewType.Audio);
    }

    #endregion

    #region LoadTextPreviewAsync Tests

    [Fact]
    public async Task LoadTextPreviewAsync_ValidTextFile_ReturnsContent()
    {
        var content = "Line 1\nLine 2\nLine 3\nLine 4\nLine 5";
        var filePath = _fileSystem.CreateFile("text_preview.txt", content);

        var preview = await _service.LoadTextPreviewAsync(filePath);

        preview.Should().NotBeNull();
        preview.Content.Should().Contain("Line 1");
        preview.LineCount.Should().Be(5);
    }

    [Fact]
    public async Task LoadTextPreviewAsync_EmptyFile_HandlesGracefully()
    {
        var filePath = _fileSystem.CreateFile("empty.txt", "");

        var preview = await _service.LoadTextPreviewAsync(filePath);

        preview.Should().NotBeNull();
        preview.Content.Should().BeEmpty();
        preview.LineCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadTextPreviewAsync_LargeFile_TruncatesContent()
    {
        var largeContent = string.Join("\n", Enumerable.Range(1, 10000).Select(i => $"Line {i}"));
        var filePath = _fileSystem.CreateFile("large.txt", largeContent);

        var preview = await _service.LoadTextPreviewAsync(filePath);

        preview.Should().NotBeNull();
        preview.Content.Length.Should().BeLessThan(largeContent.Length);
        preview.IsTruncated.Should().BeTrue();
    }

    #endregion

    #region LoadImagePreviewAsync Tests

    [Fact]
    public async Task LoadImagePreviewAsync_NonExistentFile_ReturnsNull()
    {
        var nonExistentPath = Path.Combine(_fileSystem.RootPath, "nonexistent.png");

        var preview = await _service.LoadImagePreviewAsync(nonExistentPath);

        preview.Should().BeNull();
    }

    #endregion

    public void Dispose()
    {
        _fileSystem.Dispose();
    }
}
