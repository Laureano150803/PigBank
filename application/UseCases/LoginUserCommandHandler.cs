using DistributedSis.domain.interfaces;
using DistributedSis.application.DTOs;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
namespace DistributedSis.application.UseCases
{
    public class LoginUserCommandHandler
    {
        private readonly IUserRepository _userRepository;
        private readonly IEventPublisher _eventPublisher;
        private readonly string _jwtSecret;
        public LoginUserCommandHandler(IUserRepository userRepository, IEventPublisher eventPublisher)
        {
            _userRepository = userRepository;
            _eventPublisher = eventPublisher;
            // La clave secreta debería venir de Environment Variables (Secrets Manager idealmente)
            _jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "una_clave_super_secreta_de_32_caracteres";
        }
        public async Task<LoginResponse> ExecuteAsync(LoginRequest request)
        {
            // 1. Buscar el usuario por email
            var user = await _userRepository.GetUserByEmailAsync(request.Email);

            // 2. Validar existencia y password
            // BCrypt.Verify compara el texto plano con el hash guardado
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new UnauthorizedAccessException("Credenciales inválidas.");
            }

            // 3. Generar el Token JWT
            var token = GenerateJwtToken(user.Id, user.Email);
            var loginEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), // [cite: 257]
                email = request.Email,
                userId = user.Id
            };
            await _eventPublisher.PublishNotificationAsync("USER.LOGIN", loginEvent);
            return new LoginResponse(token, "Login exitoso");
        }
        private string GenerateJwtToken(string userId, string email)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                new Claim("userId", userId),
                new Claim(ClaimTypes.Email, email)
            }),
                Expires = DateTime.UtcNow.AddHours(2), // Expira en 2 horas
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

    }
}
