using System.Globalization;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class GlobalizationTests
{
    /// <summary>
    /// WPF resolves <c>XmlLanguage("en-US")</c> for every binding. With
    /// <c>InvariantGlobalization=true</c> there is no specific culture and the binding engine throws
    /// "Cannot find non-neutral culture related to 'en-us'", which killed the flyout on Windows.
    /// This test fails if the property is ever turned back on.
    /// </summary>
    [Fact]
    public void Specific_cultures_are_available_for_wpf_bindings()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("en-US", culture.Name);
        Assert.False(culture.IsNeutralCulture);
    }
}
