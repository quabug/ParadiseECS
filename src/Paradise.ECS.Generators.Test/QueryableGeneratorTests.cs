namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Tests for QueryableGenerator duplicate ID detection (PECS014).
/// Note: The Roslyn test infrastructure cannot properly test generators that depend on
/// other generators' output (With&lt;T&gt; requires T to implement IComponent).
/// The duplicate ID detection was verified to work via manual compilation test:
/// Adding duplicate IDs to TestComponents.cs produced the expected PECS014 error.
/// </summary>
public class QueryableGeneratorDuplicateManualIdTests
{
    /// <summary>
    /// Verifies that the PECS014 diagnostic descriptor is properly defined.
    /// The actual duplicate detection is tested via compile-time verification.
    /// </summary>
    [Test]
    public async Task DuplicateQueryableIdDiagnostic_HasCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.DuplicateQueryableId;

        await Assert.That(descriptor.Id).IsEqualTo("PECS014");
        await Assert.That(descriptor.Title.ToString(System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo("Duplicate queryable ID");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
    }

    /// <summary>
    /// Verifies the diagnostic message format contains expected placeholders.
    /// </summary>
    [Test]
    public async Task DuplicateQueryableIdDiagnostic_MessageFormat_ContainsPlaceholders()
    {
        var descriptor = DiagnosticDescriptors.DuplicateQueryableId;
        var format = descriptor.MessageFormat.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Message format should include ID and type names placeholders
        await Assert.That(format).Contains("{0}"); // ID placeholder
        await Assert.That(format).Contains("{1}"); // Type names placeholder
    }
}
