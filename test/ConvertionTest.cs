using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puma;

namespace test
{
    [TestClass]
    public class ConvertionTest
    {
        private static void EnsureImplicit(Convertion.Type fromType, Convertion.Type toType)
        {
            if (!Convertion.IsImplicit(fromType, toType))
            {
                throw new InvalidOperationException($"Implicit conversion is not valid: {fromType} -> {toType}");
            }
        }

        [TestMethod]
        public void ConvertionTable_AllImplicitEntries_AreImplicit()
        {
            var max = Enum.GetValues<Convertion.Type>().Length;
            for (var from = 0; from < max; from++)
            {
                for (var to = 0; to < max; to++)
                {
                    var fromType = (Convertion.Type)from;
                    var toType = (Convertion.Type)to;
                    if (Convertion.GetConversionType(fromType, toType) == 'I')
                    {
                        Assert.IsTrue(
                            Convertion.IsImplicit(fromType, toType),
                            $"Expected implicit conversion for {fromType} -> {toType}.");
                    }
                }
            }
        }

        [TestMethod]
        public void Convertion_InvalidImplicitConversion_ReturnsErrorMessage()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                EnsureImplicit(Convertion.Type.FLT64, Convertion.Type.INT32));

            StringAssert.Contains(ex.Message, "Implicit conversion is not valid");
            StringAssert.Contains(ex.Message, "FLT64 -> INT32");
        }

        [TestMethod]
        public void ConvertionTable_AllExplicitEntries_AreExplicit()
        {
            var max = Enum.GetValues<Convertion.Type>().Length;
            for (var from = 0; from < max; from++)
            {
                for (var to = 0; to < max; to++)
                {
                    var fromType = (Convertion.Type)from;
                    var toType = (Convertion.Type)to;
                    if (Convertion.GetConversionType(fromType, toType) == 'E')
                    {
                        Assert.IsFalse(
                            Convertion.IsImplicit(fromType, toType),
                            $"Expected explicit conversion for {fromType} -> {toType}.");
                    }
                }
            }
        }
    }
}
