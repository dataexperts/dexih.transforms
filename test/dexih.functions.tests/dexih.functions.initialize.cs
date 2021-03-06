﻿using System;
using System.Threading;
using Xunit;

namespace dexih.functions.tests
{
    public class FunctionInitializers
    {
        [Fact]
        public void TransformFunction_RunFunction_UsingDelegate()
        {
            //create a custom function
            TransformFunction function1 = new TransformFunction(new Func<int, int, int>((i, j) => i + j));
            Assert.True((int) function1.RunFunction(new FunctionVariables(), new object[] { 6, 2 }, CancellationToken.None).returnValue == 8);
        }

        [Fact]
        public void TransformFunction_RunFunction_UsingMethod()
        {
            //create a custom function
            var globalVariable = new GlobalSettings(null);
            TransformFunction function1 = new TransformFunction(
                this, 
                nameof(TestMethod));
            Assert.True((int) function1.RunFunction(new FunctionVariables(),new object[] { 6, 2 }, CancellationToken.None).returnValue == 8);
        }

        [Fact]
        public void TransformFunction_RunFunction_UsingReflection()
        {
            //create a custom function
            TransformFunction function1 = new TransformFunction(this, this.GetType().GetMethod(nameof(TestMethod)), typeof(string), null, new GlobalSettings(null));
            Assert.True((int) function1.RunFunction(new FunctionVariables(),new object[] { 6, 2 }, CancellationToken.None).returnValue == 8);
        }

        public int TestMethod(int a, int b) => a + b;
    }
}
