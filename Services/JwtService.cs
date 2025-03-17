using COMAVI_SA.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace COMAVI_SA.Services
{
    public interface IJwtService
    {
        string GenerateJwtToken(Usuario user);
        ClaimsPrincipal ValidateToken(string token);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateJwtToken(Usuario user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Secret"] ?? "COMAVI_DefaultSecretKey_ForDevelopmentOnly");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.id_usuario.ToString()),
                new Claim(ClaimTypes.Name, user.nombre_usuario),
                new Claim(ClaimTypes.Email, user.correo_electronico),
                new Claim(ClaimTypes.Role, user.rol)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:ExpirationInMinutes"] ?? "60")),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                ),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Secret"] ?? "COMAVI_DefaultSecretKey_ForDevelopmentOnly");

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["JwtSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["JwtSettings:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch
            {
                return null;
            }
        }
    }
}