using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CodeConverterCLI.Models;

internal class AccessTokenRequest
{
    [JsonPropertyName("deviceCode")]
    public required string DeviceCode { get; set; }
}

internal class UserInfoRequest
{
    [JsonPropertyName("Token")]
    public required string AccessToken { get; set; }
}

