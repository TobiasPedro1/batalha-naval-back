using System.IdentityModel.Tokens.Jwt;
using System.Net;
using BatalhaNaval.Application.Interfaces;

namespace BatalhaNaval.API.Middlewares;

public class JwtBlocklistMiddleware
{
    private readonly RequestDelegate _next;

    public JwtBlocklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICacheService cacheService)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

        if (!string.IsNullOrEmpty(token))
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                var jti = jwtToken?.Id;

                if (!string.IsNullOrEmpty(jti))
                {
                    var isBlocked = await cacheService.ExistsAsync($"bl:{jti}");

                    if (isBlocked)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        await context.Response.WriteAsJsonAsync(new
                            { message = "Token inválido ou expirado (Logout efetuado)." });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}