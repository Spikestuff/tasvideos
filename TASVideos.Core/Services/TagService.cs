﻿namespace TASVideos.Core.Services;

public enum TagEditResult { Success, Fail, NotFound, DuplicateCode }
public enum TagDeleteResult { Success, Fail, NotFound, InUse }

public interface ITagService
{
	ValueTask<ICollection<Tag>> GetAll();
	ValueTask<Tag?> GetById(int id);
	ValueTask<ListDiff> GetDiff(IEnumerable<int> currentIds, IEnumerable<int> newIds);
	Task<bool> InUse(int id);
	Task<(int? Id, TagEditResult Result)> Add(string code, string displayName);
	Task<TagEditResult> Edit(int id, string code, string displayName);
	Task<TagDeleteResult> Delete(int id);
}

internal class TagService(ApplicationDbContext db, ICacheService cache) : ITagService
{
	internal const string TagsKey = "AllTags";

	public async ValueTask<ICollection<Tag>> GetAll()
	{
		if (cache.TryGetValue(TagsKey, out List<Tag> tags))
		{
			return tags;
		}

		tags = await db.Tags.ToListAsync();
		cache.Set(TagsKey, tags);
		return tags;
	}

	public async ValueTask<Tag?> GetById(int id)
	{
		var tags = await GetAll();
		return tags.SingleOrDefault(t => t.Id == id);
	}

	public async ValueTask<ListDiff> GetDiff(IEnumerable<int> currentIds, IEnumerable<int> newIds)
	{
		var tags = await GetAll();

		var currentTags = tags
			.Where(t => currentIds.Contains(t.Id))
			.Select(t => t.Code)
			.ToList();
		var newTags = tags
			.Where(t => newIds.Contains(t.Id))
			.Select(t => t.Code)
			.ToList();

		return new ListDiff(currentTags, newTags);
	}

	public async Task<bool> InUse(int id) => await db.PublicationTags.AnyAsync(pt => pt.TagId == id);

	public async Task<(int? Id, TagEditResult Result)> Add(string code, string displayName)
	{
		var entry = db.Tags.Add(new Tag
		{
			Code = code,
			DisplayName = displayName
		});

		try
		{
			await db.SaveChangesAsync();
			cache.Remove(TagsKey);
			return (entry.Entity.Id, TagEditResult.Success);
		}
		catch (DbUpdateConcurrencyException)
		{
			return (null, TagEditResult.Fail);
		}
		catch (DbUpdateException ex)
		{
			if (ex.InnerException?.Message.Contains("unique constraint") ?? false)
			{
				return (null, TagEditResult.DuplicateCode);
			}

			return (null, TagEditResult.Fail);
		}
	}

	public async Task<TagEditResult> Edit(int id, string code, string displayName)
	{
		var tag = await db.Tags.FindAsync(id);
		if (tag is null)
		{
			return TagEditResult.NotFound;
		}

		tag.Code = code;
		tag.DisplayName = displayName;

		try
		{
			await db.SaveChangesAsync();
			cache.Remove(TagsKey);
			return TagEditResult.Success;
		}
		catch (DbUpdateConcurrencyException)
		{
			return TagEditResult.Fail;
		}
		catch (DbUpdateException ex)
		{
			if (ex.InnerException?.Message.Contains("unique constraint") ?? false)
			{
				return TagEditResult.DuplicateCode;
			}

			return TagEditResult.Fail;
		}
	}

	public async Task<TagDeleteResult> Delete(int id)
	{
		if (await InUse(id))
		{
			return TagDeleteResult.InUse;
		}

		try
		{
			var tag = await db.Tags.FindAsync(id);
			if (tag is null)
			{
				return TagDeleteResult.NotFound;
			}

			db.Tags.Remove(tag);
			await db.SaveChangesAsync();
			cache.Remove(TagsKey);
		}
		catch (DbUpdateConcurrencyException)
		{
			return TagDeleteResult.Fail;
		}

		return TagDeleteResult.Success;
	}
}
