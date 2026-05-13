using System.Text.Json;
using Mabipacade.Core.Json;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Tests.Output;

public class ElemJsonTests
{
    private static string Render(MessageElem e)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            ElemJson.Write(w, e);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact] public void Byte_AsObject() =>
        Assert.Equal("{\"t\":\"Byte\",\"v\":42}", Render(MessageElem.Byte(42)));

    [Fact] public void Short_AsObject() =>
        Assert.Equal("{\"t\":\"Short\",\"v\":59000}", Render(MessageElem.Short(59000)));

    [Fact] public void Int_AsObject() =>
        Assert.Equal("{\"t\":\"Int\",\"v\":1234567}", Render(MessageElem.Int(1234567u)));

    [Fact] public void Long_AsString_ForUint64Safety() =>
        Assert.Equal("{\"t\":\"Long\",\"v\":\"18446744073709551615\"}", Render(MessageElem.Long(ulong.MaxValue)));

    [Fact] public void Float_AsNumber() =>
        Assert.Equal("{\"t\":\"Float\",\"v\":1.5}", Render(MessageElem.Float(1.5f)));

    [Fact] public void String_AsObject() =>
        Assert.Equal("{\"t\":\"String\",\"v\":\"hello\"}", Render(MessageElem.String("hello")));

    [Fact] public void Bin_AsBase64() =>
        Assert.Equal("{\"t\":\"Bin\",\"v\":\"AQID\"}", Render(MessageElem.Bin(new byte[] { 1, 2, 3 })));
}
