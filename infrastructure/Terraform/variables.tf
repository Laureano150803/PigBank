variable "private_subnet_cidr_block" {
  description = "The CIDR block for the private subnet."
  type        = list(string)
  default     = ["10.0.101.0/24","10.0.102.0/24"]
}
variable "public_subnet_cidr_block" {
  description = "The CIDR block for the public subnet."
  type        = list(string)
  default     = ["10.0.1.0/24","10.0.2.0/24"]
}
