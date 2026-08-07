using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

class Program
{
    static bool runProgram = true;

    static readonly HashSet<string> builtins = new()
    {
        "exit",
        "echo",
        "type",
        "pwd",
        "cd"
    };

    static void Main()
    {
        while (runProgram)
        {
            Console.Write("$ ");

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            List<string> parts = ParseInput(input);

            if (parts.Count == 0)
                continue;

            string? outputFile = null;
            string? errorFile = null;
            bool appendOutput = false;

            List<string> commandParts = new();

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == ">" || parts[i] == "1>")
                {
                    if (i + 1 < parts.Count)
                    {
                        outputFile = parts[i + 1];
                        appendOutput = false;
                        i++;
                    }

                    continue;
                }

                if (parts[i] == ">>" || parts[i] == "1>>")
                {
                    if (i + 1 < parts.Count)
                    {
                        outputFile = parts[i + 1];
                        appendOutput = true;
                        i++;
                    }

                    continue;
                }

                if (parts[i] == "2>")
                {
                    if (i + 1 < parts.Count)
                    {
                        errorFile = parts[i + 1];
                        i++;
                    }

                    continue;
                }

                commandParts.Add(parts[i]);
            }

            if (commandParts.Count == 0)
                continue;

            string command = commandParts[0];
            string[] args = commandParts.Skip(1).ToArray();

