using System;

namespace FoodLoop.Application.Common.Exceptions.AI;

public class AiServiceValidationException : AiServiceException
{
    public string RawResponseBody { get; }

    public AiServiceValidationException(string message, string rawResponseBody) 
        : base($"{message} - Raw Response: {rawResponseBody}")
    {
        RawResponseBody = rawResponseBody;
    }

    public AiServiceValidationException(string message, string rawResponseBody, Exception innerException) 
        : base($"{message} - Raw Response: {rawResponseBody}", innerException)
    {
        RawResponseBody = rawResponseBody;
    }
}
