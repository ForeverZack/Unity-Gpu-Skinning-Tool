#pragma warning disable 618
#pragma warning disable 612
#pragma warning disable 414
#pragma warning disable 168

namespace MessagePack.Resolvers
{
    using System;
    using MessagePack;

    public class GeneratedResolver : global::MessagePack.IFormatterResolver
    {
        public static readonly global::MessagePack.IFormatterResolver Instance = new GeneratedResolver();

        GeneratedResolver()
        {

        }

        public global::MessagePack.Formatters.IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.formatter;
        }

        static class FormatterCache<T>
        {
            public static readonly global::MessagePack.Formatters.IMessagePackFormatter<T> formatter;

            static FormatterCache()
            {
                var f = GeneratedResolverGetFormatterHelper.GetFormatter(typeof(T));
                if (f != null)
                {
                    formatter = (global::MessagePack.Formatters.IMessagePackFormatter<T>)f;
                }
            }
        }
    }

    internal static class GeneratedResolverGetFormatterHelper
    {
        static readonly global::System.Collections.Generic.Dictionary<Type, int> lookup;

        static GeneratedResolverGetFormatterHelper()
        {
            lookup = new global::System.Collections.Generic.Dictionary<Type, int>(3)
            {
                {typeof(global::GpuSkinningAnimClip[]), 0 },
                {typeof(global::GpuSkinningAnimClip), 1 },
                {typeof(global::GpuSkinningAnimData), 2 },
            };
        }

        internal static object GetFormatter(Type t)
        {
            int key;
            if (!lookup.TryGetValue(t, out key)) return null;

            switch (key)
            {
                case 0: return new global::MessagePack.Formatters.ArrayFormatter<global::GpuSkinningAnimClip>();
                case 1: return new MessagePack.Formatters.GpuSkinningAnimClipFormatter();
                case 2: return new MessagePack.Formatters.GpuSkinningAnimDataFormatter();
                default: return null;
            }
        }
    }
}

#pragma warning disable 168
#pragma warning restore 414
#pragma warning restore 618
#pragma warning restore 612



#pragma warning disable 618
#pragma warning disable 612
#pragma warning disable 414
#pragma warning disable 168

namespace MessagePack.Formatters
{
    using System;
    using MessagePack;


    public sealed class GpuSkinningAnimClipFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::GpuSkinningAnimClip>
    {

        public int Serialize(ref byte[] bytes, int offset, global::GpuSkinningAnimClip value, global::MessagePack.IFormatterResolver formatterResolver)
        {
            if (value == null)
            {
                return global::MessagePack.MessagePackBinary.WriteNil(ref bytes, offset);
            }
            
            var startOffset = offset;
            offset += global::MessagePack.MessagePackBinary.WriteFixedArrayHeaderUnsafe(ref bytes, offset, 3);
            offset += formatterResolver.GetFormatterWithVerify<string>().Serialize(ref bytes, offset, value.name, formatterResolver);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.startFrame);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.endFrame);
            return offset - startOffset;
        }

        public global::GpuSkinningAnimClip Deserialize(byte[] bytes, int offset, global::MessagePack.IFormatterResolver formatterResolver, out int readSize)
        {
            if (global::MessagePack.MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }

            var startOffset = offset;
            var length = global::MessagePack.MessagePackBinary.ReadArrayHeader(bytes, offset, out readSize);
            offset += readSize;

            var __name__ = default(string);
            var __startFrame__ = default(int);
            var __endFrame__ = default(int);

            for (int i = 0; i < length; i++)
            {
                var key = i;

                switch (key)
                {
                    case 0:
                        __name__ = formatterResolver.GetFormatterWithVerify<string>().Deserialize(bytes, offset, formatterResolver, out readSize);
                        break;
                    case 1:
                        __startFrame__ = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
                        break;
                    case 2:
                        __endFrame__ = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
                        break;
                    default:
                        readSize = global::MessagePack.MessagePackBinary.ReadNextBlock(bytes, offset);
                        break;
                }
                offset += readSize;
            }

            readSize = offset - startOffset;

            var ____result = new global::GpuSkinningAnimClip(__name__, __startFrame__, __endFrame__);
            ____result.name = __name__;
            ____result.startFrame = __startFrame__;
            ____result.endFrame = __endFrame__;
            return ____result;
        }
    }


    public sealed class GpuSkinningAnimDataFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::GpuSkinningAnimData>
    {

        public int Serialize(ref byte[] bytes, int offset, global::GpuSkinningAnimData value, global::MessagePack.IFormatterResolver formatterResolver)
        {
            if (value == null)
            {
                return global::MessagePack.MessagePackBinary.WriteNil(ref bytes, offset);
            }
            
            var startOffset = offset;
            offset += global::MessagePack.MessagePackBinary.WriteFixedArrayHeaderUnsafe(ref bytes, offset, 6);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.texWidth);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.texHeight);
            offset += formatterResolver.GetFormatterWithVerify<byte[]>().Serialize(ref bytes, offset, value.texBytes, formatterResolver);
            offset += formatterResolver.GetFormatterWithVerify<global::GpuSkinningAnimClip[]>().Serialize(ref bytes, offset, value.clips, formatterResolver);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.totalFrame);
            offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.totalBoneNum);
            return offset - startOffset;
        }

        public global::GpuSkinningAnimData Deserialize(byte[] bytes, int offset, global::MessagePack.IFormatterResolver formatterResolver, out int readSize)
        {
            if (global::MessagePack.MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }

            var startOffset = offset;
            var length = global::MessagePack.MessagePackBinary.ReadArrayHeader(bytes, offset, out readSize);
            offset += readSize;

            var __texWidth__ = default(int);
            var __texHeight__ = default(int);
            var __texBytes__ = default(byte[]);
            var __clips__ = default(global::GpuSkinningAnimClip[]);
            var __totalFrame__ = default(int);
            var __totalBoneNum__ = default(int);

            for (int i = 0; i < length; i++)
            {
                var key = i;

                switch (key)
                {
                    case 0:
                        __texWidth__ = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
                        break;
                    case 1:
                        __texHeight__ = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
                        break;
                    case 2:
                        __texBytes__ = formatterResolver.GetFormatterWithVerify<byte[]>().Deserialize(bytes, offset, formatterResolver, out readSize);
                        break;
                    case 3:
                        __clips__ = formatterResolver.GetFormatterWithVerify<global::GpuSkinningAnimClip[]>().Deserialize(bytes, offset, formatterResolver, out readSize);
                        break;
                    case 4:
                        __totalFrame__ = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
                        break;
                    case 5:
                        __totalBoneNum__ = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
                        break;
                    default:
                        readSize = global::MessagePack.MessagePackBinary.ReadNextBlock(bytes, offset);
                        break;
                }
                offset += readSize;
            }

            readSize = offset - startOffset;

            var ____result = new global::GpuSkinningAnimData();
            ____result.texWidth = __texWidth__;
            ____result.texHeight = __texHeight__;
            ____result.texBytes = __texBytes__;
            ____result.clips = __clips__;
            ____result.totalFrame = __totalFrame__;
            ____result.totalBoneNum = __totalBoneNum__;
            return ____result;
        }
    }

}

#pragma warning disable 168
#pragma warning restore 414
#pragma warning restore 618
#pragma warning restore 612
