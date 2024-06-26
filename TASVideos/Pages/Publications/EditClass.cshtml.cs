﻿namespace TASVideos.Pages.Publications;

[RequirePermission(PermissionTo.SetPublicationClass)]
public class EditClassModel(
	ApplicationDbContext db,
	ExternalMediaPublisher publisher,
	IPublicationMaintenanceLogger publicationMaintenanceLogger)
	: BasePageModel
{
	[FromRoute]
	public int Id { get; set; }

	[BindProperty]
	public PublicationClassEdit Publication { get; set; } = new();

	[BindProperty]
	public string Title { get; set; } = "";

	public List<SelectListItem> AvailableClasses { get; set; } = [];

	public async Task<IActionResult> OnGet()
	{
		var publication = await db.Publications
			.Where(p => p.Id == Id)
			.Select(p => new PublicationClassEdit
			{
				Title = p.Title,
				PublicationClass = p.PublicationClassId
			})
			.SingleOrDefaultAsync();

		if (publication is null)
		{
			return NotFound();
		}

		Publication = publication;
		Title = Publication.Title;
		await PopulateAvailableClasses();
		return Page();
	}

	public async Task<IActionResult> OnPost()
	{
		if (!ModelState.IsValid)
		{
			await PopulateAvailableClasses();
			return Page();
		}

		var publication = await db.Publications
			.Include(p => p.PublicationClass)
			.SingleOrDefaultAsync(p => p.Id == Id);

		if (publication is null)
		{
			return NotFound();
		}

		var publicationClass = await db.PublicationClasses
			.SingleOrDefaultAsync(t => t.Id == Publication.PublicationClass);

		if (publicationClass is null)
		{
			return NotFound();
		}

		if (publication.PublicationClassId != Publication.PublicationClass)
		{
			var originalClass = publication.PublicationClass!.Name;
			publication.PublicationClassId = Publication.PublicationClass;

			var log = $"{Id}M Class changed from {originalClass} to {publicationClass.Name}";
			await publicationMaintenanceLogger.Log(Id, User.GetUserId(), log);

			var result = await db.TrySaveChanges();
			SetMessage(result, log, "Unable to update Publication Class");
			if (result.IsSuccess())
			{
				await publisher.SendPublicationClassChange(
					Id, Title, User.Name(), originalClass, publicationClass.Name);
			}
		}

		return RedirectToPage("Edit", new { Id });
	}

	private async Task PopulateAvailableClasses()
	{
		AvailableClasses = await db.PublicationClasses.ToDropDownList();
	}

	public class PublicationClassEdit
	{
		public string Title { get; init; } = "";
		public int PublicationClass { get; init; }
	}
}
