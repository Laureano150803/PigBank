# ==========================================
# 1. Bucket S3 para respaldo del CSV
# ==========================================
resource "aws_s3_bucket" "catalog_bucket" {
  bucket = "pigbank-services-catalog-backup-${random_id.suffix.hex}" # Usamos el suffix que ya tienes en tu main.tf
}

# ==========================================
# 2. La Segunda Lambda (POST /catalog/update)
# ==========================================
resource "aws_lambda_function" "update_catalog_lambda" {
  function_name    = "update-catalog-lambda-function"
  role             = aws_iam_role.lambda_role.arn # Usamos el rol que creamos en redis.tf
  handler          = "DistributedSis::DistributedSis.infrastructure.EntryPoints.CatalogFunctions::UpdateCatalogFunctionHandler" 
  runtime          = "dotnet8"
  filename         = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  timeout          = 30
  memory_size      = 256
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")
  
  vpc_config {
    subnet_ids         = aws_subnet.private[*].id
    security_group_ids = [aws_security_group.lambda_sg.id]
  }
  
  environment {
    variables = {
      REDIS_ENDPOINT      = aws_elasticache_cluster.redis.cache_nodes[0].address
      REDIS_PORT          = aws_elasticache_cluster.redis.port
      CATALOG_BUCKET_NAME = aws_s3_bucket.catalog_bucket.id
    }
  }
  
  depends_on = [
    aws_iam_role_policy_attachment.lambda_redis_attach, 
    aws_elasticache_cluster.redis
  ] 
}

# ==========================================
# 3. Permisos S3 para el Rol de las Lambdas
# ==========================================
resource "aws_iam_policy" "s3_catalog_policy" {
  name        = "s3-catalog-policy"
  description = "Permite a la Lambda guardar los CSV en S3"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["s3:PutObject", "s3:GetObject"]
        Resource = "${aws_s3_bucket.catalog_bucket.arn}/*"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_s3_attach" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = aws_iam_policy.s3_catalog_policy.arn
}

# ==========================================
# 4. Rutas de API Gateway (Corregidas a banking_api)
# ==========================================

# GET /catalog
resource "aws_apigatewayv2_route" "get_catalog_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "GET /catalog"
  target    = "integrations/${aws_apigatewayv2_integration.get_catalog_int.id}"
}

resource "aws_apigatewayv2_integration" "get_catalog_int" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.redis_lambda.invoke_arn # Usa la lambda de tu redis.tf
}

# POST /catalog/update
resource "aws_apigatewayv2_route" "post_catalog_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /catalog/update"
  target    = "integrations/${aws_apigatewayv2_integration.update_catalog_int.id}"
}

resource "aws_apigatewayv2_integration" "update_catalog_int" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.update_catalog_lambda.invoke_arn
}

# ==========================================
# 5. Permisos para que API Gateway invoque las Lambdas (Corregidas a banking_api)
# ==========================================
resource "aws_lambda_permission" "api_gw_get_catalog" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.redis_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*/catalog"
}

resource "aws_lambda_permission" "api_gw_update_catalog" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.update_catalog_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*/catalog/update"
}