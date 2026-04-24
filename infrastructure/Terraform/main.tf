provider "aws" {
  region = "us-east-1"
}

# --- RECURSOS AUXILIARES ---
resource "random_id" "suffix" {
  byte_length = 4
}

# --- DYNAMODB ---
resource "aws_dynamodb_table" "Users" {
  name         = "Users"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"

  attribute {
    name = "Id"
    type = "S"
  }
}
resource "aws_dynamodb_table" "Cards" {
  name           = "Cards"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "uuid"
  range_key      = "createdAt"

  attribute {
    name = "uuid"
    type = "S"
   }
  attribute {
    name = "createdAt"
    type = "S"
   }
  attribute {
    name = "user_id"  
    type = "S"
   }
  global_secondary_index {
    name            = "UserIndex"
    hash_key        = "user_id"
    projection_type = "ALL"
   }
}

resource "aws_dynamodb_table" "Transactions" {
  name           = "Transactions"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "uuid"
  range_key      = "createdAt"

  attribute {
    name = "uuid"
    type = "S"
  }
  attribute {
    name = "createdAt"
    type = "S"
  }
  attribute {
    name = "cardId" # <--- ESTO ES LO QUE FALTABA
    type = "S"
  }
  global_secondary_index {
    name            = "CardIdIndex"
    hash_key        = "cardId"
    range_key       = "createdAt" # Permite ordenar por fecha automáticamente
    projection_type = "ALL"
  }
}
resource "aws_dynamodb_table" "Notifications" {
  name           = "Notifications"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "uuid"
  range_key      = "createdAt"

  attribute {
    name = "uuid"
    type = "S"
  }
  attribute {
    name = "createdAt"
    type = "S"
  }
}

resource "aws_dynamodb_table" "NotificationErrors" {
  name           = "NotificationErrors"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "uuid"
  range_key      = "createdAt"
  attribute {
    name = "uuid"
    type = "S"
  }
  attribute {
    name = "createdAt"
    type = "S"
  }
}

# --- SQS ---
resource "aws_sqs_queue" "create_request_card_dlq" {
  name = "error-create-request-card-sqs"
}
resource "aws_sqs_queue" "create_request_card_sqs" {
  name = "create-request-card-sqs"
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.create_request_card_dlq.arn
    maxReceiveCount     = 3 # Reintenta 3 veces antes de ir a la DLQ
  })
}
resource "aws_sqs_queue" "notification_email_error_sqs" {
  name = "notification-email-error-sqs"
}
resource "aws_sqs_queue" "notification_email_sqs" {
  name = "notification-email-sqs"
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.notification_email_error_sqs.arn
    maxReceiveCount     = 3 # Reintenta 3 veces antes de fallar y mandar a la DLQ
  })
}

# --- S3 (BUCKET PARA AVATARES) ---
resource "aws_s3_bucket" "user_avatars" {
  bucket = "distributed-sis-avatars-${random_id.suffix.hex}"
}
# --- S3 (BUCKET PARA TRANSACTIONS) ---
resource "aws_s3_bucket" "transactions_report_bucket" {
  bucket = "distributed-sis-reports-${random_id.suffix.hex}"
}
# --- S3 (BUCKET PARA PLANTILLAS HTML) ---
resource "aws_s3_bucket" "templates_email_notification" {
  bucket = "templates-email-notification-${random_id.suffix.hex}"
}

