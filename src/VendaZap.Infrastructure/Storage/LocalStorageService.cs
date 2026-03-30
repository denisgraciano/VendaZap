using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VendaZap.Application.Common.Interfaces;

namespace VendaZap.Infrastructure.Storage;

/// <summary>
/// Implementação de armazenamento local para ambiente de desenvolvimento.
/// Salva arquivos em wwwroot/uploads e retorna URLs públicas relativas ao base URL configurado.
/// </summary>
public class LocalStorageService : IStorageService
{
    private readonly string _uploadsPath;
    private readonly string _baseUrl;
    private readonly ILogger<LocalStorageService> _logger;

    private static readonly HashSet<string> _allowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    };

    public LocalStorageService(IConfiguration config, ILogger<LocalStorageService> logger)
    {
        _logger = logger;
        _uploadsPath = config["Storage:LocalPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        _baseUrl = (config["Storage:BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        string folder = "general",
        CancellationToken ct = default)
    {
        if (!_allowedContentTypes.Contains(contentType))
            throw new InvalidOperationException($"Tipo de arquivo não permitido: {contentType}. Use imagens JPEG, PNG, GIF, WebP ou SVG.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            ext = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => ".bin"
            };

        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var folderPath = Path.Combine(_uploadsPath, folder);
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, uniqueName);
        await using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, ct);

        var publicUrl = $"{_baseUrl}/uploads/{folder}/{uniqueName}";
        _logger.LogInformation("Arquivo salvo em {FilePath}, URL pública: {Url}", filePath, publicUrl);

        return publicUrl;
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        try
        {
            var uri = new Uri(fileUrl);
            var relativePath = uri.AbsolutePath.TrimStart('/');
            // relativePath é algo como "uploads/products/abcd.jpg"
            var filePath = Path.Combine(_uploadsPath, "..", relativePath.Replace('/', Path.DirectorySeparatorChar));
            filePath = Path.GetFullPath(filePath);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Arquivo removido: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover arquivo: {Url}", fileUrl);
        }

        return Task.CompletedTask;
    }
}
