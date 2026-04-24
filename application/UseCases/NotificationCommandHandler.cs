using DistributedSis.application.DTOs;
using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;
using DistributedSis.infrastructure.Repository;
using System.Text.Json;

namespace DistributedSis.application.UseCases
{
    public class NotificationCommandHandler
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly ITemplateRepository _templateRepository;
        private readonly IEmailSender _emailSender;
        private readonly ICardRepository _cardRepository;
        private readonly IUserRepository _userRepository;
        public NotificationCommandHandler(
            INotificationRepository notificationRepository,
            ITemplateRepository templateRepository,
            IEmailSender emailSender,
            ICardRepository cardRepository,
            IUserRepository userRepository)
        {
            _notificationRepository = notificationRepository;
            _templateRepository = templateRepository;
            _emailSender = emailSender;
            _cardRepository = cardRepository;
            _userRepository = userRepository;
        }

        public async Task ProcessNotificationAsync(NotificationMessageDto notificationEvent)
        {
            if (string.IsNullOrEmpty(notificationEvent.Type))
            {
                throw new ArgumentException("El tipo de evento es nulo o inválido.");
            }

            string htmlTemplate = await _templateRepository.GetTemplateAsync(notificationEvent.Type);
            string processedHtml = ReplaceTemplateVariables(htmlTemplate, notificationEvent.Data);
            string userEmail = await ResolveUserEmailAsync(notificationEvent.Data);

            if (string.IsNullOrEmpty(userEmail))
            {
                throw new InvalidOperationException($"No se pudo resolver el correo para la notificación tipo {notificationEvent.Type}. Datos insuficientes.");
            }

            await _emailSender.SendEmailAsync(userEmail, $"Notificación de tu Banco: {notificationEvent.Type}", processedHtml);

            var log = new NotificationLog
            {
                Type = notificationEvent.Type,
                Content = notificationEvent.Data.GetRawText()
            };
            await _notificationRepository.SaveNotificationAsync(log);
        }

        private string ReplaceTemplateVariables(string html, JsonElement data)
        {
            foreach (var prop in data.EnumerateObject())
            {
                html = html.Replace($"{{{{{prop.Name}}}}}", prop.Value.ToString());
            }
            return html;
        }
        private async Task<string> ResolveUserEmailAsync(JsonElement data)
        {
            if (data.TryGetProperty("email", out var emailProp) && !string.IsNullOrEmpty(emailProp.GetString()))
            {
                return emailProp.GetString();
            }

            string userId = null;

            if (data.TryGetProperty("cardId", out var cardIdProp))
            {
                var cardId = cardIdProp.GetString();
                var card = await _cardRepository.GetByIdAsync(cardId);

                if (card != null)
                {
                    userId = card.UserId;
                }
            }

            if (string.IsNullOrEmpty(userId) && data.TryGetProperty("userId", out var userIdProp))
            {
                userId = userIdProp.GetString();
            }


            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user != null)
                {
                    return user.Email; 
                }
            }

            return null;
        }
        public async Task ProcessErrorAsync(string failedMessageBody)
        {
            var errorLog = new NotificationErrorLog
            {
                ErrorReason = "El mensaje falló 3 veces en la cola principal.",
                FailedMessage = failedMessageBody
            };

            await _notificationRepository.SaveErrorAsync(errorLog);
        }
    }
}
