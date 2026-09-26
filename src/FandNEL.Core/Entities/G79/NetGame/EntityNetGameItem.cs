using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.NetGame;

public class EntityNetGameItem
{
	[JsonPropertyName("item_id")]
	public required string ItemId { get; set; }

	[JsonPropertyName("res_name")]
	public required string ResName { get; set; }

	[JsonPropertyName("brief")]
	public required string Brief { get; set; }

	[JsonPropertyName("tag_names")]
	public required List<string> TagNames { get; set; }

	[JsonPropertyName("title_image_url")]
	public required string TitleImageUrl { get; set; }

	[JsonPropertyName("new_recommend")]
	public required int NewRecommend { get; set; }

	[JsonPropertyName("new_entrance_recommend")]
	public required int NewEntranceRecommend { get; set; }

	[JsonPropertyName("new_recommend_time")]
	public required int NewRecommendTime { get; set; }

	[JsonPropertyName("order")]
	public required string Order { get; set; }

	[JsonPropertyName("is_spigot")]
	public required int IsSpigot { get; set; }

	[JsonPropertyName("stars")]
	public required float Stars { get; set; }

	[JsonPropertyName("entity_id")]
	public required string EntityId { get; set; }

	[JsonPropertyName("online_num")]
	public required string OnlineNum { get; set; }

	[JsonPropertyName("pic_url_list")]
	public required List<string> PicUrlList { get; set; }

	[JsonPropertyName("is_access_by_uid")]
	public required int IsAccessByUid { get; set; }

	[JsonPropertyName("opening_hour")]
	public required string OpeningHour { get; set; }

	[JsonPropertyName("sort_description")]
	public required string SortDescription { get; set; }

	[JsonPropertyName("is_show_online_count")]
	public required int IsShowOnlineCount { get; set; }

	[JsonPropertyName("sort")]
	public required int Sort { get; set; }

	[JsonPropertyName("is_fellow")]
	public required int IsFellow { get; set; }

	[JsonPropertyName("developer_id")]
	public required int DeveloperId { get; set; }

	[JsonPropertyName("friend_play_num")]
	public required int FriendPlayNum { get; set; }

	[JsonPropertyName("week_play_num")]
	public required int WeekPlayNum { get; set; }

	[JsonPropertyName("recommend_sort_num")]
	public required int RecommendSortNum { get; set; }

	[JsonPropertyName("total_play_num")]
	public required int TotalPlayNum { get; set; }

	[JsonPropertyName("create_time")]
	public required int CreateTime { get; set; }

	[JsonPropertyName("running_status")]
	public required string RunningStatus { get; set; }
}
