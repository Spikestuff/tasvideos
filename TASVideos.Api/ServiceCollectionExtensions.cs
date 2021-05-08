﻿using Microsoft.Extensions.DependencyInjection;
using TASVideos.Api.Services;
using TASVideos.Core.Settings;

#pragma warning disable 1591
namespace TASVideos.Api
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddTasvideosApi(this IServiceCollection services, AppSettings.JwtSettings settings)
		{
			services.AddSingleton(settings);
			services.AddScoped<IJwtAuthenticator, JwtAuthenticator>();
			return services;
		}
	}
}