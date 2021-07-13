// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class AssemblyIdentityMustMatchTests : SdkTest
    {
        private static readonly byte[] _publicKey = new byte[]
        { 
            0, 36, 0, 0, 4, 128, 0, 0, 148, 0, 0, 0, 6, 2, 0, 0, 0, 36, 0, 0,
            82, 83, 65, 49, 0, 4, 0, 0, 1, 0, 1, 0, 59, 95, 150, 159, 243, 67,
            213, 101, 13, 42, 127, 1, 28, 70, 32, 249, 95, 32, 222, 178, 241,
            112, 43, 130, 179, 253, 136, 12, 214, 69, 99, 48, 108, 1, 225, 85,
            43, 140, 249, 91, 96, 28, 32, 96, 222, 101, 30, 186, 118, 74, 97, 47,
            90, 203, 33, 109, 13, 224, 26, 68, 113, 252, 132, 189, 45, 113, 37, 194,
            246, 28, 250, 11, 142, 65, 158, 36, 69, 33, 123, 215, 206, 43, 179, 174,
            44, 66, 108, 152, 199, 61, 182, 176, 126, 115, 72, 67, 1, 234, 122, 214,
            208, 240, 99, 182, 103, 113, 54, 95, 253, 54, 249, 70, 150, 123, 230, 135,
            122, 189, 56, 195, 25, 62, 141, 151, 88, 234, 231, 156
        };

        public AssemblyIdentityMustMatchTests(ITestOutputHelper log) : base(log) { }

        [Fact]
        public static void AssemblyNamesDoNotMatch()
        {
            IAssemblySymbol left = CSharpCompilation.Create("AssemblyA").Assembly;
            IAssemblySymbol right = CSharpCompilation.Create("AssemblyB").Assembly;
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Single(differences);

            CompatDifference expected = new CompatDifference(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "AssemblyB, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First(), CompatDifferenceComparer.Default);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void AssemblyCultureMustBeCompatible()
        {
            // setting different assembly culture for net6.0
            string assemblyCulture = "#if NET6_0 \n[assembly: System.Reflection.AssemblyCultureAttribute(\"de\")]\n #endif";

            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = "net6.0;netstandard2.0",
            };

            testProject.SourceFiles.Add("AssemblyInfo.cs", assemblyCulture);
            testProject.AdditionalProperties["GenerateAssemblyInfo"] = "false";

            // building the project.
            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject);
            BuildCommand buildCommand = new(testAsset);
            buildCommand.Execute().Should().Pass();

            string leftDllPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "Project.dll");
            string rightDllPath = Path.Combine(buildCommand.GetOutputDirectory("net6.0").FullName, "Project.dll");
            IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbols, rightSymbols);
            Assert.Single(differences);

            CompatDifference expected = new CompatDifference(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "Project, Version=0.0.0.0, Culture=de, PublicKeyToken=null");
            Assert.Equal(expected, differences.First(), CompatDifferenceComparer.Default);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void AssemblyVersionMustBeCompatible()
        {
            // setting different assembly culture for netstanard2.0
            string assemblyCulture = "#if !NET6_0 \n[assembly: System.Reflection.AssemblyVersionAttribute(\"2.0.0.0\")]\n #endif";

            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = "net6.0;netstandard2.0",
            };

            testProject.SourceFiles.Add("AssemblyInfo.cs", assemblyCulture);
            testProject.AdditionalProperties["GenerateAssemblyInfo"] = "false";

            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject);
            BuildCommand buildCommand = new(testAsset);
            buildCommand.Execute().Should().Pass();
            string leftDllPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "Project.dll");
            string rightDllPath = Path.Combine(buildCommand.GetOutputDirectory("net6.0").FullName, "Project.dll");
            IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbols, rightSymbols);

            // net6.0 assembly should have same or higher version than net6.0
            Assert.Single(differences);

            CompatDifference expected = new CompatDifference(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "Project, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First(), CompatDifferenceComparer.Default);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void AssemblyVersionMustBeStrictlyCompatible()
        {
            // setting different assembly culture for netstanard2.0
            string assemblyCulture = "#if !NET6_0 \n[assembly: System.Reflection.AssemblyVersionAttribute(\"1.0.0.0\")]\n #else\n[assembly: System.Reflection.AssemblyVersionAttribute(\"2.0.0.0\")]\n #endif";

            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = "net6.0;netstandard2.0",
            };

            testProject.SourceFiles.Add("AssemblyInfo.cs", assemblyCulture);
            testProject.AdditionalProperties["GenerateAssemblyInfo"] = "false";

            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject);
            BuildCommand buildCommand = new(testAsset);
            buildCommand.Execute().Should().Pass();
            string leftDllPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "Project.dll");
            string rightDllPath = Path.Combine(buildCommand.GetOutputDirectory("net6.0").FullName, "Project.dll");
            IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            // Compatible assembly versions
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbols, rightSymbols);
            Assert.Empty(differences);

            ApiComparer strictDiffer = new();
            strictDiffer.StrictMode = true;
            leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            // Not strictly compatible
            differences = strictDiffer.GetDifferences(leftSymbols, rightSymbols);
            Assert.Single(differences);

            CompatDifference expected = new CompatDifference(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "Project, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First(), CompatDifferenceComparer.Default);
        }

        [Fact]
        public void AssemblyKeyTokenMustBeCompatible()
        {
            string syntax = "namespace EmptyNs { }";

            IAssemblySymbol leftSymbols = SymbolFactory.GetAssemblyFromSyntax(syntax, publicKey: _publicKey);
            IAssemblySymbol rightSymbols = SymbolFactory.GetAssemblyFromSyntax(syntax, publicKey: _publicKey);

            // public key tokens must match
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbols, rightSymbols);
            Assert.Empty(differences);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void LeftAssemblyKeyTokenNull()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PublicKeyTokenValidation")
                .WithSource();

            BuildCommand buildCommand = new(testAsset);
            buildCommand.Execute("-p:DisableSigningOnNetStandard2_0=true").Should().Pass();

            string leftDllPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "PublicKeyTokenValidation.dll");
            string rightDllPath = Path.Combine(buildCommand.GetOutputDirectory("net6.0").FullName, "PublicKeyTokenValidation.dll");
            IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbols, rightSymbols);
            Assert.Empty(differences);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void RightAssemblyKeyTokenNull()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PublicKeyTokenValidation")
                .WithSource();

            BuildCommand buildCommand = new(testAsset);
            buildCommand.Execute("-p:DisableSigningOnNet6_0=true").Should().Pass();

            string leftDllPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "PublicKeyTokenValidation.dll");
            string rightDllPath = Path.Combine(buildCommand.GetOutputDirectory("net6.0").FullName, "PublicKeyTokenValidation.dll");
            IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbols, rightSymbols);
            Assert.Single(differences);

            CompatDifference expected = new CompatDifference(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "PublicKeyTokenValidation, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First(), CompatDifferenceComparer.Default);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void RetargetableFlagSet()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PublicKeyTokenValidation")
                .WithSource();

            BuildCommand buildCommand = new(testAsset);
            buildCommand.Execute("-p:DisableSigningOnNet6_0=true;GenerateAssemblyInfo=false;IsRetargetable=true")
                .Should()
                .Pass();

            string leftDllPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "PublicKeyTokenValidation.dll");
            string rightDllPath = Path.Combine(buildCommand.GetOutputDirectory("net6.0").FullName, "PublicKeyTokenValidation.dll");
            IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbols, rightSymbols);
            Assert.Empty(differences);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void LeftAssemblyKeyTokenNullStrictMode()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PublicKeyTokenValidation")
                .WithSource();

            BuildCommand buildCommand = new(testAsset);
            buildCommand.Execute("-p:DisableSigningOnNetStandard2_0=true").Should().Pass();

            string leftDllPath = Path.Combine(buildCommand.GetOutputDirectory("netstandard2.0").FullName, "PublicKeyTokenValidation.dll");
            string rightDllPath = Path.Combine(buildCommand.GetOutputDirectory("net6.0").FullName, "PublicKeyTokenValidation.dll");
            IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(leftDllPath);
            IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightDllPath);

            ApiComparer strictDiffer = new();
            strictDiffer.StrictMode = true;

            IEnumerable<CompatDifference> differences = strictDiffer.GetDifferences(leftSymbols, rightSymbols);
            Assert.Single(differences);

            CompatDifference expected = new CompatDifference(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "PublicKeyTokenValidation, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First(), CompatDifferenceComparer.Default);
        }
    }
}

