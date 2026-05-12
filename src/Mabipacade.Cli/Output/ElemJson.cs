using System.Text.Json;
using Mabipacade.Core.Model;

namespace Mabipacade.Cli.Output;

internal static class ElemJson
{
    public static void Write(Utf8JsonWriter w, MessageElem e)
    {
        w.WriteStartObject();
        w.WriteString("t", e.Type.ToString());
        switch (e.Type)
        {
            case MessageElemType.Byte:   w.WriteNumber("v", e.AsByte()); break;
            case MessageElemType.Short:  w.WriteNumber("v", e.AsUInt16()); break;
            case MessageElemType.Int:    w.WriteNumber("v", e.AsUInt32()); break;
            case MessageElemType.Long:   w.WriteString("v", e.AsUInt64().ToString()); break;
            case MessageElemType.Float:  w.WriteNumber("v", e.AsFloat()); break;
            case MessageElemType.String: w.WriteString("v", e.AsString()); break;
            case MessageElemType.Bin:    w.WriteBase64String("v", e.AsBytes()); break;
        }
        w.WriteEndObject();
    }
}
