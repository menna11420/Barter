using BarterApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarterApi.Controllers;

[ApiController]
[Route("api/images")]
[Authorize]
public class ImagesController : ControllerBase
{
    private readonly ImageService _imageService;

    public ImagesController(ImageService imageService)
    {
        _imageService = imageService;
    }

    // POST /api/images/upload  — single image
    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        var url = await _imageService.UploadImageAsync(file);
        return Ok(new { url });
    }

    // POST /api/images/upload-multiple  — multiple images
    [HttpPost("upload-multiple")]
    public async Task<IActionResult> UploadMultipleImages(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "No files provided" });

        var urls = await _imageService.UploadMultipleImagesAsync(files);
        return Ok(new { urls });
    }
}
