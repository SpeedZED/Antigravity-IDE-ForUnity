using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public static class ProjectGeneration
{
    public static void Sync()
    {
        var assemblies = CompilationPipeline.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            GenerateCsproj(assembly);
        }
        GenerateSolution(assemblies);
    }

    public static void SyncIfNeeded(string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, string[] importedAssets)
    {
        Sync();
    }

    private static void GenerateCsproj(Assembly assembly)
    {
        string projectPath = Path.Combine(Directory.GetCurrentDirectory(), $"{assembly.name}.csproj");
        
        // Get Unity's scripting defines for proper IntelliSense
        var defines = assembly.defines ?? new string[0];
        string defineConstants = string.Join(";", defines);
        
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<Project ToolsVersion=\"Current\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
        
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>");
        sb.AppendLine("    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
        sb.AppendLine("    <ProductVersion>10.0.20506</ProductVersion>");
        sb.AppendLine("    <SchemaVersion>2.0</SchemaVersion>");
        sb.AppendLine($"    <ProjectGuid>{{{GenerateGuid(assembly.name)}}}</ProjectGuid>");
        sb.AppendLine("    <OutputType>Library</OutputType>");
        sb.AppendLine($"    <AssemblyName>{assembly.name}</AssemblyName>");
        sb.AppendLine("    <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>");
        sb.AppendLine("    <FileAlignment>512</FileAlignment>");
        sb.AppendLine("    <BaseDirectory>.</BaseDirectory>");
        sb.AppendLine("    <LangVersion>latest</LangVersion>");
        sb.AppendLine($"    <DefineConstants>{defineConstants}</DefineConstants>");
        sb.AppendLine("    <ErrorReport>prompt</ErrorReport>");
        sb.AppendLine("    <WarningLevel>4</WarningLevel>");
        sb.AppendLine("    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>");
        sb.AppendLine("    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>");
        sb.AppendLine("    <DebugSymbols>true</DebugSymbols>");
        sb.AppendLine("    <DebugType>full</DebugType>");
        sb.AppendLine("    <Optimize>false</Optimize>");
        sb.AppendLine($"    <OutputPath>Temp/bin/Debug/</OutputPath>");
        sb.AppendLine($"    <IntermediateOutputPath>Temp/obj/Debug/</IntermediateOutputPath>");
        sb.AppendLine("    <NoWarn>0169;0649</NoWarn>");
        sb.AppendLine("  </PropertyGroup>");

        // Debug configuration
        sb.AppendLine("  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' \">");
        sb.AppendLine("    <DebugSymbols>true</DebugSymbols>");
        sb.AppendLine("    <DebugType>full</DebugType>");
        sb.AppendLine("    <Optimize>false</Optimize>");
        sb.AppendLine($"    <OutputPath>Temp/bin/Debug/</OutputPath>");
        sb.AppendLine($"    <DefineConstants>{defineConstants}</DefineConstants>");
        sb.AppendLine("    <ErrorReport>prompt</ErrorReport>");
        sb.AppendLine("    <WarningLevel>4</WarningLevel>");
        sb.AppendLine("  </PropertyGroup>");

        // Release configuration
        sb.AppendLine("  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' \">");
        sb.AppendLine("    <DebugType>pdbonly</DebugType>");
        sb.AppendLine("    <Optimize>true</Optimize>");
        sb.AppendLine($"    <OutputPath>Temp/bin/Release/</OutputPath>");
        sb.AppendLine($"    <DefineConstants>{defineConstants}</DefineConstants>");
        sb.AppendLine("    <ErrorReport>prompt</ErrorReport>");
        sb.AppendLine("    <WarningLevel>4</WarningLevel>");
        sb.AppendLine("  </PropertyGroup>");

        // Assembly references
        sb.AppendLine("  <ItemGroup>");
        foreach (var reference in assembly.compiledAssemblyReferences)
        {
            string refName = Path.GetFileNameWithoutExtension(reference);
            sb.AppendLine($"    <Reference Include=\"{refName}\">");
            sb.AppendLine($"      <HintPath>{reference}</HintPath>");
            sb.AppendLine("      <Private>False</Private>");
            sb.AppendLine("    </Reference>");
        }
        sb.AppendLine("  </ItemGroup>");

        // Source files
        sb.AppendLine("  <ItemGroup>");
        foreach (var sourceFile in assembly.sourceFiles)
        {
            // Convert to relative path if needed
            string relativePath = sourceFile;
            sb.AppendLine($"    <Compile Include=\"{relativePath}\" />");
        }
        sb.AppendLine("  </ItemGroup>");
        
        // Project references
        sb.AppendLine("  <ItemGroup>");
        foreach (var refAssembly in assembly.assemblyReferences)
        {
            sb.AppendLine($"    <ProjectReference Include=\"{refAssembly.name}.csproj\">");
            sb.AppendLine($"      <Project>{{{GenerateGuid(refAssembly.name)}}}</Project>");
            sb.AppendLine($"      <Name>{refAssembly.name}</Name>");
            sb.AppendLine("      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>");
            sb.AppendLine("    </ProjectReference>");
        }
        sb.AppendLine("  </ItemGroup>");

        sb.AppendLine("  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />");
        sb.AppendLine("</Project>");

        File.WriteAllText(projectPath, sb.ToString());
    }

    private static void GenerateSolution(Assembly[] assemblies)
    {
        string solutionPath = Path.Combine(Directory.GetCurrentDirectory(), $"{Path.GetFileName(Directory.GetCurrentDirectory())}.sln");
        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio 15");
        
        foreach (var assembly in assemblies)
        {
            string guid = GenerateGuid(assembly.name);
            sb.AppendLine($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{assembly.name}\", \"{assembly.name}.csproj\", \"{{{guid}}}\"");
            sb.AppendLine("EndProject");
        }
        
        File.WriteAllText(solutionPath, sb.ToString());
    }

    private static string GenerateGuid(string input)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(input));
            return new Guid(hash).ToString().ToUpper();
        }
    }
}
