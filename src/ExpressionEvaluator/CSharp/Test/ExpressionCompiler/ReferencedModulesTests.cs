﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.ExpressionEvaluator;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.DiaSymReader;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Roslyn.Test.PdbUtilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class ReferencedModulesTests : ExpressionCompilerTestBase
    {
        /// <summary>
        /// MakeAssemblyReferences should drop unreferenced assemblies.
        /// </summary>
        [WorkItem(1141029)]
        [Fact]
        public void AssemblyDuplicateReferences()
        {
            var sourceA =
@"public class A
{
}";
            var sourceB =
@"public class B
{
    public A F = new A();
}";
            var sourceC =
@"class C
{
    private B F = new B();
    static void M()
    {
    }
}";
            // Assembly A, multiple versions, strong name.
            var assemblyNameA = ExpressionCompilerUtilities.GenerateUniqueName();
            var publicKeyA = ImmutableArray.CreateRange(new byte[] { 0x00, 0x24, 0x00, 0x00, 0x04, 0x80, 0x00, 0x00, 0x94, 0x00, 0x00, 0x00, 0x06, 0x02, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x52, 0x53, 0x41, 0x31, 0x00, 0x04, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0xED, 0xD3, 0x22, 0xCB, 0x6B, 0xF8, 0xD4, 0xA2, 0xFC, 0xCC, 0x87, 0x37, 0x04, 0x06, 0x04, 0xCE, 0xE7, 0xB2, 0xA6, 0xF8, 0x4A, 0xEE, 0xF3, 0x19, 0xDF, 0x5B, 0x95, 0xE3, 0x7A, 0x6A, 0x28, 0x24, 0xA4, 0x0A, 0x83, 0x83, 0xBD, 0xBA, 0xF2, 0xF2, 0x52, 0x20, 0xE9, 0xAA, 0x3B, 0xD1, 0xDD, 0xE4, 0x9A, 0x9A, 0x9C, 0xC0, 0x30, 0x8F, 0x01, 0x40, 0x06, 0xE0, 0x2B, 0x95, 0x62, 0x89, 0x2A, 0x34, 0x75, 0x22, 0x68, 0x64, 0x6E, 0x7C, 0x2E, 0x83, 0x50, 0x5A, 0xCE, 0x7B, 0x0B, 0xE8, 0xF8, 0x71, 0xE6, 0xF7, 0x73, 0x8E, 0xEB, 0x84, 0xD2, 0x73, 0x5D, 0x9D, 0xBE, 0x5E, 0xF5, 0x90, 0xF9, 0xAB, 0x0A, 0x10, 0x7E, 0x23, 0x48, 0xF4, 0xAD, 0x70, 0x2E, 0xF7, 0xD4, 0x51, 0xD5, 0x8B, 0x3A, 0xF7, 0xCA, 0x90, 0x4C, 0xDC, 0x80, 0x19, 0x26, 0x65, 0xC9, 0x37, 0xBD, 0x52, 0x81, 0xF1, 0x8B, 0xCD });
            var compilationAS1 = CreateCompilation(
                new AssemblyIdentity(assemblyNameA, new Version(1, 1, 1, 1), cultureName: "", publicKeyOrToken: publicKeyA, hasPublicKey: true),
                new[] { sourceA },
                references: new[] { MscorlibRef },
                options: TestOptions.DebugDll.WithDelaySign(true));
            var referenceAS1 = compilationAS1.EmitToImageReference();
            var identityAS1 = referenceAS1.GetAssemblyIdentity();
            var compilationAS2 = CreateCompilation(
                new AssemblyIdentity(assemblyNameA, new Version(2, 1, 1, 1), cultureName: "", publicKeyOrToken: publicKeyA, hasPublicKey: true),
                new[] { sourceA },
                references: new[] { MscorlibRef },
                options: TestOptions.DebugDll.WithDelaySign(true));
            var referenceAS2 = compilationAS2.EmitToImageReference();
            var identityAS2 = referenceAS2.GetAssemblyIdentity();

            // Assembly B, multiple versions, not strong name.
            var assemblyNameB = ExpressionCompilerUtilities.GenerateUniqueName();
            var compilationBN1 = CreateCompilation(
                new AssemblyIdentity(assemblyNameB, new Version(1, 1, 1, 1)),
                new[] { sourceB },
                references: new[] { MscorlibRef, referenceAS1 },
                options: TestOptions.DebugDll);
            var referenceBN1 = compilationBN1.EmitToImageReference();
            var identityBN1 = referenceBN1.GetAssemblyIdentity();
            var compilationBN2 = CreateCompilation(
                new AssemblyIdentity(assemblyNameB, new Version(2, 2, 2, 1)),
                new[] { sourceB },
                references: new[] { MscorlibRef, referenceAS1 },
                options: TestOptions.DebugDll);
            var referenceBN2 = compilationBN2.EmitToImageReference();
            var identityBN2 = referenceBN2.GetAssemblyIdentity();

            // Assembly B, multiple versions, strong name.
            var publicKeyB = ImmutableArray.CreateRange(new byte[] { 0x00, 0x24, 0x00, 0x00, 0x04, 0x80, 0x00, 0x00, 0x94, 0x00, 0x00, 0x00, 0x06, 0x02, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x53, 0x52, 0x41, 0x31, 0x00, 0x04, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0xED, 0xD3, 0x22, 0xCB, 0x6B, 0xF8, 0xD4, 0xA2, 0xFC, 0xCC, 0x87, 0x37, 0x04, 0x06, 0x04, 0xCE, 0xE7, 0xB2, 0xA6, 0xF8, 0x4A, 0xEE, 0xF3, 0x19, 0xDF, 0x5B, 0x95, 0xE3, 0x7A, 0x6A, 0x28, 0x24, 0xA4, 0x0A, 0x83, 0x83, 0xBD, 0xBA, 0xF2, 0xF2, 0x52, 0x20, 0xE9, 0xAA, 0x3B, 0xD1, 0xDD, 0xE4, 0x9A, 0x9A, 0x9C, 0xC0, 0x30, 0x8F, 0x01, 0x40, 0x06, 0xE0, 0x2B, 0x95, 0x62, 0x89, 0x2A, 0x34, 0x75, 0x22, 0x68, 0x64, 0x6E, 0x7C, 0x2E, 0x83, 0x50, 0x5A, 0xCE, 0x7B, 0x0B, 0xE8, 0xF8, 0x71, 0xE6, 0xF7, 0x73, 0x8E, 0xEB, 0x84, 0xD2, 0x73, 0x5D, 0x9D, 0xBE, 0x5E, 0xF5, 0x90, 0xF9, 0xAB, 0x0A, 0x10, 0x7E, 0x23, 0x48, 0xF4, 0xAD, 0x70, 0x2E, 0xF7, 0xD4, 0x51, 0xD5, 0x8B, 0x3A, 0xF7, 0xCA, 0x90, 0x4C, 0xDC, 0x80, 0x19, 0x26, 0x65, 0xC9, 0x37, 0xBD, 0x52, 0x81, 0xF1, 0x8B, 0xCD });
            var compilationBS1 = CreateCompilation(
                new AssemblyIdentity(assemblyNameB, new Version(1, 1, 1, 1), cultureName: "", publicKeyOrToken: publicKeyB, hasPublicKey: true),
                new[] { sourceB },
                references: new[] { MscorlibRef, referenceAS1 },
                options: TestOptions.DebugDll.WithDelaySign(true));
            var referenceBS1 = compilationBS1.EmitToImageReference();
            var identityBS1 = referenceBS1.GetAssemblyIdentity();
            var compilationBS2 = CreateCompilation(
                new AssemblyIdentity(assemblyNameB, new Version(2, 2, 2, 1), cultureName: "", publicKeyOrToken: publicKeyB, hasPublicKey: true),
                new[] { sourceB },
                references: new[] { MscorlibRef, referenceAS2 },
                options: TestOptions.DebugDll.WithDelaySign(true));
            var referenceBS2 = compilationBS2.EmitToImageReference();
            var identityBS2 = referenceBS2.GetAssemblyIdentity();

            var mscorlibIdentity = MscorlibRef.GetAssemblyIdentity();
            var mscorlib20Identity = MscorlibRef_v20.GetAssemblyIdentity();
            var systemRefIdentity = SystemRef.GetAssemblyIdentity();
            var systemRef20Identity = SystemRef_v20.GetAssemblyIdentity();

            // No duplicates.
            VerifyAssemblyReferences(
                referenceBN1,
                ImmutableArray.Create(MscorlibRef, referenceAS1, referenceBN1),
                ImmutableArray.Create(mscorlibIdentity, identityAS1, identityBN1));
            // No duplicates, extra references.
            VerifyAssemblyReferences(
                referenceAS1,
                ImmutableArray.Create(MscorlibRef, referenceBN1, referenceAS1, referenceBS2),
                ImmutableArray.Create(mscorlibIdentity, identityAS1));
            // Strong-named, non-strong-named, and framework duplicates, same version (no aliases).
            VerifyAssemblyReferences(
                referenceBN2,
                ImmutableArray.Create(MscorlibRef, referenceAS1, MscorlibRef, referenceBN2, referenceBN2, referenceAS1, referenceAS1),
                ImmutableArray.Create(mscorlibIdentity, identityAS1, identityBN2));
            // Strong-named, non-strong-named, and framework duplicates, different versions.
            VerifyAssemblyReferences(
                referenceBN1,
                ImmutableArray.Create(MscorlibRef, referenceAS1, MscorlibRef_v20, referenceAS2, referenceBN2, referenceBN1, referenceAS2, referenceAS1, referenceBN1),
                ImmutableArray.Create(mscorlibIdentity, identityAS2, identityBN2));
            VerifyAssemblyReferences(
                referenceBN2,
                ImmutableArray.Create(MscorlibRef, referenceAS1, MscorlibRef_v20, referenceAS2, referenceBN2, referenceBN1, referenceAS2, referenceAS1, referenceBN1),
                ImmutableArray.Create(mscorlibIdentity, identityAS2, identityBN2));
            // Strong-named, different versions.
            VerifyAssemblyReferences(
                referenceBS1,
                ImmutableArray.Create(MscorlibRef, referenceAS1, referenceAS2, referenceBS2, referenceBS1, referenceAS2, referenceAS1, referenceBS1),
                ImmutableArray.Create(mscorlibIdentity, identityAS2, identityBS2));
            VerifyAssemblyReferences(
                referenceBS2,
                ImmutableArray.Create(MscorlibRef, referenceAS1, referenceAS2, referenceBS2, referenceBS1, referenceAS2, referenceAS1, referenceBS1),
                ImmutableArray.Create(mscorlibIdentity, identityAS2, identityBS2));

            // Assembly C, multiple versions, not strong name.
            var assemblyNameC = ExpressionCompilerUtilities.GenerateUniqueName();
            var compilationCN1 = CreateCompilation(
                new AssemblyIdentity(assemblyNameC, new Version(1, 1, 1, 1)),
                new[] { sourceC },
                references: new[] { MscorlibRef, referenceBS1 },
                options: TestOptions.DebugDll);
            byte[] exeBytesC1;
            byte[] pdbBytesC1;
            ImmutableArray<MetadataReference> references;
            compilationCN1.EmitAndGetReferences(out exeBytesC1, out pdbBytesC1, out references);
            var compilationCN2 = CreateCompilation(
                new AssemblyIdentity(assemblyNameC, new Version(2, 1, 1, 1)),
                new[] { sourceC },
                references: new[] { MscorlibRef, referenceBS2 },
                options: TestOptions.DebugDll);
            byte[] exeBytesC2;
            byte[] pdbBytesC2;
            compilationCN1.EmitAndGetReferences(out exeBytesC2, out pdbBytesC2, out references);

            // Duplicate assemblies, target module referencing BS1.
            using (var runtime = CreateRuntimeInstance(
                assemblyNameC,
                ImmutableArray.Create(MscorlibRef, referenceAS1, referenceAS2, referenceBS2, referenceBS1, referenceBS2),
                exeBytesC1,
                new SymReader(pdbBytesC1)))
            {
                ImmutableArray<MetadataBlock> typeBlocks;
                ImmutableArray<MetadataBlock> methodBlocks;
                Guid moduleVersionId;
                ISymUnmanagedReader symReader;
                int typeToken;
                int methodToken;
                int localSignatureToken;
                GetContextState(runtime, "C", out typeBlocks, out moduleVersionId, out symReader, out typeToken, out localSignatureToken);
                GetContextState(runtime, "C.M", out methodBlocks, out moduleVersionId, out symReader, out methodToken, out localSignatureToken);
                int ilOffset = ExpressionCompilerTestHelpers.GetOffset(methodToken, symReader);

                // Compile expression with type context with all modules.
                var context = EvaluationContext.CreateTypeContext(
                    default(CSharpMetadataContext),
                    typeBlocks,
                    moduleVersionId,
                    typeToken);
                string error;
                // A is ambiguous.
                var testData = new CompilationTestData();
                context.CompileExpression("new A()", out error, testData);
                Assert.True(error.StartsWith("error CS0433: The type 'A' exists in both "));
                testData = new CompilationTestData();
                // B is ambiguous.
                context.CompileExpression("new B()", out error, testData);
                Assert.True(error.StartsWith("error CS0433: The type 'B' exists in both "));
                var previous = new CSharpMetadataContext(typeBlocks, context);

                // Compile expression with type context with referenced modules only.
                context = EvaluationContext.CreateTypeContext(
                    typeBlocks.ToCompilationReferencedModulesOnly(moduleVersionId),
                    moduleVersionId,
                    typeToken);
                // A is unrecognized since there were no direct references to AS1 or AS2.
                testData = new CompilationTestData();
                context.CompileExpression("new A()", out error, testData);
                Assert.Equal(error, "error CS0246: The type or namespace name 'A' could not be found (are you missing a using directive or an assembly reference?)");
                testData = new CompilationTestData();
                // B should be resolved to BS2.
                context.CompileExpression("new B()", out error, testData);
                var methodData = testData.GetMethodData("<>x.<>m0");
                methodData.VerifyIL(
@"{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  newobj     ""B..ctor()""
  IL_0005:  ret
}");
                Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityBS2.GetDisplayName());
                // B.F should result in missing assembly AS2 since there were no direct references to AS2.
                ResultProperties resultProperties;
                ImmutableArray<AssemblyIdentity> missingAssemblyIdentities;
                testData = new CompilationTestData();
                context.CompileExpression(
                    InspectionContextFactory.Empty,
                    "(new B()).F",
                    DkmEvaluationFlags.None,
                    DiagnosticFormatter.Instance,
                    out resultProperties,
                    out error,
                    out missingAssemblyIdentities,
                    EnsureEnglishUICulture.PreferredOrNull,
                    testData);
                AssertEx.Equal(missingAssemblyIdentities, ImmutableArray.Create(identityAS2));

                // Compile expression with method context with all modules.
                context = EvaluationContext.CreateMethodContext(
                    previous,
                    methodBlocks,
                    symReader,
                    moduleVersionId,
                    methodToken: methodToken,
                    methodVersion: 1,
                    ilOffset: ilOffset,
                    localSignatureToken: localSignatureToken);
                Assert.Equal(previous.Compilation, context.Compilation); // re-use type context compilation
                testData = new CompilationTestData();
                // A is ambiguous.
                testData = new CompilationTestData();
                context.CompileExpression("new A()", out error, testData);
                Assert.True(error.StartsWith("error CS0433: The type 'A' exists in both "));
                testData = new CompilationTestData();
                // B is ambiguous.
                context.CompileExpression("new B()", out error, testData);
                Assert.True(error.StartsWith("error CS0433: The type 'B' exists in both "));

                // Compile expression with method context with referenced modules only.
                context = EvaluationContext.CreateMethodContext(
                    methodBlocks.ToCompilationReferencedModulesOnly(moduleVersionId),
                    symReader,
                    moduleVersionId,
                    methodToken: methodToken,
                    methodVersion: 1,
                    ilOffset: ilOffset,
                    localSignatureToken: localSignatureToken);
                // A is unrecognized since there were no direct references to AS1 or AS2.
                testData = new CompilationTestData();
                context.CompileExpression("new A()", out error, testData);
                Assert.Equal(error, "error CS0246: The type or namespace name 'A' could not be found (are you missing a using directive or an assembly reference?)");
                testData = new CompilationTestData();
                // B should be resolved to BS2.
                context.CompileExpression("new B()", out error, testData);
                methodData = testData.GetMethodData("<>x.<>m0");
                methodData.VerifyIL(
@"{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  newobj     ""B..ctor()""
  IL_0005:  ret
}");
                Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityBS2.GetDisplayName());
                // B.F should result in missing assembly AS2 since there were no direct references to AS2.
                testData = new CompilationTestData();
                context.CompileExpression(
                    InspectionContextFactory.Empty,
                    "(new B()).F",
                    DkmEvaluationFlags.None,
                    DiagnosticFormatter.Instance,
                    out resultProperties,
                    out error,
                    out missingAssemblyIdentities,
                    EnsureEnglishUICulture.PreferredOrNull,
                    testData);
                AssertEx.Equal(missingAssemblyIdentities, ImmutableArray.Create(identityAS2));
            }
        }

        private static void VerifyAssemblyReferences(
            MetadataReference target,
            ImmutableArray<MetadataReference> references,
            ImmutableArray<AssemblyIdentity> expectedIdentities)
        {
            Assert.True(references.Contains(target));
            var modules = references.SelectAsArray(r => r.ToModuleInstance(fullImage: null, symReader: null, includeLocalSignatures: false));
            using (var runtime = new RuntimeInstance(modules))
            {
                var moduleVersionId = target.GetModuleVersionId();
                var blocks = runtime.Modules.SelectAsArray(m => m.MetadataBlock);
                var actualReferences = blocks.MakeAssemblyReferences(moduleVersionId, Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator.CompilationExtensions.IdentityComparer);
                // Verify identities.
                var actualIdentities = actualReferences.SelectAsArray(r => r.GetAssemblyIdentity());
                AssertEx.Equal(expectedIdentities, actualIdentities);
                // Verify identities are unique.
                var uniqueIdentities = actualIdentities.Distinct();
                Assert.Equal(actualIdentities.Length, uniqueIdentities.Length);
            }
        }

        [Fact]
        public void DuplicateTypesAndMethodsDifferentAssemblies()
        {
            var sourceA =
@"using N;
namespace N
{
    class C1 { }
    public static class E
    {
        public static A F(this A o) { return o; }
    }
}
class C2 { }
public class A
{
    public static void M()
    {
        var x = new A();
        var y = x.F();
    }
}";
            var sourceB =
@"using N;
namespace N
{
    class C1 { }
    public static class E
    {
        public static int F(this A o) { return 2; }
    }
}
class C2 { }
public class B
{
    static void M()
    {
        var x = new A();
    }
}";
            var assemblyNameA = ExpressionCompilerUtilities.GenerateUniqueName();
            var compilationA = CreateCompilationWithMscorlibAndSystemCore(sourceA, options: TestOptions.DebugDll, assemblyName: assemblyNameA);
            byte[] exeBytesA;
            byte[] pdbBytesA;
            ImmutableArray<MetadataReference> referencesA;
            compilationA.EmitAndGetReferences(out exeBytesA, out pdbBytesA, out referencesA);
            var referenceA = AssemblyMetadata.CreateFromImage(exeBytesA).GetReference(display: assemblyNameA);
            var identityA = referenceA.GetAssemblyIdentity();
            var moduleA = referenceA.ToModuleInstance(exeBytesA, new SymReader(pdbBytesA));

            var assemblyNameB = ExpressionCompilerUtilities.GenerateUniqueName();
            var compilationB = CreateCompilationWithMscorlibAndSystemCore(sourceB, options: TestOptions.DebugDll, assemblyName: assemblyNameB, references: new[] { referenceA });
            byte[] exeBytesB;
            byte[] pdbBytesB;
            ImmutableArray<MetadataReference> referencesB;
            compilationB.EmitAndGetReferences(out exeBytesB, out pdbBytesB, out referencesB);
            var referenceB = AssemblyMetadata.CreateFromImage(exeBytesB).GetReference(display: assemblyNameB);
            var moduleB = referenceB.ToModuleInstance(exeBytesB, new SymReader(pdbBytesB));

            var moduleBuilder = ArrayBuilder<ModuleInstance>.GetInstance();
            moduleBuilder.AddRange(referencesA.Select(r => r.ToModuleInstance(null, null)));
            moduleBuilder.Add(moduleA);
            moduleBuilder.Add(moduleB);
            var modules = moduleBuilder.ToImmutableAndFree();

            using (var runtime = new RuntimeInstance(modules))
            {
                ImmutableArray<MetadataBlock> blocks;
                Guid moduleVersionId;
                ISymUnmanagedReader symReader;
                int typeToken;
                int methodToken;
                int localSignatureToken;
                GetContextState(runtime, "B", out blocks, out moduleVersionId, out symReader, out typeToken, out localSignatureToken);
                string errorMessage;
                CompilationTestData testData;
                var contextFactory = CreateTypeContextFactory(moduleVersionId, typeToken);

                // Duplicate type in namespace, at type scope.
                ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "new N.C1()", contextFactory, getMetaDataBytesPtr: null, errorMessage: out errorMessage, testData: out testData);
                Assert.True(errorMessage.StartsWith("error CS0433: The type 'C1' exists in both "));

                GetContextState(runtime, "B.M", out blocks, out moduleVersionId, out symReader, out methodToken, out localSignatureToken);
                contextFactory = CreateMethodContextFactory(moduleVersionId, symReader, methodToken, localSignatureToken);

                // Duplicate type in namespace, at method scope.
                ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "new C1()", contextFactory, getMetaDataBytesPtr: null, errorMessage: out errorMessage, testData: out testData);
                Assert.True(errorMessage.StartsWith("error CS0433: The type 'C1' exists in both "));

                // Duplicate type in global namespace, at method scope.
                ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "new C2()", contextFactory, getMetaDataBytesPtr: null, errorMessage: out errorMessage, testData: out testData);
                Assert.True(errorMessage.StartsWith("error CS0433: The type 'C2' exists in both "));

                // Duplicate extension method, at method scope.
                ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "x.F()", contextFactory, getMetaDataBytesPtr: null, errorMessage: out errorMessage, testData: out testData);
                Assert.Equal(errorMessage, "(1,3): error CS0121: The call is ambiguous between the following methods or properties: 'N.E.F(A)' and 'N.E.F(A)'");

                // Same tests as above but in library that does not directly reference duplicates.
                GetContextState(runtime, "A", out blocks, out moduleVersionId, out symReader, out typeToken, out localSignatureToken);
                contextFactory = CreateTypeContextFactory(moduleVersionId, typeToken);

                // Duplicate type in namespace, at type scope.
                ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "new N.C1()", contextFactory, getMetaDataBytesPtr: null, errorMessage: out errorMessage, testData: out testData);
                Assert.Null(errorMessage);
                var methodData = testData.GetMethodData("<>x.<>m0");
                methodData.VerifyIL(
@"{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  newobj     ""N.C1..ctor()""
  IL_0005:  ret
}");
                Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityA.GetDisplayName());

                GetContextState(runtime, "A.M", out blocks, out moduleVersionId, out symReader, out methodToken, out localSignatureToken);
                contextFactory = CreateMethodContextFactory(moduleVersionId, symReader, methodToken, localSignatureToken);

                // Duplicate type in global namespace, at method scope.
                ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "new C2()", contextFactory, getMetaDataBytesPtr: null, errorMessage: out errorMessage, testData: out testData);
                Assert.Null(errorMessage);
                methodData = testData.GetMethodData("<>x.<>m0");
                methodData.VerifyIL(
@"{
  // Code size        6 (0x6)
  .maxstack  1
  .locals init (A V_0, //x
                A V_1) //y
  IL_0000:  newobj     ""C2..ctor()""
  IL_0005:  ret
}");
                Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityA.GetDisplayName());

                // Duplicate extension method, at method scope.
                ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "x.F()", contextFactory, getMetaDataBytesPtr: null, errorMessage: out errorMessage, testData: out testData);
                Assert.Null(errorMessage);
                methodData = testData.GetMethodData("<>x.<>m0");
                methodData.VerifyIL(
@"{
  // Code size        7 (0x7)
  .maxstack  1
  .locals init (A V_0, //x
                A V_1) //y
  IL_0000:  ldloc.0
  IL_0001:  call       ""A N.E.F(A)""
  IL_0006:  ret
}");
                Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityA.GetDisplayName());
            }
        }

        private static ExpressionCompiler.CreateContextDelegate CreateTypeContextFactory(
            Guid moduleVersionId,
            int typeToken)
        {
            return (blocks, useReferencedModulesOnly) => EvaluationContext.CreateTypeContext(
                useReferencedModulesOnly ? blocks.ToCompilationReferencedModulesOnly(moduleVersionId) : blocks.ToCompilation(),
                moduleVersionId,
                typeToken);
        }

        private static ExpressionCompiler.CreateContextDelegate CreateMethodContextFactory(
            Guid moduleVersionId,
            ISymUnmanagedReader symReader,
            int methodToken,
            int localSignatureToken)
        {
            return (blocks, useReferencedModulesOnly) => EvaluationContext.CreateMethodContext(
                useReferencedModulesOnly ? blocks.ToCompilationReferencedModulesOnly(moduleVersionId) : blocks.ToCompilation(),
                symReader,
                moduleVersionId,
                methodToken,
                methodVersion: 1,
                ilOffset: 0,
                localSignatureToken: localSignatureToken);
        }
    }
}
