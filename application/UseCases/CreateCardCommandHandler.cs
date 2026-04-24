using DistributedSis.application.DTOs;
using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;

namespace DistributedSis.application.UseCases
{
    public class CreateCardCommandHandler
    {
        private readonly ICardRepository _cardRepository;
        private readonly IEventPublisher _eventPublisher;

        public CreateCardCommandHandler(ICardRepository cardRepository, IEventPublisher eventPublisher)
        {
            _cardRepository = cardRepository;
            _eventPublisher = eventPublisher;
        }

        public async Task ExecuteAsync(CardRequestMessage request)
        {
            var newCard = new Card
            {
                UserId = request.UserId,
                Type = request.request.ToUpper() 
            };

            if (newCard.Type == "DEBIT")
            {
                newCard.Status = "ACTIVATED";
                newCard.Balance = 0;
            }
            else if (newCard.Type == "CREDIT")
            {
                var random = new Random();
                double score = random.Next(0, 101); 
                decimal amount = 100m + (decimal)(score / 100.0) * (10000000m - 100m);

                newCard.Status = "PENDING";
                newCard.Balance = Math.Round(amount, 2);
            }
            else
            {
                throw new ArgumentException($"Tipo de tarjeta no soportado: {newCard.Type}");
            }
            var cardCreateEvent = new
            {
                date = DateTime.UtcNow.ToString("O"),
                type = newCard.Type,
                amount = newCard.Type == "DEBIT" ? 0 : newCard.Balance,
                userId = newCard.UserId
            };

            await _cardRepository.SaveAsync(newCard);
            await _eventPublisher.PublishNotificationAsync("CARD.CREATE", cardCreateEvent);
        }
    }
}