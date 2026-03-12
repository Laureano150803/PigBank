using Amazon.S3;
using Amazon.S3.Model;
using DistributedSis.application.DTOs;
using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;
namespace DistributedSis.application.UseCases
{
    public class RegisterUserCommandHandler
    {
        private readonly IUserRepository _userRepository;
        private readonly IEventPublisher _eventPublisher;
        private readonly IAmazonS3 _s3Client;
        private readonly string BucketName = Environment.GetEnvironmentVariable("AVATAR_BUCKET_NAME");
        public RegisterUserCommandHandler(IUserRepository userRepository, IEventPublisher eventPublisher, IAmazonS3 amazonS3)
        {
            _userRepository = userRepository;
            _eventPublisher = eventPublisher;
            _s3Client = amazonS3;
        }
        public async Task Handle(CreateUserRequest request)
        {
            var userId = Guid.NewGuid().ToString();
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new User
            {
                Id = userId,
                Name = $"{request.Name} {request.LastName}",
                Email = request.Email,
                Password = hashedPassword,
                IDnumber = request.Document,
                Address = request.Address,
                Phone = request.Phone,
                Image = request.Image
            };
            var loginEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), 
                email = request.Email,
                userId = user.Id
            };
            await _eventPublisher.PublishNotificationAsync("USER.LOGIN", loginEvent);
            await _userRepository.SaveUserAsync(user);
            await _eventPublisher.PublishCardRequestAsync(user.Id, "CREDIT");
            await _eventPublisher.PublishCardRequestAsync(user.Id, "DEBIT");
           
            await _eventPublisher.PublishNotificationAsync("USER.LOGIN", loginEvent);
        }

        public async Task<User> GetUserProfileHandler(string userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("Usuario no encontrado.");
            return user;
        }

        public async Task UpdateUserProfileHandler(string userId, UpdateUserRequest request)
        {
            var existingUser = await _userRepository.GetUserByIdAsync(userId);
            if (existingUser == null) throw new KeyNotFoundException("Usuario no encontrado.");
            existingUser.Name = $"{request.Name} {request.LastName}";
            existingUser.Email = request.Email;
            existingUser.IDnumber = request.Document;
            existingUser.Address = request.Address;
            existingUser.Phone = request.Phone;
            existingUser.Image = request.Image;

            var updateEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), 
                userId = userId
            };
            await _userRepository.UpdateUserAsync(existingUser);
            await _eventPublisher.PublishNotificationAsync("USER.UPDATE", updateEvent);
        }

        public async Task<string> UploadProfileImage(string userId, uploadImageRequest request) {
            byte[] imageBytes = Convert.FromBase64String(request.Image);
            string extension = request.FileType.Split('/')[1];
            string fileName = $"avatars/{userId}.{extension}";
            using (var stream = new MemoryStream(imageBytes))
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = BucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = request.FileType,
                };

                await _s3Client.PutObjectAsync(putRequest);
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            user.Image = fileName;
            await _userRepository.UpdateUserAsync(user);

            return fileName;
        }
    }
}
