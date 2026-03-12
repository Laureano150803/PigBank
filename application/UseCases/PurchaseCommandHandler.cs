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
            // 1. Obtener la tarjeta (necesitamos el saldo actual y el tipo)
            var card = await _cardRepository.GetByIdAsync(request.CardId);

            if (card == null)
                throw new KeyNotFoundException("Tarjeta no encontrada.");

            if (card.Status != "ACTIVATED")
                throw new InvalidOperationException("La tarjeta no está activa.");

            // 2. Validar Reglas de Negocio
            if (card.Type == "DEBIT")
            {
                if (card.Balance < request.Amount)
                    throw new InvalidOperationException("Saldo insuficiente en cuenta de ahorros.");

                card.Balance -= request.Amount;
            }
            else if (card.Type == "CREDIT")
            {
                // En crédito, el 'balance' es el cupo total. Necesitamos saber cuánto se ha usado.
                // Por ahora, asumamos que balance es el cupo disponible:
                if (card.Balance < request.Amount)
                    throw new InvalidOperationException("La transacción excede el límite de crédito disponible.");

                card.Balance -= request.Amount;
            }

            // 3. Crear el registro de la transacción
            var transaction = new Transaction
            {
                CardId = request.CardId,
                Amount = request.Amount,
                Merchant = request.Merchant,
                Type = "PURCHASE"
            };

            // 4. Persistir (Idealmente usando una Transacción de DynamoDB)
            // Para mantenerlo simple según tus repositorios actuales:
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
            // 1. Obtener la tarjeta
            var card = await _cardRepository.GetByIdAsync(cardId);

            if (card == null)
                throw new KeyNotFoundException("Tarjeta no encontrada.");

            // 2. Regla de Negocio: Solo Débito puede recibir ahorros (según contrato)
            if (card.Type != "DEBIT")
                throw new InvalidOperationException("Solo se puede añadir saldo a tarjetas de débito.");

            // 3. Actualizar Saldo
            card.Balance += request.Amount;

            // 4. Crear registro de transacción
            var transaction = new Transaction
            {
                CardId = cardId,
                Amount = request.Amount,
                Merchant = request.Merchant,
                Type = "SAVING" // Marcamos como ahorro
            };
            var saveEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), // [cite: 295]
                merchant = "SAVING", // [cite: 296]
                amount = request.Amount, // [cite: 297]
                cardId = cardId // Pasamos cardId para que el notificador busque el correo
            };

            // 5. Persistir cambios
            await _cardRepository.UpdateAsync(card);
            await _transactionRepository.SaveTransactionAsync(transaction);
            await _eventPublisher.PublishNotificationAsync("TRANSACTION.SAVE", saveEvent);
        }
        public async Task PaidCreditCardCommandHandler(string cardId, decimal amount)
        {
            var card = await _cardRepository.GetByIdAsync(cardId);

            if (card == null) throw new KeyNotFoundException("Tarjeta no encontrada.");
            if (card.Type != "CREDIT") throw new InvalidOperationException("Esta operación solo es válida para tarjetas de CRÉDITO.");

            // Liberar cupo: sumamos al balance disponible
            card.Balance += amount;

            var transaction = new Transaction
            {
                CardId = cardId,
                Amount = amount,
                Merchant = "PSE", // Según tu contrato
                Type = "PAYMENT_BALANCE"
            };

            var paidEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), // [cite: 303]
                merchant = "PSE", // [cite: 304]
                amount = amount, // [cite: 305]
                cardId = cardId
            };

            await _cardRepository.UpdateAsync(card);
            await _transactionRepository.SaveTransactionAsync(transaction);
            await _eventPublisher.PublishNotificationAsync("TRANSACTION.PAID", paidEvent);
        }
        public async Task ActivateCardCommandHandler(string userId)
        {
            // 1. Obtener todas las tarjetas del usuario
            var userCards = await _cardRepository.GetByUserIdAsync(userId);

            if (userCards == null || !userCards.Any())
                throw new KeyNotFoundException("No se encontraron tarjetas para este usuario.");

            // 2. Buscar si tiene alguna tarjeta en estado PENDING
            var ActiveCard = userCards.FirstOrDefault(c => c.Status == "ACTIVATED");

            var cardToActivate = userCards.FirstOrDefault(c => c.Status == "PENDING");

            if (cardToActivate == null)
            {
                // Si no hay PENDING, podría ser porque ya todas están ACTIVATED
                if (userCards.Any(c => c.Status == "ACTIVATED"))
                    return; // Ya están activas, no hacemos nada (o puedes lanzar excepción según prefieras)

                throw new InvalidOperationException("No hay tarjetas pendientes por activar.");
            }

            // 3. Contar transacciones de ESA tarjeta específica
            var transactions = await _transactionRepository.GetTransactionsByCardIdAsync(ActiveCard.Uuid);

            // 4. Validar meta de 10 transacciones
            if (transactions.Count >= 10)
            {
                cardToActivate.Status = "ACTIVATED";
                await _cardRepository.UpdateAsync(cardToActivate);

                var activateEvent = new
                {
                    date = DateTime.UtcNow.ToString("O"), // [cite: 283]
                    type = cardToActivate.Type, // [cite: 284]
                    amount = cardToActivate.Balance, // [cite: 285]
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
            // 1. Validar que la tarjeta existe (opcional pero recomendado)
            var card = await _cardRepository.GetByIdAsync(cardId);
            if (card == null)
                throw new KeyNotFoundException("La tarjeta especificada no existe.");

            // 2. Obtener transacciones del repositorio (Asegúrate de tener implementado el GetTransactionsReportAsync que discutimos antes)
            var transactions = await _transactionRepository.GetTransactionsReportAsync(cardId, startDate, endDate);

            if (transactions == null || !transactions.Any())
                throw new InvalidOperationException("No se encontraron transacciones en el rango de fechas.");

            // 3. Construir el archivo CSV
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("ID,CardId,Amount,Merchant,Type,Date");

            foreach (var t in transactions)
            {
                csvBuilder.AppendLine($"{t.Uuid},{t.CardId},{t.Amount},{t.Merchant},{t.Type},{t.CreatedAt}");
            }

            // 4. Configurar cliente S3 (Lo ideal es inyectarlo, pero aquí se crea por simplicidad si no lo tienes en DI)
            var s3Client = new AmazonS3Client();
            var bucketName = Environment.GetEnvironmentVariable("REPORT_BUCKET");
            var fileName = $"reports/{cardId}/{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            // 5. Subir a S3
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = csvBuilder.ToString(),
                ContentType = "text/csv"
            };

            await s3Client.PutObjectAsync(putRequest);

            // 6. Generar URL Pre-firmada válida por 24 horas
            var urlRequest = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = fileName,
                Expires = DateTime.UtcNow.AddHours(24)
            };

            var presignedUrl = s3Client.GetPreSignedURL(urlRequest);

            var reportEvent = new
            {
                date = DateTime.UtcNow.ToString("O"), // [cite: 311]
                url = presignedUrl, // La URL de S3 que acabas de generar [cite: 312]
                cardId = cardId
            };
            await _eventPublisher.PublishNotificationAsync("REPORT.ACTIVITY", reportEvent);

            return presignedUrl;



        }
    }
}
