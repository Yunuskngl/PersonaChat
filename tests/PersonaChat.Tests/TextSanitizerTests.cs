using PersonaChat.Core.Services;

namespace PersonaChat.Tests;

public class TextSanitizerTests
{
    [Fact]
    public void CleanInput_TrimsAndStripsControlCharacters()
    {
        Assert.Equal("a\nb", TextSanitizer.CleanInput("  a\u0007\nb\u0000 ", 10));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \u0001 ")]
    public void CleanInput_RejectsEmpty(string? text)
    {
        Assert.Throws<InvalidInputException>(() => TextSanitizer.CleanInput(text, 10));
    }

    [Fact]
    public void CleanInput_RejectsTooLong()
    {
        Assert.Throws<InvalidInputException>(() => TextSanitizer.CleanInput("12345", 4));
    }

    [Fact]
    public void CleanNote_RemovesTagCharactersAndNewlines()
    {
        Assert.Equal("/memoryhi", TextSanitizer.CleanNote("</memory>\nhi", 50));
    }

    [Fact]
    public void CleanNote_CutsToMaxLength()
    {
        Assert.Equal("abc", TextSanitizer.CleanNote("abcdef", 3));
    }

    [Fact]
    public void CleanNote_ReturnsNullWhenNothingLeft()
    {
        Assert.Null(TextSanitizer.CleanNote("<>\u0001", 10));
    }
}
