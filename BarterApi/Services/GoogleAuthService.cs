using Google.Apis.Auth;

namespace BarterApi.Services;

public class GoogleAuthService
{
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(IConfiguration config, ILogger<GoogleAuthService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Verifies the Google ID token from the Flutter app and returns the user's info.
    /// </summary>
    public async Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string idToken)
    {
        try
        {
            var clientId = _config["Google:ClientId"]!;
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleUserInfo
            {
                GoogleId = payload.Subject,
                Email = payload.Email,
                Name = payload.Name,
                PhotoUrl = payload.Picture,
                EmailVerified = payload.EmailVerified
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google token verification failed");
            return null;
        }
    }
}

public class GoogleUserInfo
{
    public string GoogleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool EmailVerified { get; set; }
}
