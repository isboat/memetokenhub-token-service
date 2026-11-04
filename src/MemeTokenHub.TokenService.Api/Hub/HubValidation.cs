using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace MemeTokenHub.TokenService.Api.Hub;

public static class HubValidation
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { RespectNullableAnnotations = true };
    public static string? Validate(string kind, JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return "A content object is required.";
        try
        {
            object? model = kind switch
            {
                "upcoming" => data.Deserialize<UpcomingProfileData>(JsonOptions),
                "events" => data.Deserialize<SpaceData>(JsonOptions),
                "applications" => data.Deserialize<ListingApplicationData>(JsonOptions),
                _ => null,
            };
            if (model is null)
                return "Unknown content type.";
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(model, new ValidationContext(model), errors, true))
                return string.Join(" ", errors.Select(x => x.ErrorMessage));
            IEnumerable<string> urls = model switch
            {
                UpcomingProfileData x => [x.LogoUrl, x.WebsiteUrl, x.TelegramUrl, x.TradingUrl],
                SpaceData x => [x.EventUrl, x.RecordingUrl],
                ListingApplicationData x => [x.WebsiteUrl],
                _ => [],
            };
            if (urls.Any(x => x.Length > 0 && !IsPublicHttpsUrl(x)))
                return "Links must use public HTTPS URLs without credentials.";
            if (model is UpcomingProfileData project && project.LaunchAt == default)
                return "A launch date and time are required.";
            if (model is SpaceData space)
            {
                if (space.StartsAt == default)
                    return "An event date and time are required.";
                if (space.EventUrl.Length == 0 && space.RecordingUrl.Length == 0)
                    return "Provide an event or recording link.";
                if (space.Summary.Length > 0 && !space.OwnerReviewed)
                    return "The owner must review the summary before publication.";
            }
            return null;
        }
        catch (JsonException) { return "Invalid content."; }
    }
    public static bool IsPublicHttpsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && !uri.IsLoopback && !string.IsNullOrEmpty(uri.Host);
}
