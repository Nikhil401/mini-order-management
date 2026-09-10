using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IJwtTokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult Login(LoginRequest request)
    {
        if (request.Username == "admin" && request.Password == "admin123")
        {
            var token = tokenService.CreateToken(request.Username, "Admin");
            return Ok(new { accessToken = token, tokenType = "Bearer" });
        }

        if (request.Username == "user" && request.Password == "user123")
        {
            var token = tokenService.CreateToken(request.Username, "User");
            return Ok(new { accessToken = token, tokenType = "Bearer" });
        }

        return Unauthorized(new { message = "Invalid username or password." });
    }
}
