﻿using Microsoft.AspNetCore.Mvc.Rendering;
using TASVideos.Core.Services.Wiki;
using TASVideos.Data.Entity.Exhibition;
using TASVideos.MovieParsers;
using TASVideos.Pages.Exhibitions.Models;

namespace TASVideos.Pages.Exhibitions;

public class EditModel(
	ApplicationDbContext db,
	IMediaFileUploader uploader,
	IMovieParser parser,
	IWikiPages wikiPages)
	: BasePageModel
{
	[FromRoute]
	public int Id { get; set; }

	[BindProperty]
	public ExhibitionFormModel ExhibitionForm { get; set; } = new();

	public async Task<IActionResult> OnGet()
	{
		var exhibition = await db.Exhibitions
			.Where(e => e.Id == Id)
			.Select(e => new ExhibitionAddEditModel
			{
				ExhibitionId = e.Id,
				Title = e.Title,
				ExhibitionTimestamp = e.ExhibitionTimestamp,
				Games = e.Games.Select(g => g.Id).ToList(),
				Contributors = e.Contributors.Select(e => e.Id).ToList(),
				Movies = e.Files.Where(f => f.Type == ExhibitionFileType.MovieFile).Select(m => new ExhibitionAddEditModel.ExhibitionAddEditMovieModel
				{
					FileId = m.Id,
					MovieFileDescription = m.Description,
				}).ToList(),
				Urls = e.Urls.Select(u => new ExhibitionAddEditModel.ExhibitionAddEditUrlModel
				{
					UrlId = u.Id,
					Type = u.Type,
					DisplayName = u.DisplayName ?? "",
					Url = u.Url,
				}).ToList(),
			})
			.SingleOrDefaultAsync();

		if (exhibition is null)
		{
			return NotFound();
		}

		var page = await wikiPages.Page(WikiHelper.ToExhibitionWikiPageName(Id));

		exhibition.Markup = page?.Markup ?? "";

		ExhibitionForm.Exhibition = exhibition;

		await PopulateDropdowns();

		return Page();
	}

	private async Task PopulateDropdowns()
	{
		ExhibitionForm.AvailableGames = await db.Games
			.OrderBy(g => g.DisplayName)
			.Select(g => new SelectListItem
			{
				Text = g.DisplayName,
				Value = g.Id.ToString()
			})
			.ToListAsync();

		ExhibitionForm.AvailableUsers = await db.Users
			.OrderBy(u => u.UserName)
			.Select(u => new SelectListItem
			{
				Text = u.UserName,
				Value = u.Id.ToString()
			})
			.ToListAsync();

		ExhibitionForm.AvailableStatuses = [ExhibitionStatus.Drafted, ExhibitionStatus.Accepted, ExhibitionStatus.Rejected];
	}

	public async Task<IActionResult> OnPost()
	{
		if (ExhibitionForm.Exhibition.Screenshot != null && !ExhibitionForm.Exhibition.Screenshot.IsValidImage())
		{
			ModelState.AddModelError($"{nameof(ExhibitionForm)}.{nameof(ExhibitionForm.Exhibition)}.{nameof(ExhibitionForm.Exhibition.Screenshot)}", "Invalid file type. Must be .png or .jpg");
		}

		if (!ModelState.IsValid)
		{
			await PopulateDropdowns();
			return Page();
		}

		foreach (var movie in ExhibitionForm.Exhibition.Movies)
		{
			if (movie.MovieFile != null)
			{
				var parseResult = await parser.ParseZip(movie.MovieFile.OpenReadStream());

				if (!parseResult.Success)
				{
					ModelState.AddParseErrors(parseResult);
					await PopulateDropdowns();
					return Page();
				}
			}
		}

		var exhibition = await db.Exhibitions
			.Include(e => e.Games)
			.Include(e => e.Contributors)
			.Include(e => e.Files)
			.Include(e => e.Urls)
			.SingleAsync(e => e.Id == Id);

		exhibition.Title = ExhibitionForm.Exhibition.Title;
		exhibition.ExhibitionTimestamp = ExhibitionForm.Exhibition.ExhibitionTimestamp;
		exhibition.Games = await db.Games.Where(g => ExhibitionForm.Exhibition.Games.Contains(g.Id)).ToListAsync();
		exhibition.Contributors = await db.Users.Where(u => ExhibitionForm.Exhibition.Contributors.Contains(u.Id)).ToListAsync();

		var movieFilesToDelete = exhibition.Files.Where(f => f.Type == ExhibitionFileType.MovieFile).ToList();
		foreach (var movie in ExhibitionForm.Exhibition.Movies)
		{
			if (movie.FileId == 0 && movie.MovieFile != null)
			{
				// add
				byte[] movieFile = await movie.MovieFile.ToBytes();

				exhibition.Files.Add(new ExhibitionFile
				{
					Type = ExhibitionFileType.MovieFile,
					Description = movie.MovieFileDescription,
					FileData = movieFile,
				});
			}
			else if (movie.FileId != 0)
			{
				// edit
				var existingMovie = exhibition.Files.SingleOrDefault(f => f.Id == movie.FileId);
				if (existingMovie != null)
				{
					movieFilesToDelete.Remove(existingMovie);
					existingMovie.Type = ExhibitionFileType.MovieFile;
					existingMovie.Description = movie.MovieFileDescription;
					if (movie.MovieFile != null)
					{
						existingMovie.FileData = await movie.MovieFile.ToBytes();
					}
				}
			}
		}

		// remove
		foreach (var movieFileToDelete in movieFilesToDelete)
		{
			exhibition.Files.Remove(movieFileToDelete);
		}

		var urlsToDelete = exhibition.Urls.ToList();
		foreach (var url in ExhibitionForm.Exhibition.Urls)
		{
			if (url.UrlId == 0)
			{
				// add
				exhibition.Urls.Add(new ExhibitionUrl
				{
					Type = url.Type,
					DisplayName = url.DisplayName,
					Url = url.Url,
				});
			}
			else
			{
				// edit
				var existingUrl = exhibition.Urls.SingleOrDefault(u => u.Id == url.UrlId);
				if (existingUrl != null)
				{
					urlsToDelete.Remove(existingUrl);
					existingUrl.Type = url.Type;
					existingUrl.DisplayName = url.DisplayName;
					existingUrl.Url = url.Url;
				}
			}
		}

		// remove
		foreach (var urlToDelete in urlsToDelete)
		{
			exhibition.Urls.Remove(urlToDelete);
		}

		await db.SaveChangesAsync();

		if (ExhibitionForm.Exhibition.Screenshot != null)
		{
			await uploader.UploadExhibitionScreenshot(exhibition.Id, ExhibitionForm.Exhibition.Screenshot, ExhibitionForm.Exhibition.ScreenshotDescription);
		}

		var exhibitionWikiPageName = WikiHelper.ToExhibitionWikiPageName(Id);
		var page = await wikiPages.Page(exhibitionWikiPageName);
		if (page == null || page.Markup != ExhibitionForm.Exhibition.Markup)
		{
			var wikiPage = new WikiCreateRequest
			{
				RevisionMessage = ExhibitionForm.Exhibition.RevisionMessage,
				PageName = exhibitionWikiPageName,
				MinorEdit = ExhibitionForm.Exhibition.MinorEdit,
				Markup = ExhibitionForm.Exhibition.Markup,
				AuthorId = User.GetUserId()
			};
			var addedWikiPage = await wikiPages.Add(wikiPage);
			await db.SaveChangesAsync();
		}

		var topic = await db.ForumTopics.FirstOrDefaultAsync(t => t.Id == exhibition.TopicId);
		if (topic is not null)
		{
			string topicTitle = $"#{exhibition.Id}: {exhibition.Title}";
			topic.Title = topicTitle;
			await db.SaveChangesAsync();
		}

		return RedirectToPage("View", new { Id });
	}
}
