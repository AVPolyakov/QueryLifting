using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace QueryLifting
{
    /// <summary>
    /// http://www.codeproject.com/KB/cs/sdilreader.aspx
    /// </summary>
    public static class UsageResolver
    {
        public static IEnumerable<Usage> ResolveUsages(this IEnumerable<Type> types)
        {
            return types.SelectMany(type =>
                type.GetMethods(AllBindingFlags).SelectMany(info => ResolveUsages(info, info.GetGenericArguments))
                    .Concat(type.GetConstructors(AllBindingFlags).SelectMany(info => ResolveUsages(info, () => new Type[] { }))));
        }

        private static IEnumerable<Usage> ResolveUsages(MethodBase methodInfo, Func<Type[]> genericMethodArgumentsFunc)
        {
            var methodBody = methodInfo.GetMethodBody();
            if (methodBody == null) yield break;
            var ilAsByteArray = methodBody.GetILAsByteArray();
            var position = 0;
            while (position < ilAsByteArray.Length)
            {
                OpCode opCode;
                ushort value = ilAsByteArray[position++];
                if (value == 0xfe)
                {
                    value = ilAsByteArray[position++];
                    opCode = multiByteOpCodes[value];
                }
                else
                    opCode = singleByteOpCodes[value];
                switch (opCode.OperandType)
                {
                    case OperandType.InlineBrTarget:
                        ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineField:
                        ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineMethod:
                        var metadataToken = ReadInt32(ilAsByteArray, ref position);
                        if (methodInfo.DeclaringType != null)
                            yield return new Usage(
                                methodInfo,
                                methodInfo.Module.ResolveMember(
                                    metadataToken,
                                    methodInfo.DeclaringType.GetGenericArguments(),
                                    genericMethodArgumentsFunc()));
                        break;
                    case OperandType.InlineSig:
                        ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineTok:
                        ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineType:
                        ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineI:
                        ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineI8:
                        ReadInt64(ref position);
                        break;
                    case OperandType.InlineNone:
                        break;
                    case OperandType.InlineR:
                        ReadDouble(ref position);
                        break;
                    case OperandType.InlineString:
                        ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineSwitch:
                        var count = ReadInt32(ilAsByteArray, ref position);
                        for (var i = 0; i < count; i++) ReadInt32(ilAsByteArray, ref position);
                        break;
                    case OperandType.InlineVar:
                        ReadUInt16(ref position);
                        break;
                    case OperandType.ShortInlineBrTarget:
                        ReadSByte(ref position);
                        break;
                    case OperandType.ShortInlineI:
                        ReadSByte(ref position);
                        break;
                    case OperandType.ShortInlineR:
                        ReadSingle(ref position);
                        break;
                    case OperandType.ShortInlineVar:
                        ReadByte(ref position);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        static UsageResolver()
        {
            singleByteOpCodes = new OpCode[0x100];
            multiByteOpCodes = new OpCode[0x100];
            foreach (var fieldInfo in typeof(OpCodes).GetFields())
            {
                if (fieldInfo.FieldType == typeof(OpCode))
                {
                    var opCode = (OpCode)fieldInfo.GetValue(null);
                    var value = unchecked((ushort)opCode.Value);
                    if (value < 0x100)
                    {
                        singleByteOpCodes[value] = opCode;
                    }
                    else
                    {
                        if ((value & 0xff00) != 0xfe00)
                        {
                            throw new Exception("Invalid OpCode.");
                        }
                        multiByteOpCodes[value & 0xff] = opCode;
                    }
                }
            }
        }

        private static readonly OpCode[] multiByteOpCodes;
        private static readonly OpCode[] singleByteOpCodes;

        private static void ReadUInt16(ref int position)
        {
            position += 2;
        }

        private static int ReadInt32(byte[] bytes, ref int position)
        {
            return bytes[position++] | bytes[position++] << 8 | bytes[position++] << 0x10 | bytes[position++] << 0x18;
        }

        private static void ReadInt64(ref int position)
        {
            position += 8;
        }

        private static void ReadDouble(ref int position)
        {
            position += 8;
        }

        private static void ReadSByte(ref int position)
        {
            position++;
        }

        private static void ReadByte(ref int position)
        {
            position++;
        }

        private static void ReadSingle(ref int position)
        {
            position += 4;
        }

        public const BindingFlags AllBindingFlags = BindingFlags.Default |
                                                    BindingFlags.IgnoreCase |
                                                    BindingFlags.DeclaredOnly |
                                                    BindingFlags.Instance |
                                                    BindingFlags.Static |
                                                    BindingFlags.Public |
                                                    BindingFlags.NonPublic |
                                                    BindingFlags.FlattenHierarchy |
                                                    BindingFlags.InvokeMethod |
                                                    BindingFlags.CreateInstance |
                                                    BindingFlags.GetField |
                                                    BindingFlags.SetField |
                                                    BindingFlags.GetProperty |
                                                    BindingFlags.SetProperty |
                                                    BindingFlags.PutDispProperty |
                                                    BindingFlags.PutRefDispProperty |
                                                    BindingFlags.ExactBinding |
                                                    BindingFlags.SuppressChangeType |
                                                    BindingFlags.OptionalParamBinding |
                                                    BindingFlags.IgnoreReturn;
    }
}