# --- IAM (ROLES Y POLÍTICAS) ---
resource "aws_iam_role" "lambda_exec_role" {
  name = "banking_lambda_exec_role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "lambda.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy" "lambda_policy" {
  name = "banking_lambda_policy"
  role = aws_iam_role.lambda_exec_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = "arn:aws:logs:*:*:*"
      },
      {
        Effect = "Allow"
        Action = [
          "dynamodb:PutItem",
          "dynamodb:GetItem",
          "dynamodb:DescribeTable",
          "dynamodb:Scan",
          "dynamodb:Query",
          "dynamodb:UpdateItem"
        ]
        Resource = [
          aws_dynamodb_table.Users.arn,
          "${aws_dynamodb_table.Users.arn}/index/*",
          aws_dynamodb_table.Cards.arn,
          "${aws_dynamodb_table.Cards.arn}/index/*",
          aws_dynamodb_table.Transactions.arn,
          "${aws_dynamodb_table.Transactions.arn}/index/*",
          # --- NUEVAS TABLAS DE NOTIFICACIONES ---
          aws_dynamodb_table.Notifications.arn,
          aws_dynamodb_table.NotificationErrors.arn
        ]
      },
      {
        Effect = "Allow"
        Action = [
          "sqs:SendMessage",
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes"
        ]
        Resource = [
          aws_sqs_queue.create_request_card_sqs.arn,
          aws_sqs_queue.create_request_card_dlq.arn,
          # --- NUEVAS COLAS DE NOTIFICACIONES ---
          aws_sqs_queue.notification_email_sqs.arn,
          aws_sqs_queue.notification_email_error_sqs.arn
        ]
      },
      {
        Effect = "Allow"
        Action = ["s3:PutObject", "s3:PutObjectAcl", "s3:GetObject"]
        Resource = [
          "${aws_s3_bucket.user_avatars.arn}/*",
          "${aws_s3_bucket.transactions_report_bucket.arn}/*",
          # --- NUEVO BUCKET DE PLANTILLAS ---
          "${aws_s3_bucket.templates_email_notification.arn}/*"
        ]
      },
      {
        Effect = "Allow"
        Action = [
          "ses:SendEmail",
          "ses:SendRawEmail"
        ]
        Resource = "*"
      }
    ]
  })
}
# --- LAMBDAS ---

resource "aws_lambda_function" "register_user_lambda" {
  function_name = "register-user-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.RegisterUserFunction::FunctionHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")

  environment {
    variables = {
      CREATE_REQUEST_CARD_SQS_URL = aws_sqs_queue.create_request_card_sqs.url
      NOTIFICATION_QUEUE_URL      = aws_sqs_queue.notification_email_sqs.id
    }
  }
}

