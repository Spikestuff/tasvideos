﻿using TASVideos.Data.Entity.Forum;

namespace TASVideos.Pages.Forum.Topics;

[RequirePermission(PermissionTo.SplitTopics)]
public class SplitModel(
	ApplicationDbContext db,
	ExternalMediaPublisher publisher,
	IForumService forumService)
	: BasePageModel
{
	[FromRoute]
	public int Id { get; set; }

	[BindProperty]
	public TopicSplit Topic { get; set; } = new();

	public List<SelectListItem> AvailableForums { get; set; } = [];

	private bool CanSeeRestricted => User.Has(PermissionTo.SeeRestrictedForums);

	public async Task<IActionResult> OnGet()
	{
		var splitTopic = await PopulatePosts();
		if (splitTopic is null)
		{
			return NotFound();
		}

		Topic = splitTopic;
		await PopulateAvailableForums();
		return Page();
	}

	public async Task<IActionResult> OnPost()
	{
		if (!ModelState.IsValid)
		{
			await PopulatePosts();
			await PopulateAvailableForums();
			return Page();
		}

		var topic = await db.ForumTopics
			.Include(t => t.Forum)
			.Include(t => t.ForumPosts)
			.ExcludeRestricted(CanSeeRestricted)
			.SingleOrDefaultAsync(t => t.Id == Id);

		if (topic is null)
		{
			return NotFound();
		}

		var destinationForum = await db.Forums
			.ExcludeRestricted(CanSeeRestricted)
			.SingleOrDefaultAsync(f => f.Id == Topic.CreateNewTopicIn);

		if (destinationForum is null)
		{
			return NotFound();
		}

		var selectedPosts = Topic.Posts
			.Where(tp => tp.Selected)
			.Select(tp => tp.Id)
			.ToList();
		var postsToSplit = topic.ForumPosts
			.Where(p => selectedPosts.Contains(p.Id))
			.ToList();

		if (!postsToSplit.Any())
		{
			var splitOnPost = topic.ForumPosts
				.SingleOrDefault(p => p.Id == Topic.SplitPostsStartingAt);

			if (splitOnPost is null)
			{
				await PopulatePosts();
				await PopulateAvailableForums();
				return Page();
			}

			postsToSplit = topic.ForumPosts
				.Where(p => p.Id == splitOnPost.Id
					|| p.CreateTimestamp > splitOnPost.CreateTimestamp)
				.ToList();
		}

		var newTopic = new ForumTopic
		{
			Type = ForumTopicType.Regular,
			Title = Topic.NewTopicName,
			PosterId = User.GetUserId(),
			ForumId = Topic.CreateNewTopicIn
		};

		db.ForumTopics.Add(newTopic);
		await db.SaveChangesAsync();

		foreach (var post in postsToSplit)
		{
			post.TopicId = newTopic.Id;
			post.ForumId = destinationForum.Id;
		}

		await db.SaveChangesAsync();

		forumService.ClearLatestPostCache();
		forumService.ClearTopicActivityCache();

		await publisher.SendForum(
			destinationForum.Restricted || topic.Forum!.Restricted,
			$"[Topic]({{0}}) SPLIT by {User.Name()}",
			$"\"{newTopic.Title}\" from \"{Topic.Title}\"",
			$"Forum/Topics/{newTopic.Id}");

		return RedirectToPage("Index", new { id = newTopic.Id });
	}

	private async Task<TopicSplit?> PopulatePosts()
	{
		return await db.ForumTopics
			.ExcludeRestricted(CanSeeRestricted)
			.Where(t => t.Id == Id)
			.Select(t => new TopicSplit
			{
				Title = t.Title,
				NewTopicName = "(Split from " + t.Title + ")",
				CreateNewTopicIn = t.Forum!.Id,
				ForumId = t.Forum.Id,
				ForumName = t.Forum.Name,
				Posts = t.ForumPosts
					.Select(p => new TopicSplit.Post
					{
						Id = p.Id,
						PostCreateTimestamp = p.CreateTimestamp,
						EnableBbCode = p.EnableBbCode,
						EnableHtml = p.EnableHtml,
						Subject = p.Subject,
						Text = p.Text,
						PosterName = p.Poster!.UserName,
						PosterAvatar = p.Poster.Avatar
					})
					.OrderBy(p => p.PostCreateTimestamp)
					.ToList()
			})
			.SingleOrDefaultAsync();
	}

	private async Task PopulateAvailableForums()
	{
		AvailableForums = await db.Forums.ToDropdownList(CanSeeRestricted, Topic.ForumId);
	}

	public class TopicSplit
	{
		public int? SplitPostsStartingAt { get; init; }
		public int CreateNewTopicIn { get; init; }
		public string NewTopicName { get; init; } = "";
		public string Title { get; init; } = "";
		public int ForumId { get; init; }
		public string ForumName { get; init; } = "";
		public List<Post> Posts { get; init; } = [];

		public class Post
		{
			public int Id { get; init; }
			public DateTime PostCreateTimestamp { get; init; }
			public bool EnableHtml { get; init; }
			public bool EnableBbCode { get; init; }
			public string? Subject { get; init; }
			public string Text { get; init; } = "";
			public string PosterName { get; init; } = "";
			public string? PosterAvatar { get; init; }
			public bool Selected { get; init; }
		}
	}
}
