using System;

namespace FoodLoop.Application.Common.Exceptions.AI;

public abstract class AiServiceException : Exception
{
    protected AiServiceException(string message) : base(message) { }
    protected AiServiceException(string message, Exception innerException) : base(message, innerException) { }
}
