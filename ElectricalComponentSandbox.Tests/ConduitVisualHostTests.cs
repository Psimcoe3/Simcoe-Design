using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace ElectricalComponentSandbox.Tests
{
    [TestClass]
    public class ConduitVisualHostTests
    {
        [TestMethod]
        public void Constructor_DoesNotThrow()
        {
            ConduitVisualHost host = null;
            Exception exception = null;
            try
            {
                host = new ConduitVisualHost();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNull(exception, $"Constructor threw exception: {exception}");
            Assert.IsNotNull(host, "ConduitVisualHost instance is null");
        }
    }
}
