﻿using System.Text;

namespace TASVideos.Middleware;

#pragma warning disable CS9113 // Parameter is unread.
public class RobotHandlingMiddleware(RequestDelegate request, IHostEnvironment env)
{
	public async Task Invoke(HttpContext context)
	{
		var sb = new StringBuilder();

		if (env.IsProduction())
		{
			sb.AppendLine("""
						User-agent: *
						Disallow: /Movies-
						Disallow: /MovieMaintenanceLog
						Disallow: /UserMaintenanceLog
						Disallow: /InternalSystem/
						Disallow: /*?revision=*
						Disallow: /Wiki/PageHistory

						User-agent: Fasterfox
						Disallow: /
						""");
		}
		else
		{
			sb
				.AppendLine("User-agent: *")
				.AppendLine("Disallow: / ");
		}

		context.Response.StatusCode = 200;
		context.Response.ContentType = "text/plain";
		await context.Response.WriteAsync(sb.ToString());
	}
}
