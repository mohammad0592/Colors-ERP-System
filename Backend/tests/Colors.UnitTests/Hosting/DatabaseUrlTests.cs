using Colors.Api.Extensions;
using Npgsql;

namespace Colors.UnitTests.Hosting;

/// <summary>
/// Reading the database address the way a cloud host writes it.
///
/// This only matters for the trial — the factory server is given its connection string
/// directly. But it matters completely there: get it wrong and the application cannot
/// reach the database at all, and the only clue is an authentication failure against a
/// password that looks right in every screen you can see it in.
/// </summary>
public class DatabaseUrlTests
{
    private static NpgsqlConnectionStringBuilder Parse(string url) =>
        new(HostingExtensions.TranslateDatabaseUrl(url));

    [Fact]
    public void Reads_every_part_of_the_address()
    {
        var settings = Parse("postgresql://factory:secret@db.example.com:6543/colors_erp");

        Assert.Equal("db.example.com", settings.Host);
        Assert.Equal(6543, settings.Port);
        Assert.Equal("colors_erp", settings.Database);
        Assert.Equal("factory", settings.Username);
        Assert.Equal("secret", settings.Password);
    }

    [Theory]
    [InlineData("postgresql://u:p@host/db")]
    [InlineData("postgres://u:p@host/db")]
    public void Accepts_both_spellings_hosts_use(string url)
    {
        // Railway and Render write "postgresql", Heroku and some others write "postgres".
        // They mean the same thing.
        var settings = Parse(url);

        Assert.Equal("host", settings.Host);
        Assert.Equal("db", settings.Database);
    }

    [Fact]
    public void Falls_back_to_the_usual_port_when_none_is_given()
    {
        // Uri reports -1 for a missing port, which would be a nonsense to connect to.
        var settings = Parse("postgresql://u:p@host/db");

        Assert.Equal(5432, settings.Port);
    }

    [Fact]
    public void Turns_an_escaped_password_back_into_the_real_one()
    {
        // This is the whole reason the function exists. A generated password with an @
        // in it arrives as %40, and handing %40 to the database means failing to sign in
        // with a password that is, as far as anybody can see, correct.
        var settings = Parse("postgresql://user:p%40ss%2Fword%231@host:5432/db");

        Assert.Equal("p@ss/word#1", settings.Password);
    }

    [Fact]
    public void Turns_an_escaped_user_and_database_back_as_well()
    {
        var settings = Parse("postgresql://od%40d:p@host:5432/my%20db");

        Assert.Equal("od@d", settings.Username);
        Assert.Equal("my db", settings.Database);
    }

    [Fact]
    public void Copes_with_no_password_at_all()
    {
        // A database on the same private network sometimes has none.
        var settings = Parse("postgresql://postgres@host:5432/db");

        Assert.Equal("postgres", settings.Username);

        // Npgsql leaves an empty password out of the connection string altogether rather
        // than carrying an empty one, so this reads back as nothing. Either way what
        // matters is that no password is sent and the user is still right.
        Assert.True(string.IsNullOrEmpty(settings.Password));
    }

    [Fact]
    public void Asks_for_encryption_without_demanding_a_public_certificate()
    {
        // Managed databases nearly always present a certificate signed by their own
        // authority. Demanding a publicly trusted one would refuse to connect to most
        // of them; refusing encryption altogether would send the password in the clear.
        var settings = Parse("postgresql://u:p@host:5432/db");

        Assert.Equal(SslMode.Prefer, settings.SslMode);
    }

    [Fact]
    public void Produces_something_Npgsql_will_accept()
    {
        // The real proof: the result is a connection string, not just text that looks
        // like one. NpgsqlConnection reads it or throws.
        var text = HostingExtensions.TranslateDatabaseUrl(
            "postgresql://factory:s3cr%40t@db.internal:5432/colors_erp");

        using var connection = new NpgsqlConnection(text);

        Assert.Equal("colors_erp", connection.Database);
    }
}
