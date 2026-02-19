using Xunit;

namespace KeyBard.Tests;

public class BindingProviderTests
{
    [Fact]
    public void Set_And_Get_Binding_Works()
    {
        var provider = new KeyBard.BindingProvider();
        provider.SetBinding(60, 0x1E);
        Assert.Equal((ushort)0x1E, provider.GetBinding(60));
        Assert.Null(provider.GetBinding(61));
    }

    [Fact]
    public void ClearBinding_Removes_Only_Specified_Note()
    {
        var provider = new KeyBard.BindingProvider();
        provider.SetBinding(60, 0x1E);
        provider.SetBinding(62, 0x1F);
        provider.ClearBinding(60);
        Assert.Null(provider.GetBinding(60));
        Assert.Equal((ushort)0x1F, provider.GetBinding(62));
    }

    [Fact]
    public void ClearAll_Removes_All()
    {
        var provider = new KeyBard.BindingProvider();
        provider.SetBinding(60, 0x1E);
        provider.SetBinding(62, 0x1F);
        provider.ClearAll();
        Assert.Null(provider.GetBinding(60));
        Assert.Null(provider.GetBinding(62));
    }

    [Fact]
    public void Export_And_Import_Roundtrip()
    {
        var provider1 = new KeyBard.BindingProvider();
        provider1.SetBinding(60, 0x1E);
        provider1.SetBinding(62, 0x1F);

        var exported = provider1.Export();

        var provider2 = new KeyBard.BindingProvider();
        provider2.Import(exported);

        Assert.Equal((ushort)0x1E, provider2.GetBinding(60));
        Assert.Equal((ushort)0x1F, provider2.GetBinding(62));
    }

    [Fact]
    public void KeyBindingsStore_Roundtrip_With_BindingProvider()
    {
        var provider = new KeyBard.BindingProvider();
        provider.SetBinding(60, 0x1E);
        provider.SetBinding(62, 0x1F);

        var file = "provider_roundtrip.json";
        try
        {
            KeyBard.KeyBindingsStore.Save(file, provider.Export());
            var loaded = KeyBard.KeyBindingsStore.Load(file);
            var provider2 = new KeyBard.BindingProvider();
            provider2.Import(loaded);

            Assert.Equal((ushort)0x1E, provider2.GetBinding(60));
            Assert.Equal((ushort)0x1F, provider2.GetBinding(62));
        }
        finally
        {
            if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
        }
    }
}
