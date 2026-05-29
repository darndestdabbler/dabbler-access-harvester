using AccessHarvester.Pipeline.Sanitization;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

public class RemapRegistryTests
{
    [Fact]
    public void Same_original_always_maps_to_same_new()
    {
        var r = new RemapRegistry(startValue: 100);
        var key = new TableColumn("Person", "PersonID");

        var first  = r.Remap(key, 42);
        var second = r.Remap(key, 42);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_originals_map_to_different_news()
    {
        var r = new RemapRegistry(startValue: 100);
        var key = new TableColumn("Person", "PersonID");

        var a = r.Remap(key, 1);
        var b = r.Remap(key, 2);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Null_original_returns_null()
    {
        var r = new RemapRegistry();
        Assert.Null(r.Remap(new TableColumn("T", "C"), null));
    }

    [Fact]
    public void First_value_starts_at_start_value()
    {
        var r = new RemapRegistry(startValue: 5_000_000);
        var v = r.Remap(new TableColumn("T", "C"), "first");
        Assert.Equal(5_000_000L, v);
    }

    [Fact]
    public void Numeric_and_string_originals_with_same_canonical_form_collide()
    {
        // FK target column may receive boxed int from one row source and
        // boxed long from another (OLE DB sometimes promotes the type). Both
        // representations of the same numeric value MUST hit the same map
        // entry, else FK consistency breaks.
        var r = new RemapRegistry(startValue: 100);
        var key = new TableColumn("Person", "PersonID");

        long fromInt    = r.Remap(key, 42)!.Value;
        long fromLong   = r.Remap(key, 42L)!.Value;
        long fromShort  = r.Remap(key, (short)42)!.Value;
        long fromString = r.Remap(key, "42")!.Value;

        Assert.Equal(fromInt, fromLong);
        Assert.Equal(fromInt, fromShort);
        Assert.Equal(fromInt, fromString);
    }

    [Fact]
    public void Different_target_keys_have_independent_maps()
    {
        var r = new RemapRegistry(startValue: 100);
        var personKey = new TableColumn("Person", "PersonID");
        var visitKey  = new TableColumn("Visit",  "VisitID");

        long p1 = r.Remap(personKey, 1)!.Value;
        long v1 = r.Remap(visitKey,  1)!.Value;

        Assert.Equal(100L, p1);
        Assert.Equal(100L, v1);
    }

    [Fact]
    public void Sizes_reports_per_target_map_size()
    {
        var r = new RemapRegistry();
        var key = new TableColumn("T", "C");
        r.Remap(key, 1);
        r.Remap(key, 2);
        r.Remap(key, 1); // dup

        Assert.Equal(2, r.Sizes()[key]);
    }
}
