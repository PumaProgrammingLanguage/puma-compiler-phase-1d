namespace Puma
{
    internal static class Convertion
    {
        internal enum Type
        {
            UINT8,
            UINT16,
            UINT32,
            UINT64,
            INT8,
            INT16,
            INT32,
            INT64,
            FLT32,
            FLT64,
            FIX32,
            FIX64,
        }

        private static readonly char[,] _conversionTable =
            {
            /*From  To: U8   U16  U32  U64  I8   I16  I32  I64  F32  F64  FX32 FX64 */
            /*U8    */{ 'I', 'I', 'I', 'I', 'E', 'I', 'I', 'I', 'I', 'I', 'E', 'E' },
            /*U16   */{ 'E', 'I', 'I', 'I', 'E', 'E', 'I', 'I', 'I', 'I', 'E', 'E' },
            /*U32   */{ 'E', 'E', 'I', 'I', 'E', 'E', 'E', 'I', 'E', 'I', 'E', 'E' },
            /*U64   */{ 'E', 'E', 'E', 'I', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E' },
            /*I8    */{ 'E', 'E', 'E', 'E', 'I', 'I', 'I', 'I', 'I', 'I', 'E', 'E' },
            /*I16   */{ 'E', 'E', 'E', 'E', 'E', 'I', 'I', 'I', 'I', 'I', 'E', 'E' },
            /*I32   */{ 'E', 'E', 'E', 'E', 'E', 'E', 'I', 'I', 'E', 'I', 'E', 'E' },
            /*I64   */{ 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'I', 'E', 'E', 'E', 'E' },
            /*F32   */{ 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'I', 'I', 'E', 'E' },
            /*F64   */{ 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'I', 'E', 'E' },
            /*FX32  */{ 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'I', 'I' },
            /*FX64  */{ 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'E', 'I' }};

        internal static char GetConversionType(Type fromType, Type toType)
        {
            return _conversionTable[(int)fromType, (int)toType];
        }

        internal static bool IsImplicit(Type fromType, Type toType)
        {
            return GetConversionType(fromType, toType) == 'I';
        }

        internal static bool IsUnsignedInteger(Type type) =>
            type is Type.UINT8 or Type.UINT16 or Type.UINT32 or Type.UINT64;

        internal static bool IsSignedInteger(Type type) =>
            type is Type.INT8 or Type.INT16 or Type.INT32 or Type.INT64;

        internal static bool IsInteger(Type type) =>
            IsUnsignedInteger(type) || IsSignedInteger(type);

        internal static bool IsFloat(Type type) =>
            type is Type.FLT32 or Type.FLT64;

        internal static bool IsFix(Type type) =>
            type is Type.FIX32 or Type.FIX64;

        internal static int IntegerWidth(Type type) => type switch
        {
            Type.UINT8 or Type.INT8 => 8,
            Type.UINT16 or Type.INT16 => 16,
            Type.UINT32 or Type.INT32 => 32,
            Type.UINT64 or Type.INT64 => 64,
            _ => 0
        };

        internal static int FloatWidth(Type type) => type switch
        {
            Type.FLT32 => 32,
            Type.FLT64 => 64,
            _ => 0
        };

        internal static int FixWidth(Type type) => type switch
        {
            Type.FIX32 => 32,
            Type.FIX64 => 64,
            _ => 0
        };
    }
}
