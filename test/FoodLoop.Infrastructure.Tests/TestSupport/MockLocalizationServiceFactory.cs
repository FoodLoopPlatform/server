using FoodLoop.Application.Common.Interfaces;
using Moq;

namespace FoodLoop.Infrastructure.Tests.TestSupport;

/// <summary>
/// Returns a mock ILocalizationService that echoes the key back as the message.
/// This lets tests assert on handler logic without needing real .resx files loaded.
/// </summary>
public static class MockLocalizationServiceFactory
{
    public static Mock<ILocalizationService> Create()
    {
        var mock = new Mock<ILocalizationService>();

        // this[key] returns the key itself
        mock.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => key);

        // this[key, args] returns the key itself (ignores format args)
        mock.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => key);

        return mock;
    }
}
