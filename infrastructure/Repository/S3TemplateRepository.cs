using Amazon.S3;
using Amazon.S3.Model;
using DistributedSis.domain.interfaces;

namespace DistributedSis.infrastructure.Repository
{
    public class S3TemplateRepository : ITemplateRepository
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public S3TemplateRepository(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
            _bucketName = Environment.GetEnvironmentVariable("TEMPLATE_BUCKET")
                          ?? throw new ArgumentNullException("TEMPLATE_BUCKET no configurado.");
        }
        public async Task<string> GetTemplateAsync(string eventType)
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = $"{eventType}.html"
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync();
        }
    }
}