resource "aws_lambda_function" "login_user_lambda" {
  function_name = "login-user-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.LoginUserFunction::FunctionHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")

  environment {
    variables = {
      JWT_SECRET = "super_secreta_clave_para_jwt_cambiar_en_produccion"
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}

resource "aws_lambda_function" "get_profile_lambda" {
  function_name = "get-profile-user-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.RegisterUserFunction::GetProfileUserFunction"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")
}

resource "aws_lambda_function" "update_user_lambda" {
  function_name = "update-user-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.RegisterUserFunction::UpdateUserFunction"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")
  environment {
    variables = {
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}

resource "aws_lambda_function" "upload_avatar_user_lambda" {
  function_name = "upload-avatar-user-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.RegisterUserFunction::UploadAvatarUserFunction"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")

  environment {
    variables = {
      AVATAR_BUCKET_NAME = aws_s3_bucket.user_avatars.id
    }
  }
}
resource "aws_lambda_function" "card_approval_worker" {
  function_name = "card-approval-worker"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.CardApprovalFunction::FunctionHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")

  environment {
    variables = {
      CARD_TABLE = aws_dynamodb_table.Cards.name
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}
resource "aws_lambda_function" "card_purchase_lambda" {
  function_name = "card-purchase-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.TransactionFunctions::PurchaseHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout	   = 30
  
  environment {
    variables = {
      CARD_TABLE        = aws_dynamodb_table.Cards.name
      TRANSACTION_TABLE = aws_dynamodb_table.Transactions.name
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}
resource "aws_lambda_function" "card_transaction_save_lambda" {
  function_name = "card-transaction-save-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.TransactionFunctions::SaveBalanceHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30

  environment {
    variables = {
      CARD_TABLE        = aws_dynamodb_table.Cards.name
      TRANSACTION_TABLE = aws_dynamodb_table.Transactions.name
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}
resource "aws_lambda_function" "card_activate_lambda" {
  function_name = "card-activate-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.TransactionFunctions::ActivateCardHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout       = 30
  environment {
    variables = {
      CARD_TABLE             = aws_dynamodb_table.Cards.name
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}
resource "aws_lambda_function" "card_request_failed_lambda" {
  function_name = "card-request-failed"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.CardApprovalFunction::ErrorHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
}
resource "aws_lambda_function" "card_paid_credit_card_lambda" {
  function_name = "card-paid-credit-card-lambda"
  filename      = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler       = "DistributedSis::DistributedSis.infrastructure.EntryPoints.TransactionFunctions::PaidCreditCardHandler"
  role          = aws_iam_role.lambda_exec_role.arn
  runtime       = "dotnet8"
  timeout	   = 30
  environment {
    variables = {
      CARD_TABLE             = aws_dynamodb_table.Cards.name
      TRANSACTION_TABLE      = aws_dynamodb_table.Transactions.name
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}
resource "aws_lambda_function" "card_get_report_lambda" {
  function_name    = "card-get-report-lambda"
  filename         = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler          = "DistributedSis::DistributedSis.infrastructure.EntryPoints.TransactionFunctions::GetReportHandler"
  role             = aws_iam_role.lambda_exec_role.arn
  runtime          = "dotnet8"
  timeout          = 30
  source_code_hash = filebase64sha256("${path.module}/../../bin/Release/net8.0/deploy.zip")

  environment {
    variables = {
      TRANSACTION_TABLE = aws_dynamodb_table.Transactions.name
      REPORT_BUCKET     = aws_s3_bucket.transactions_report_bucket.id
      NOTIFICATION_QUEUE_URL = aws_sqs_queue.notification_email_sqs.id
    }
  }
}
resource "aws_lambda_function" "send_notifications_lambda" {
  function_name    = "send-notifications-lambda"
  filename         = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler          = "DistributedSis::DistributedSis.infrastructure.EntryPoints.NotificationFunctions::SendNotificationHandler"
  role             = aws_iam_role.lambda_exec_role.arn
  runtime          = "dotnet8"
  timeout          = 30
  
  environment {
    variables = {
      TEMPLATE_BUCKET    = aws_s3_bucket.templates_email_notification.id
      NOTIFICATION_TABLE = aws_dynamodb_table.Notifications.name
    }
  }
}
resource "aws_lambda_function" "send_notifications_error_lambda" {
  function_name    = "send-notifications-error-lambda"
  filename         = "${path.module}/../../bin/Release/net8.0/deploy.zip"
  handler          = "DistributedSis::DistributedSis.infrastructure.EntryPoints.NotificationFunctions::ErrorHandler"
  role             = aws_iam_role.lambda_exec_role.arn
  runtime          = "dotnet8"
  timeout          = 30

  environment {
    variables = {
      ERROR_TABLE = aws_dynamodb_table.NotificationErrors.name
    }
  }
}

# Disparador desde la DLQ
resource "aws_lambda_event_source_mapping" "dlq_trigger" {
  event_source_arn = aws_sqs_queue.create_request_card_dlq.arn
  function_name    = aws_lambda_function.card_request_failed_lambda.arn
}

# --- API GATEWAY ---

resource "aws_apigatewayv2_api" "banking_api" {
  name          = "banking-api"
  protocol_type = "HTTP"
}

# --- INTEGRACIONES API GATEWAY ---

resource "aws_apigatewayv2_integration" "register_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.register_user_lambda.invoke_arn
}

resource "aws_apigatewayv2_integration" "login_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.login_user_lambda.invoke_arn
}

resource "aws_apigatewayv2_integration" "get_profile_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.get_profile_lambda.invoke_arn
}

resource "aws_apigatewayv2_integration" "update_profile_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.update_user_lambda.invoke_arn
}

resource "aws_apigatewayv2_integration" "avatar_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.upload_avatar_user_lambda.invoke_arn
}

# Activar Tarjeta
resource "aws_apigatewayv2_integration" "card_activate_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.card_activate_lambda.invoke_arn
}

# Compra (Purchase)
resource "aws_apigatewayv2_integration" "card_purchase_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.card_purchase_lambda.invoke_arn
}

# Ahorro / Carga de Saldo
resource "aws_apigatewayv2_integration" "card_save_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.card_transaction_save_lambda.invoke_arn
}

# Pago de Tarjeta de Crédito
resource "aws_apigatewayv2_integration" "card_paid_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.card_paid_credit_card_lambda.invoke_arn
}

resource "aws_apigatewayv2_integration" "card_report_integration" {
  api_id           = aws_apigatewayv2_api.banking_api.id
  integration_type = "AWS_PROXY"
  integration_uri  = aws_lambda_function.card_get_report_lambda.invoke_arn
}

# --- RUTAS API GATEWAY ---

resource "aws_apigatewayv2_route" "register_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /register"
  target    = "integrations/${aws_apigatewayv2_integration.register_integration.id}"
}

resource "aws_apigatewayv2_route" "login_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /login"
  target    = "integrations/${aws_apigatewayv2_integration.login_integration.id}"
}

resource "aws_apigatewayv2_route" "get_profile_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "GET /profile/{user_id}"
  target    = "integrations/${aws_apigatewayv2_integration.get_profile_integration.id}"
}

resource "aws_apigatewayv2_route" "update_profile_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "PUT /profile/{user_id}"
  target    = "integrations/${aws_apigatewayv2_integration.update_profile_integration.id}"
}

