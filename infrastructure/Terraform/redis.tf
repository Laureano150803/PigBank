# --- VPC y Networking Base ---
resource "aws_vpc" "main" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true
}

# Internet Gateway
resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id
}

# Public subnet
resource "aws_subnet" "public" {
  count             = length(var.public_subnet_cidr_block)
  vpc_id            = aws_vpc.main.id
  cidr_block        = var.public_subnet_cidr_block[count.index]
}

# Private subnet
resource "aws_subnet" "private" {
  count             = length(var.private_subnet_cidr_block)
  vpc_id            = aws_vpc.main.id
  cidr_block        = var.private_subnet_cidr_block[count.index]
  availability_zone = data.aws_availability_zones.available.names[count.index]
}

# Route table public
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id
  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }
}

# Route table private
resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id
}

# Route table association public
resource "aws_route_table_association" "public" {
  count          = length(aws_subnet.public)
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# Route table association private
resource "aws_route_table_association" "private" {
  count          = length(aws_subnet.private)
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private.id
}

# --- Security Groups ---

# Security group for lambda to access redis
resource "aws_security_group" "lambda_sg" {
  name        = "lambda-sg"
  description = "Security group for Lambda to access Redis"
  vpc_id      = aws_vpc.main.id
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# Security group for Redis cluster
resource "aws_security_group" "redis_sg" {
  name        = "redis-cluster-sg"
  description = "Security group for Redis cluster"
  vpc_id      = aws_vpc.main.id
  ingress {
    description     = "Redis for lambda"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    # SOLUCIÓN 1: Referenciamos el SG de la Lambda
    security_groups = [aws_security_group.lambda_sg.id] 
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# --- Redis ElastiCache ---

# Subnet group for elasticache
resource "aws_elasticache_subnet_group" "redis_subnet" {
  name       = "redis-cluster-subnet-group"
  subnet_ids = aws_subnet.private[*].id
}

# Cluster redis
resource "aws_elasticache_cluster" "redis" {
  cluster_id           = "redis-cluster"
  engine               = "redis"
  node_type            = "cache.t3.micro"
  num_cache_nodes      = 1
  parameter_group_name = "default.redis7"
  port                 = 6379
  security_group_ids   = [aws_security_group.redis_sg.id]
  subnet_group_name    = aws_elasticache_subnet_group.redis_subnet.id
}

# --- IAM Roles y Políticas ---

# Lambda role
resource "aws_iam_role" "lambda_role" {
  name = "name-lamda-role"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })
}

# Lambda policy
resource "aws_iam_policy" "lambda_policy" {
  name        = "lambda-policy"
  description = "Policy for Lambda to access ElastiCache"
  policy      = data.aws_iam_policy_document.redis_policy.json
}

# SOLUCIÓN 2: Adjuntar la política al rol
resource "aws_iam_role_policy_attachment" "lambda_redis_attach" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = aws_iam_policy.lambda_policy.arn
}

# --- Lambda Function ---

resource "aws_lambda_function" "redis_lambda" {
  function_name    = "catalog-lambda-function"
  role             = aws_iam_role.lambda_role.arn
  # SOLUCIÓN 3: Handler formato .NET
  handler          = "DistributedSis::DistributedSis.infrastructure.EntryPoints.CatalogFunctions::GetCatalogFunctionHandler" 
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
      # SOLUCIÓN 4: Nombres correctos del recurso redis
      REDIS_ENDPOINT = aws_elasticache_cluster.redis.cache_nodes[0].address
      REDIS_PORT     = aws_elasticache_cluster.redis.port
    }
  }
  
  # SOLUCIÓN 5: Dependencias corregidas (sin archive_file)
  depends_on = [
    aws_iam_role_policy_attachment.lambda_redis_attach, 
    aws_elasticache_cluster.redis
  ] 
}

# --- VPC Endpoints (SOLUCIÓN 6: Para evitar que la Lambda pierda salida a servicios AWS) ---

# VPC Endpoint para DynamoDB
resource "aws_vpc_endpoint" "dynamodb" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.us-east-1.dynamodb"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.private.id]
}

# VPC Endpoint para S3
resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.us-east-1.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.private.id]
}