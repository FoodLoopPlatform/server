using System;

namespace FoodLoop.Application.Common.Exceptions.AI;

public class AiServiceContractException : AiServiceException
{
    public AiServiceContractException(string message) : base(message) { }
    public AiServiceContractException(string message, Exception innerException) : base(message, innerException) { }
}
