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

            // 1. Descargar la plantilla HTML desde S3
            string htmlTemplate = await _templateRepository.GetTemplateAsync(notificationEvent.Type);

            // 2. Reemplazar las variables dinámicas del HTML con los datos del JSON
            string processedHtml = ReplaceTemplateVariables(htmlTemplate, notificationEvent.Data);

            // 3. Resolver dinámicamente el correo del destinatario
            string userEmail = await ResolveUserEmailAsync(notificationEvent.Data);

            if (string.IsNullOrEmpty(userEmail))
            {
                throw new InvalidOperationException($"No se pudo resolver el correo para la notificación tipo {notificationEvent.Type}. Datos insuficientes.");
            }

            // 4. Enviar el correo usando AWS SES V2
            await _emailSender.SendEmailAsync(userEmail, $"Notificación de tu Banco: {notificationEvent.Type}", processedHtml);

            // 5. Registrar el éxito en DynamoDB
            var log = new NotificationLog
            {
                Type = notificationEvent.Type,
                Content = notificationEvent.Data.GetRawText()
            };
            await _notificationRepository.SaveNotificationAsync(log);
        }

        private string ReplaceTemplateVariables(string html, JsonElement data)
        {
            // Recorre todas las propiedades del JSON y busca {{nombrePropiedad}} en el HTML para reemplazarlo
            foreach (var prop in data.EnumerateObject())
            {
                html = html.Replace($"{{{{{prop.Name}}}}}", prop.Value.ToString());
            }
            return html;
        }
        private async Task<string> ResolveUserEmailAsync(JsonElement data)
        {
            // Estrategia 1: ¿Viene el email explícitamente en el JSON?
            if (data.TryGetProperty("email", out var emailProp) && !string.IsNullOrEmpty(emailProp.GetString()))
            {
                return emailProp.GetString();
            }

            string userId = null;

            // Estrategia 2: ¿Viene un cardId? Buscamos la tarjeta para saber el dueño
            if (data.TryGetProperty("cardId", out var cardIdProp))
            {
                var cardId = cardIdProp.GetString();
                var card = await _cardRepository.GetByIdAsync(cardId);

                if (card != null)
                {
                    userId = card.UserId;
                }
            }

            // Estrategia 3: ¿Viene un userId directamente?
            if (string.IsNullOrEmpty(userId) && data.TryGetProperty("userId", out var userIdProp))
            {
                userId = userIdProp.GetString();
            }

            // Resolución Final: Si conseguimos un userId (por cualquier vía), buscamos al usuario
            if (!string.IsNullOrEmpty(userId))
            {
                // NOTA: Asegúrate de que el método GetByIdAsync exista en tu IUserRepository
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user != null)
                {
                    return user.Email; // Suponiendo que tu entidad User tiene la propiedad Email
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
