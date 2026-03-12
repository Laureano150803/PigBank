using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using DistributedSis.domain.interfaces;


namespace DistributedSis.infrastructure.Repository
{
    public class SesEmailSender : IEmailSender
    {
        private readonly IAmazonSimpleEmailServiceV2 _sesClient;
        private readonly string _fromEmail = "laureanohurtado1@gmail.com"; // <-- IMPORTANTE

        public SesEmailSender(IAmazonSimpleEmailServiceV2 sesClient)
        {
            _sesClient = sesClient;
        }
        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var sendRequest = new SendEmailRequest
            {
                // En V2, Source pasa a ser FromEmailAddress
                FromEmailAddress = _fromEmail,
                Destination = new Destination
                {
                    ToAddresses = new List<string> { toEmail }
                },
                // En V2, el Message se envuelve en Content -> Simple
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = subject },
                        Body = new Body
                        {
                            Html = new Content { Data = htmlBody }
                        }
                    }
                }
            };

            await _sesClient.SendEmailAsync(sendRequest);
        }
    }
}
