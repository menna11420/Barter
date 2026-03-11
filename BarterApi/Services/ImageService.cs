using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BarterApi.Services;

public class ImageService
{
    private readonly bool _isMock;
    private readonly Cloudinary? _cloudinary;
    private readonly ILogger<ImageService> _logger;
    private readonly IWebHostEnvironment _env;

    public ImageService(IConfiguration config, ILogger<ImageService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
        var apiKey = config["Cloudinary:ApiKey"];
        
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_API_KEY")
        {
            _isMock = true;
            _logger.LogInformation("Cloudinary not configured — using local file storage.");
        }
        else
        {
            var account = new Account(
                config["Cloudinary:CloudName"],
                apiKey,
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (_isMock)
        {
            // Save to wwwroot/uploads and serve as a static file
            var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("Saved image locally: {FileName}", fileName);
            return $"/uploads/{fileName}";
        }

        using var uploadStream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, uploadStream),
            Folder = "barter",
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var result = await _cloudinary!.UploadAsync(uploadParams);

        if (result.Error != null)
        {
            _logger.LogError("Cloudinary upload error: {Error}", result.Error.Message);
            throw new Exception($"Image upload failed: {result.Error.Message}");
        }

        return result.SecureUrl.ToString();
    }

    public async Task<List<string>> UploadMultipleImagesAsync(List<IFormFile> files)
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            var url = await UploadImageAsync(file);
            urls.Add(url);
        }
        return urls;
    }

    public async Task DeleteImageAsync(string publicId)
    {
        if (_isMock)
        {
            // Delete local file if it starts with /uploads/
            if (publicId.StartsWith("/uploads/"))
            {
                var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", publicId.TrimStart('/'));
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            return;
        }

        try
        {
            var deleteParams = new DeletionParams(publicId);
            await _cloudinary!.DestroyAsync(deleteParams);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete image from Cloudinary: {PublicId}", publicId);
        }
    }
}