resource "aws_apigatewayv2_route" "avatar_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /profile/{user_id}/avatar"
  target    = "integrations/${aws_apigatewayv2_integration.avatar_integration.id}"
}

resource "aws_apigatewayv2_route" "card_activate_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /card/activate"
  target    = "integrations/${aws_apigatewayv2_integration.card_activate_integration.id}"
}

resource "aws_apigatewayv2_route" "card_purchase_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /transactions/purchase" # Cambiado a /purchase según el contrato JSON
  target    = "integrations/${aws_apigatewayv2_integration.card_purchase_integration.id}"
}

resource "aws_apigatewayv2_route" "card_save_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /transactions/save/{card_id}"
  target    = "integrations/${aws_apigatewayv2_integration.card_save_integration.id}"
}

resource "aws_apigatewayv2_route" "card_paid_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "POST /card/paid/{card_id}"
  target    = "integrations/${aws_apigatewayv2_integration.card_paid_integration.id}"
}

resource "aws_apigatewayv2_route" "card_report_route" {
  api_id    = aws_apigatewayv2_api.banking_api.id
  route_key = "GET /card/{card_id}"
  target    = "integrations/${aws_apigatewayv2_integration.card_report_integration.id}"
}

# --- PERMISOS DE INVOCACIÓN (LAMBDA PERMISSIONS) ---

resource "aws_lambda_permission" "apigw_register" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.register_user_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}

resource "aws_lambda_permission" "apigw_login" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.login_user_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}

resource "aws_lambda_permission" "apigw_get_profile" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.get_profile_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}

resource "aws_lambda_permission" "apigw_update_profile" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.update_user_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}

resource "aws_lambda_permission" "apigw_avatar" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.upload_avatar_user_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}
resource "aws_lambda_permission" "apigw_card_save" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.card_transaction_save_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}
resource "aws_lambda_permission" "apigw_card_purchase" {
  action = "lambda:InvokeFunction"
  function_name = aws_lambda_function.card_purchase_lambda.function_name
  principal = "apigateway.amazonaws.com"
  source_arn = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}
resource "aws_lambda_permission" "apigw_card_active" {
  action = "lambda:InvokeFunction"
  function_name = aws_lambda_function.card_activate_lambda.function_name
  principal = "apigateway.amazonaws.com"
  source_arn = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}
resource "aws_lambda_permission" "card_paid_credit_card" {
  action = "lambda:InvokeFunction"
  function_name = aws_lambda_function.card_paid_credit_card_lambda.function_name
  principal = "apigateway.amazonaws.com"
  source_arn = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}
resource "aws_lambda_permission" "apigw_card_report" {
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.card_get_report_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.banking_api.execution_arn}/*/*"
}
# --- TRIGGER SQS -> LAMBDA ---
resource "aws_lambda_event_source_mapping" "sqs_card_trigger" {
  event_source_arn = aws_sqs_queue.create_request_card_sqs.arn
  function_name    = aws_lambda_function.card_approval_worker.arn
  batch_size       = 5
}
resource "aws_lambda_event_source_mapping" "sqs_notification_trigger" {
  event_source_arn = aws_sqs_queue.notification_email_sqs.arn
  function_name    = aws_lambda_function.send_notifications_lambda.arn
  batch_size       = 5
}

resource "aws_lambda_event_source_mapping" "dlq_notification_trigger" {
  event_source_arn = aws_sqs_queue.notification_email_error_sqs.arn
  function_name    = aws_lambda_function.send_notifications_error_lambda.arn
  batch_size       = 5
}

# --- DEPLOYMENT STAGE & OUTPUTS ---

resource "aws_apigatewayv2_stage" "default_stage" {
  api_id      = aws_apigatewayv2_api.banking_api.id
  name        = "$default"
  auto_deploy = true
}

output "api_endpoint" {
  value       = aws_apigatewayv2_api.banking_api.api_endpoint
  description = "banking_apiLH"
}