            ExecuteCommand(
                command,
                args,
                outputFile,
                errorFile,
                appendOutput
            );
        }
    }

    static void ExecuteCommand(
        string command,
        string[] args,
        string? outputFile,
        string? errorFile,
        bool appendOutput)
    {
        switch (command)
        {
            case "exit":
                runProgram = false;
                break;

            case "echo":
                WriteOutput(
                    string.Join(" ", args) + Environment.NewLine,
                    outputFile,
                    appendOutput
                );

                CreateEmptyFileIfNeeded(errorFile);
                break;

            case "type":
                HandleType(args, outputFile, appendOutput);
                CreateEmptyFileIfNeeded(errorFile);
                break;

            case "pwd":
                WriteOutput(
                    Directory.GetCurrentDirectory() + Environment.NewLine,
                    outputFile,
                    appendOutput
                );

                CreateEmptyFileIfNeeded(errorFile);
                break;

            case "cd":
                HandleCd(args, errorFile);
                break;

            default:
                RunExternalCommand(
                    command,
                    args,
                    outputFile,
                    errorFile,
                    appendOutput
                );
                break;
        }
    }

    static void WriteOutput(
        string text,
        string? outputFile,
        bool append)
    {
        if (outputFile == null)
        {
            Console.Write(text);
            return;
        }

        if (append)
        {
            File.AppendAllText(outputFile, text);
        }
        else
        {
            File.WriteAllText(outputFile, text);
        }
    }

    static void WriteError(string text, string? errorFile)
    {
        if (errorFile == null)
        {
            Console.Error.Write(text);
            return;
        }

        File.WriteAllText(errorFile, text);
    }

    static void CreateEmptyFileIfNeeded(string? file)
    {
        if (file != null)
        {
            File.WriteAllText(file, "");
        }
    }

    static List<string> ParseInput(string input)
    {
        List<string> args = new();
        StringBuilder current = new();

        bool inSingleQuotes = false;
        bool inDoubleQuotes = false;
        bool argumentStarted = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (inSingleQuotes)
            {
                if (c == '\'')
                {
                    inSingleQuotes = false;
                }
                else
                {
                    current.Append(c);
                }

                argumentStarted = true;
                continue;
            }

            if (inDoubleQuotes)
            {
                if (c == '"')
                {
                    inDoubleQuotes = false;
                    argumentStarted = true;
                    continue;
                }

                if (c == '\\')
                {
                    if (i + 1 < input.Length)
                    {
                        char next = input[i + 1];

                        if (next == '"' || next == '\\')
                        {
                            current.Append(next);
                            i++;
                        }
                        else
                        {
                            current.Append('\\');
                        }
                    }
                    else
                    {
                        current.Append('\\');
                    }

                    argumentStarted = true;
                    continue;
                }

                current.Append(c);
                argumentStarted = true;
                continue;
            }

            if (c == '\\')
            {
                argumentStarted = true;

                if (i + 1 < input.Length)
                {
                    current.Append(input[i + 1]);
                    i++;
                }

                continue;
            }

            if (c == '\'')
            {
                inSingleQuotes = true;
                argumentStarted = true;
                continue;
            }

            if (c == '"')
            {
                inDoubleQuotes = true;
                argumentStarted = true;
                continue;
            }

            if (c == '>')
            {
                string prefix = current.ToString();

                if (i + 1 < input.Length && input[i + 1] == '>')
                {
                    i++;

                    if (prefix == "1")
                    {
                        args.Add("1>>");
                    }
                    else
                    {
                        if (argumentStarted)
                        {
                            args.Add(prefix);
                        }

                        args.Add(">>");
                    }
                }
                else
                {
                    if (prefix == "1")
                    {
                        args.Add("1>");
                    }
                    else if (prefix == "2")
                    {
                        args.Add("2>");
                    }
                    else
                    {
                        if (argumentStarted)
                        {
                            args.Add(prefix);
                        }

                        args.Add(">");
                    }
                }

                current.Clear();
                argumentStarted = false;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (argumentStarted)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    argumentStarted = false;
                }

                continue;
            }

            current.Append(c);
            argumentStarted = true;
        }

        if (argumentStarted)
        {
            args.Add(current.ToString());
        }

        return args;
    }

    static void HandleCd(string[] args, string? errorFile)
    {
        if (args.Length == 0)
        {
            CreateEmptyFileIfNeeded(errorFile);
            return;
        }

        string directory = args[0];

        if (directory == "~")
        {
            string? home = Environment.GetEnvironmentVariable("HOME");

            if (string.IsNullOrEmpty(home) || !Directory.Exists(home))
            {
                WriteError(
                    $"cd: {directory}: No such file or directory{Environment.NewLine}",
                    errorFile
                );
                return;
            }

            Directory.SetCurrentDirectory(home);
            CreateEmptyFileIfNeeded(errorFile);
            return;
        }

        string targetPath = Path.IsPathRooted(directory)
            ? directory
            : Path.Combine(
                Directory.GetCurrentDirectory(),
                directory
            );

        if (!Directory.Exists(targetPath))
        {
            WriteError(
                $"cd: {directory}: No such file or directory{Environment.NewLine}",
                errorFile
            );
            return;
        }

        Directory.SetCurrentDirectory(
            Path.GetFullPath(targetPath)
        );

        CreateEmptyFileIfNeeded(errorFile);
    }

    static void HandleType(
        string[] args,
        string? outputFile,
        bool appendOutput)
    {
        if (args.Length == 0)
            return;

        string command = args[0];

        if (builtins.Contains(command))
        {
            WriteOutput(
                $"{command} is a shell builtin{Environment.NewLine}",
                outputFile,
                appendOutput
            );
            return;
        }

        string? executablePath = FindExecutable(command);

        if (executablePath != null)
        {
            WriteOutput(
                $"{command} is {executablePath}{Environment.NewLine}",
                outputFile,
                appendOutput
            );
            return;
        }

        WriteOutput(
            $"{command}: not found{Environment.NewLine}",
            outputFile,
            appendOutput
        );
    }

    static string? FindExecutable(string command)
    {
        string? path =
            Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
            return null;

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(directory))
                continue;

            string fullPath =
                Path.Combine(directory, command);

            if (!File.Exists(fullPath))
                continue;

            try
            {
                UnixFileMode mode =
                    File.GetUnixFileMode(fullPath);

                bool executable =
                    mode.HasFlag(UnixFileMode.UserExecute) ||
                    mode.HasFlag(UnixFileMode.GroupExecute) ||
                    mode.HasFlag(UnixFileMode.OtherExecute);

                if (executable)
                    return fullPath;
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    static void RunExternalCommand(
        string command,
        string[] args,
        string? outputFile,
        string? errorFile,
        bool appendOutput)
    {
        string? executablePath = FindExecutable(command);

        if (executablePath == null)
        {
            WriteError(
                $"{command}: command not found{Environment.NewLine}",
                errorFile
            );
            return;
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/env",
            UseShellExecute = false,
            RedirectStandardOutput = outputFile != null,
            RedirectStandardError = errorFile != null
        };

        processInfo.ArgumentList.Add(command);

        foreach (string arg in args)
        {
            processInfo.ArgumentList.Add(arg);
        }

        using Process? process = Process.Start(processInfo);

        if (process == null)
            return;

        string? stdout = null;
        string? stderr = null;

        if (outputFile != null)
        {
            stdout = process.StandardOutput.ReadToEnd();
        }

        if (errorFile != null)
        {
            stderr = process.StandardError.ReadToEnd();
        }

        process.WaitForExit();

        if (outputFile != null)
        {
            if (appendOutput)
            {
                File.AppendAllText(outputFile, stdout ?? "");
            }
            else
            {
                File.WriteAllText(outputFile, stdout ?? "");
            }
        }

        if (errorFile != null)
        {
            File.WriteAllText(errorFile, stderr ?? "");
        }
    }
}