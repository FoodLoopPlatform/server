using System;

namespace FoodLoop.Application.Common.Exceptions.AI;

public class AiServiceUnavailableException : AiServiceException
{
    public AiServiceUnavailableException(string message) : base(message) { }
    public AiServiceUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
