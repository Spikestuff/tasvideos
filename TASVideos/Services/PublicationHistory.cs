﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using TASVideos.Data;

namespace TASVideos.Services.PublicationChain
{
	public interface IPublicationHistory
	{
		/// <summary>
		/// Returns the publication history for a game,
		/// grouped by non-obsolete publications as the parent node
		/// </summary>
		Task<PublicationHistoryGroup> ForGame(int gameId);
	}

	public class PublicationHistory : IPublicationHistory
	{
		private readonly ApplicationDbContext _db;
		private readonly ICacheService _cache;

		public PublicationHistory(
			ApplicationDbContext db,
			ICacheService cache)
		{
			_db = db;
			_cache = cache;
		}

		public async Task<PublicationHistoryGroup> ForGame(int gameId)
		{
			var game = await _db.Games
				.SingleOrDefaultAsync(g => g.Id == gameId);

			if (game == null)
			{
				return null;
			}

			var parents = new List<PublicationHistoryNode>();

			var publications = await _db.Publications
				.Where(p => p.GameId == gameId)
				.Select(p => new PublicationHistoryNode
				{
					Id = p.Id,
					Title = p.Title,
					Branch = p.Branch,
					ObsoletedById = p.ObsoletedById
				})
				.ToListAsync();

			// TODO: this is an n squared problem. Any way to avoid it?
			// Realistically, no publication history is going to be large enough to cause a major problem
			foreach (var pub in publications)
			{
				pub.ObsoleteList = publications.Where(p => p.ObsoletedById == pub.Id).ToList();
			}

			return new PublicationHistoryGroup
			{
				GameId = gameId,
				Branches = publications
					.Where(p => !p.ObsoletedById.HasValue)
					.ToList()
			};
		}
	}
}
