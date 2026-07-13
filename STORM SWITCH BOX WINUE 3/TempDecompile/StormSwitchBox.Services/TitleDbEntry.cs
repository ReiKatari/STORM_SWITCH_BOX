using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StormSwitchBox.Services;

public class TitleDbEntry
{
	[JsonPropertyName("id")]
	public string? Id { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("description")]
	public string? Description { get; set; }

	[JsonPropertyName("intro")]
	public string? Intro { get; set; }

	[JsonPropertyName("iconUrl")]
	public string? IconUrl { get; set; }

	[JsonPropertyName("publisher")]
	public string? Publisher { get; set; }

	[JsonPropertyName("developer")]
	public string? Developer { get; set; }

	[JsonPropertyName("releaseDate")]
	public JsonElement? ReleaseDateElement { get; set; }

	[JsonPropertyName("version")]
	public JsonElement? VersionElement { get; set; }

	[JsonPropertyName("versions")]
	public Dictionary<string, string>? VersionsDictionary { get; set; }

	[JsonPropertyName("rating")]
	public JsonElement? RatingElement { get; set; }

	[JsonPropertyName("category")]
	public List<string>? Category { get; set; }

	[JsonPropertyName("languages")]
	public List<string>? Languages { get; set; }

	[JsonPropertyName("regions")]
	public JsonElement? RegionsElement { get; set; }

	[JsonPropertyName("region")]
	public JsonElement? RegionElement { get; set; }

	public string? Version
	{
		get
		{
			try
			{
				if (VersionsDictionary != null && VersionsDictionary.Count > 0)
				{
					string text = VersionsDictionary.Values.LastOrDefault();
					if (!string.IsNullOrEmpty(text))
					{
						return text;
					}
				}
				JsonElement? versionElement = VersionElement;
				if (versionElement.HasValue && versionElement.GetValueOrDefault().ValueKind == JsonValueKind.Number)
				{
					return VersionElement.Value.GetDouble().ToString();
				}
				versionElement = VersionElement;
				if (versionElement.HasValue && versionElement.GetValueOrDefault().ValueKind == JsonValueKind.String)
				{
					return VersionElement.Value.GetString();
				}
			}
			catch
			{
			}
			return null;
		}
	}

	public int? ReleaseDate
	{
		get
		{
			try
			{
				JsonElement? releaseDateElement = ReleaseDateElement;
				if (releaseDateElement.HasValue && releaseDateElement.GetValueOrDefault().ValueKind == JsonValueKind.Number)
				{
					return (int)ReleaseDateElement.Value.GetDouble();
				}
			}
			catch
			{
			}
			return null;
		}
	}

	public int? Rating
	{
		get
		{
			try
			{
				JsonElement? ratingElement = RatingElement;
				if (ratingElement.HasValue && ratingElement.GetValueOrDefault().ValueKind == JsonValueKind.Number)
				{
					return (int)RatingElement.Value.GetDouble();
				}
				ratingElement = RatingElement;
				if (ratingElement.HasValue && ratingElement.GetValueOrDefault().ValueKind == JsonValueKind.String && int.TryParse(RatingElement.Value.GetString(), out var result))
				{
					return result;
				}
			}
			catch
			{
			}
			return null;
		}
	}

	public string? Regions
	{
		get
		{
			try
			{
				JsonElement? regionsElement = RegionsElement;
				if (regionsElement.HasValue && regionsElement.GetValueOrDefault().ValueKind == JsonValueKind.String)
				{
					return RegionsElement.Value.GetString();
				}
				regionsElement = RegionElement;
				if (regionsElement.HasValue && regionsElement.GetValueOrDefault().ValueKind == JsonValueKind.String)
				{
					return RegionElement.Value.GetString();
				}
			}
			catch
			{
			}
			return null;
		}
	}
}
