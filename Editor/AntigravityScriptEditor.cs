using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Unity.CodeEditor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public class AntigravityScriptEditor : IExternalCodeEditor
{
    const string EditorName = "Antigravity";
    static readonly string[] KnownPaths =
    {
        "/Applications/Antigravity.app",
        "/Applications/Antigravity.app/Contents/MacOS/Antigravity"
    };

    static AntigravityScriptEditor()
    {
        CodeEditor.Register(new AntigravityScriptEditor());
        string current = EditorPrefs.GetString("kScriptsDefaultApp");
        if (IsAntigravityInstalled() && !current.Contains(EditorName))
        {
            // Registration handles availability; user preference is respected unless explicitly changed.
        }
    }

    private static bool IsAntigravityInstalled()
    {
        return KnownPaths.Any(p => File.Exists(p) || Directory.Exists(p));
    }

    private static string GetExecutablePath(string path)
    {
        if (path.EndsWith(".app"))
        {
            string macOSDir = Path.Combine(path, "Contents", "MacOS");
            
            // Try common executable names for Electron apps
            string[] possibleNames = { "Electron", "Antigravity", "antigravity" };
            
            foreach (var name in possibleNames)
            {
                string executable = Path.Combine(macOSDir, name);
                if (File.Exists(executable))
                {
                    return executable;
                }
            }
            
            // If no known name found, try to find any executable in the MacOS folder
            if (Directory.Exists(macOSDir))
            {
                var files = Directory.GetFiles(macOSDir);
                if (files.Length > 0)
                {
                    return files[0]; // Return the first file found
                }
            }
        }
        return path;
    }

    public CodeEditor.Installation[] Installations
    {
        get
        {
            var installations = new List<CodeEditor.Installation>();
            foreach (var path in KnownPaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    installations.Add(new CodeEditor.Installation
                    {
                        Name = EditorName,
                        Path = path
                    });
                }
            }
            return installations.ToArray();
        }
    }

    public void Initialize(string editorInstallationPath)
    {
        // FIX: Force project generation on init to help with IntelliSense
        ProjectGeneration.Sync();
    }

    public void OnGUI()
    {
        GUILayout.Label("Antigravity IDE Settings", EditorStyles.boldLabel);
    }

    public bool OpenProject(string filePath, int line, int column)
    {
        string installation = CodeEditor.CurrentEditorInstallation;
        string projectDirectory = Directory.GetCurrentDirectory();
        string solutionPath = Path.Combine(projectDirectory, $"{Path.GetFileName(projectDirectory)}.sln");

        // Ensure the solution file exists
        if (!File.Exists(solutionPath))
        {
            ProjectGeneration.Sync();
        }

        // Convert file path to absolute path if it's relative
        if (!string.IsNullOrEmpty(filePath) && !Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(projectDirectory, filePath);
        }

        // Build arguments - VS Code style:
        // First: project directory for workspace context
        // Then: -g file:line:column for goto functionality
        var argsList = new List<string>();
        
        // Add the project directory as the workspace
        argsList.Add($"\"{projectDirectory}\"");
        
        // Add the file with goto flag if a file is specified
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            if (line > 0)
            {
                // VS Code style: -g file:line:column
                argsList.Add("-g");
                argsList.Add($"\"{filePath}:{line}:{(column > 0 ? column : 1)}\"");
            }
            else
            {
                // Just open the file
                argsList.Add($"\"{filePath}\"");
            }
        }

        string arguments = string.Join(" ", argsList);
        
        UnityEngine.Debug.Log($"[Antigravity] Opening: {arguments}");

        try
        {
            Process process = new Process();
            
            // On macOS, directly invoke the executable inside the .app bundle
            // This ensures arguments are passed correctly
            string executablePath = GetExecutablePath(installation);
            
            process.StartInfo.FileName = executablePath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            
            UnityEngine.Debug.Log($"[Antigravity] Launching: {executablePath} {arguments}");
            
            process.Start();
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to open Antigravity: {e.Message}\nInstallation: {installation}");
            return false;
        }
    }

    public void SyncAll()
    {
        ProjectGeneration.Sync();
    }

    public void SyncIfNeeded(string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, string[] importedAssets)
    {
        ProjectGeneration.SyncIfNeeded(addedAssets, deletedAssets, movedAssets, movedFromAssetPaths, importedAssets);
    }

    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
        if (editorPath.Contains("Antigravity"))
        {
            installation = new CodeEditor.Installation
            {
                Name = EditorName,
                Path = editorPath
            };
            return true;
        }
