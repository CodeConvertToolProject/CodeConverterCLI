using System.Text.Json.Serialization;

namespace CodeConverterCLI;
internal class AccessTokenResponse
{
    [JsonPropertyName("accessCode")]
    public required string AccessToken { get; set; }
}

internal class LoginResponse
{
    [JsonPropertyName("verificationUriComplete")]
    public required string VerificationUriComplete { get; set; }

    [JsonPropertyName("interval")]
    public required string Interval { get; set; }

    [JsonPropertyName("deviceCode")]
    public required string DeviceCode { get; set; }
}

internal class ErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal class UserInfoResponse
{
    [JsonPropertyName("nickname")]
    public required string UserName { get; set; }

    [JsonPropertyName("email")]
    public required string Email { get; set; }
}


