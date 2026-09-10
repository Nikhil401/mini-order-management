namespace OrderService.Services;

public interface IJwtTokenService
{
    string CreateToken(string username, string role);
}
