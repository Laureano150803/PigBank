using Amazon.S3;
using Amazon.S3.Model;
using DistributedSis.application.DTOs;
using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;
using System.Text;

namespace DistributedSis.application.UseCases
{
    public class PurchaseCommandHandler
    {
        private readonly ICardRepository _cardRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IEventPublisher _eventPublisher;

        public PurchaseCommandHandler(ICardRepository cardRepository, ITransactionRepository transactionRepository, IEventPublisher eventPublisher)
        {
            _cardRepository = cardRepository;
            _transactionRepository = transactionRepository;
            _eventPublisher = eventPublisher;
        }
        public async Task ExecuteAsync(PurchaseRequest request)
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);

            if (card == null)
                throw new KeyNotFoundException("Tarjeta no encontrada.");

            if (card.Status != "ACTIVATED")
                throw new InvalidOperationException("La tarjeta no está activa.");

            if (card.Type == "DEBIT")
            {
                if (card.Balance < request.Amount)
                    throw new InvalidOperationException("Saldo insuficiente en cuenta de ahorros.");

                card.Balance -= request.Amount;
            }
            else if (card.Type == "CREDIT")
            {
                if (card.Balance < request.Amount)
                    throw new InvalidOperationException("La transacción excede el límite de crédito disponible.");

                card.Balance -= request.Amount;
            }

            var transaction = new Transaction
            {
                CardId = request.CardId,
                Amount = request.Amount,
                Merchant = request.Merchant,
                Type = "PURCHASE"
            };

            var eventData = new
            {
                date = DateTime.UtcNow.ToString("O"),
                merchant = request.Merchant,
                cardId = request.CardId,
                amount = request.Amount
            };
            await _cardRepository.UpdateAsync(card);
            await _transactionRepository.SaveTransactionAsync(transaction);
            await _eventPublisher.PublishNotificationAsync("TRANSACTION.PURCHASE", eventData);
        }
        public async Task SaveBalanceCommandHandler(string cardId, SaveBalanceRequest request)
        {

            var card = await _cardRepository.GetByIdAsync(cardId);

            if (card == null)
                throw new KeyNotFoundException("Tarjeta no encontrada.");

            if (card.Type != "DEBIT")
                throw new InvalidOperationException("Solo se puede añadir saldo a tarjetas de débito.");

            card.Balance += request.Amount;

            var transaction = new Transaction
            {
                CardId = cardId,
                Amount = request.Amount,
                Merchant = request.Merchant,
                Type = "SAVING"
            };
            var saveEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), 
                merchant = "SAVING", 
                amount = request.Amount, 
                cardId = cardId 
            };

            await _cardRepository.UpdateAsync(card);
            await _transactionRepository.SaveTransactionAsync(transaction);
            await _eventPublisher.PublishNotificationAsync("TRANSACTION.SAVE", saveEvent);
        }
        public async Task PaidCreditCardCommandHandler(string cardId, decimal amount)
        {
            var card = await _cardRepository.GetByIdAsync(cardId);

            if (card == null) throw new KeyNotFoundException("Tarjeta no encontrada.");
            if (card.Type != "CREDIT") throw new InvalidOperationException("Esta operación solo es válida para tarjetas de CRÉDITO.");

            card.Balance += amount;

            var transaction = new Transaction
            {
                CardId = cardId,
                Amount = amount,
                Merchant = "PSE", 
                Type = "PAYMENT_BALANCE"
            };

            var paidEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), 
                merchant = "PSE", 
                amount = amount, 
                cardId = cardId
            };

            await _cardRepository.UpdateAsync(card);
            await _transactionRepository.SaveTransactionAsync(transaction);
            await _eventPublisher.PublishNotificationAsync("TRANSACTION.PAID", paidEvent);
        }
        public async Task ActivateCardCommandHandler(string userId)
        {
            var userCards = await _cardRepository.GetByUserIdAsync(userId);

            if (userCards == null || !userCards.Any())
                throw new KeyNotFoundException("No se encontraron tarjetas para este usuario.");

            var ActiveCard = userCards.FirstOrDefault(c => c.Status == "ACTIVATED");

            var cardToActivate = userCards.FirstOrDefault(c => c.Status == "PENDING");

            if (cardToActivate == null)
            {
                if (userCards.Any(c => c.Status == "ACTIVATED"))
                    return; 

                throw new InvalidOperationException("No hay tarjetas pendientes por activar.");
            }

            var transactions = await _transactionRepository.GetTransactionsByCardIdAsync(ActiveCard.Uuid);

            if (transactions.Count >= 10)
            {
                cardToActivate.Status = "ACTIVATED";
                await _cardRepository.UpdateAsync(cardToActivate);

                var activateEvent = new
                {
                    date = DateTime.UtcNow.ToString("O"), 
                    type = cardToActivate.Type, 
                    amount = cardToActivate.Balance, 
                    userId = cardToActivate.UserId
                };
                await _eventPublisher.PublishNotificationAsync("CARD.ACTIVATE", activateEvent);
            }
            else
            {
                throw new InvalidOperationException($"Faltan {10 - transactions.Count} transacciones para poder activar la tarjeta {cardToActivate.Type}.");
            }
        }
        public async Task<string> GenerateReportAsync(string cardId, string startDate, string endDate)
        {
            var card = await _cardRepository.GetByIdAsync(cardId);
            if (card == null)
                throw new KeyNotFoundException("La tarjeta especificada no existe.");

            var transactions = await _transactionRepository.GetTransactionsReportAsync(cardId, startDate, endDate);

            if (transactions == null || !transactions.Any())
                throw new InvalidOperationException("No se encontraron transacciones en el rango de fechas.");

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("ID,CardId,Amount,Merchant,Type,Date");

            foreach (var t in transactions)
            {
                csvBuilder.AppendLine($"{t.Uuid},{t.CardId},{t.Amount},{t.Merchant},{t.Type},{t.CreatedAt}");
            }

            var s3Client = new AmazonS3Client();
            var bucketName = Environment.GetEnvironmentVariable("REPORT_BUCKET");
            var fileName = $"reports/{cardId}/{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = csvBuilder.ToString(),
                ContentType = "text/csv"
            };

            await s3Client.PutObjectAsync(putRequest);

            var urlRequest = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = fileName,
                Expires = DateTime.UtcNow.AddHours(24)
            };

            var presignedUrl = s3Client.GetPreSignedURL(urlRequest);

            var reportEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), 
                url = presignedUrl, 
                cardId = cardId
            };
            await _eventPublisher.PublishNotificationAsync("REPORT.ACTIVITY", reportEvent);

            return presignedUrl;



        }
    }
}
