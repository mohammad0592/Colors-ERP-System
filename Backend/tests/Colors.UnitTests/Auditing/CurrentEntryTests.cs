using Colors.Api.Auditing;
using Colors.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Colors.UnitTests.Auditing;

/// <summary>
/// Reading how a code arrived off the request header (specification section 12).
///
/// The header comes from a browser, so every one of these is a value somebody could
/// send. None of them may throw, and anything that is not one of the three answers has
/// to read as <see cref="EntryMethod.Unknown"/> — the honest answer — rather than
/// guessing at the nearest match.
/// </summary>
public class CurrentEntryTests
{
    private static EntryMethod MethodFor(string? header)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers[CurrentEntry.HeaderName] = header;
        }

        return new CurrentEntry(new HttpContextAccessor { HttpContext = context }).Method;
    }

    [Theory]
    [InlineData("Scanned", EntryMethod.Scanned)]
    [InlineData("Typed", EntryMethod.Typed)]
    [InlineData("Picked", EntryMethod.Picked)]
    public void The_three_answers_are_read(string header, EntryMethod expected)
    {
        Assert.Equal(expected, MethodFor(header));
    }

    [Theory]
    [InlineData("scanned")]
    [InlineData("SCANNED")]
    [InlineData("sCaNnEd")]
    public void Case_does_not_matter(string header)
    {
        // The screens send one spelling, but a header is not a place to be strict about
        // capitals when the answer is unambiguous either way.
        Assert.Equal(EntryMethod.Scanned, MethodFor(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("something else")]
    [InlineData("Unknown")]
    public void Anything_else_is_unknown(string? header)
    {
        Assert.Equal(EntryMethod.Unknown, MethodFor(header));
    }

    [Fact]
    public void A_number_off_the_end_of_the_enum_is_unknown()
    {
        // Enum.TryParse happily accepts "7" and hands back an EntryMethod of 7, which is
        // not one of the three. Without the IsDefined check the audit log would carry a
        // value nothing can read back.
        Assert.Equal(EntryMethod.Unknown, MethodFor("7"));
    }

    [Fact]
    public void No_request_at_all_is_unknown()
    {
        // The seeders and the migrations save through the same context with no web
        // request anywhere. That must not throw.
        var entry = new CurrentEntry(new HttpContextAccessor { HttpContext = null });

        Assert.Equal(EntryMethod.Unknown, entry.Method);
    }
}
