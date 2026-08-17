using FoodLoop.Application.Common.Interfaces;
using Moq;
using System;
using System.Collections.Generic;

namespace FoodLoop.Infrastructure.Tests.TestSupport;

public static class MockLocalizationServiceFactory
{
    private static readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "AccountNotActive", "This account is not active" },
        { "InvalidEmailOrPassword", "Invalid email or password" },
        { "InvalidRole", "Invalid role" },
        { "EmailAlreadyRegistered", "already registered" },
        { "BusinessNameRequired", "Business name is required" }
    };

    public static Mock<ILocalizationService> Create()
    {
        var mock = new Mock<ILocalizationService>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns((string key) => 
            _translations.TryGetValue(key, out var val) ? val : key);
        mock.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string key, object[] args) => 
            _translations.TryGetValue(key, out var val) ? val : key);
        return mock;
    }
}
