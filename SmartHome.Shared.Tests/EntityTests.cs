using SmartHome.Shared.Entities;

namespace SmartHome.Shared.Tests;

public sealed class EntityTests
{
    [Fact]
    public void Entities_initialize_required_text_properties()
    {
        var entities = new object[]
        {
            new Usuario(),
            new OauthCode(),
            new OauthToken(),
            new HistorialReproduccion()
        };

        foreach (var entity in entities)
        {
            var textProperties = entity.GetType()
                .GetProperties()
                .Where(property => property.PropertyType == typeof(string));

            Assert.All(textProperties, property =>
                Assert.NotNull(property.GetValue(entity)));
        }
    }

    [Fact]
    public void Entities_expose_expected_persistence_fields()
    {
        Assert.Equal(
            ["Id", "Username", "PasswordHash", "AgentUserId"],
            typeof(Usuario).GetProperties().Select(property => property.Name));
        Assert.Equal(
            ["Id", "Code", "AgentUserId", "ExpiresAt", "IsUsed"],
            typeof(OauthCode).GetProperties().Select(property => property.Name));
        Assert.Equal(
            ["Id", "AccessToken", "RefreshToken", "AgentUserId", "AccessExpiresAt"],
            typeof(OauthToken).GetProperties().Select(property => property.Name));
        Assert.Equal(
            ["Id", "QueryTexto", "Fecha", "Exitoso"],
            typeof(HistorialReproduccion).GetProperties().Select(property => property.Name));
    }
}
