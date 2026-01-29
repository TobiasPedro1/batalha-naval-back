using System.IdentityModel.Tokens.Jwt;
using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatalhaNaval.API.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IUserRepository _userRepository;

    public AuthController(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ITokenService tokenService,
        ICacheService cacheService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _cacheService = cacheService;
    }


    /// <summary>
    ///     Efetua o login de um usuário.
    /// </summary>
    /// <remarks>
    ///     Efetua o login de um usuário com nome de usuário e senha fornecidos.
    /// </remarks>
    /// <response code="200">Login efetuado com sucesso.</response>
    /// <response code="401">Nome de usuário ou senha inválidos.</response>
    [HttpPost("login", Name = "PostLogin")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var user = await _userRepository.GetByUsernameAsync(loginDto.Username);

        if (user == null || !_passwordService.VerifyPassword(loginDto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Usuário ou senha inválidos.", StatusCode = 401 });

        var token = _tokenService.GenerateToken(user);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Expiration = DateTime.UtcNow.AddHours(2),
            Username = user.Username,
            Profile = user.Profile != null
                ? new UserProfileDTO
                {
                    RankPoints = user.Profile.RankPoints,
                    Wins = user.Profile.Wins,
                    Losses = user.Profile.Losses
                }
                : new UserProfileDTO()
        });
    }

    /// <summary>
    ///     Realiza o logout invalidando o token atual.
    /// </summary>
    /// <remarks>
    ///     Adiciona o JTI (ID do token) a uma blocklist no Redis até sua expiração natural.
    /// </remarks>
    /// <response code="200">Logout realizado com sucesso.</response>
    /// <response code="401">Token inválido ou não fornecido.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
            return BadRequest("Token não fornecido.");

        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        if (jsonToken == null)
            return BadRequest("Token inválido.");

        var jti = jsonToken.Id;
        var expiration = jsonToken.ValidTo;

        var timeRemaining = expiration - DateTime.UtcNow;

        if (timeRemaining <= TimeSpan.Zero)
            return Ok(new { message = "Logout realizado (token já expirado)." });

        await _cacheService.SetAsync($"bl:{jti}", "revoked", timeRemaining);

        return Ok(new { message = "Logout realizado com sucesso." });
    }
}