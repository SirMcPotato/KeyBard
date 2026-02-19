using KeyBard.Midi;
using Xunit;

namespace KeyBard.Tests;

public class GeneralMidiTests
{
    [Theory]
    [InlineData(0, "Acoustic Grand Piano")]
    [InlineData(1, "Bright Acoustic Piano")]
    [InlineData(127, "Gunshot")]
    public void GetProgramName_KnownIndex_ShouldReturnCorrectName(int programNumber, string expectedName)
    {
        // Act
        var result = GeneralMidi.GetProgramName(programNumber);

        // Assert
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData(-1, "Program -1")]
    [InlineData(128, "Program 128")]
    [InlineData(1000, "Program 1000")]
    public void GetProgramName_UnknownIndex_ShouldReturnGenericName(int programNumber, string expectedName)
    {
        // Act
        var result = GeneralMidi.GetProgramName(programNumber);

        // Assert
        Assert.Equal(expectedName, result);
    }
}
