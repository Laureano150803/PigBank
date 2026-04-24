data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_iam_policy_document" "redis_policy" {
  statement {
    effect = "Allow"
	actions   = [
		"logs:CreateLogGroup",
		"logs:CreateLogStream",
		"logs:PutLogEvents"
	]
	resources = ["arn:aws:logs:*:*:*"]
  }
  statement {
	effect = "Allow"
	actions   = [
		"ec2:CreateNetworkInterface",
		"ec2:DescribeNetworkInterfaces",
		"ec2:DeleteNetworkInterface",
		"ec2:AttachNetworkInterface",
		"ec2:DetachNetworkInterface"
	]
	resources = ["*"]
  }
}