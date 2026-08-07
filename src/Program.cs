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
            List<string> commandParts = new();

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == ">" || parts[i] == "1>")
                {
                    if (i + 1 < parts.Count)
                    {
                        outputFile = parts[i + 1];
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

            ExecuteCommand(command, args, outputFile);
        }
    }

    static void ExecuteCommand(
        string command,
        string[] args,
        string? outputFile)
    {
        switch (command)
        {
            case "exit":
                runProgram = false;
                break;

            case "echo":
                WriteOutput(
                    string.Join(" ", args) + Environment.NewLine,
                    outputFile
                );
                break;

            case "type":
                HandleType(args, outputFile);
                break;

            case "pwd":
                WriteOutput(
                    Directory.GetCurrentDirectory() + Environment.NewLine,
                    outputFile
                );
                break;

            case "cd":
                HandleCd(args);
                break;

            default:
                RunExternalCommand(command, args, outputFile);
                break;
        }
    }

    static void WriteOutput(string text, string? outputFile)
    {
        if (outputFile == null)
        {
            Console.Write(text);
            return;
        }

        File.WriteAllText(outputFile, text);
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
                if (current.Length > 0 || argumentStarted)
                {
                    string value = current.ToString();

                    if (value == "1")
                    {
                        args.Add("1>");
                    }
                    else
                    {
                        if (value.Length > 0)
                            args.Add(value);

                        args.Add(">");
                    }

                    current.Clear();
                    argumentStarted = false;
                }
                else
                {
                    args.Add(">");
                }

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

    static void HandleCd(string[] args)
    {
        if (args.Length == 0)
            return;

        string directory = args[0];

        if (directory == "~")
        {
            string? home = Environment.GetEnvironmentVariable("HOME");

            if (string.IsNullOrEmpty(home) || !Directory.Exists(home))
            {
                Console.WriteLine(
                    $"cd: {directory}: No such file or directory"
                );
                return;
            }

            Directory.SetCurrentDirectory(home);
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
            Console.WriteLine(
                $"cd: {directory}: No such file or directory"
            );
            return;
        }

        Directory.SetCurrentDirectory(
            Path.GetFullPath(targetPath)
        );
    }

    static void HandleType(
        string[] args,
        string? outputFile)
    {
        if (args.Length == 0)
            return;

        string command = args[0];

        if (builtins.Contains(command))
        {
            WriteOutput(
                $"{command} is a shell builtin{Environment.NewLine}",
                outputFile
            );
            return;
        }

        string? executablePath = FindExecutable(command);

        if (executablePath != null)
        {
            WriteOutput(
                $"{command} is {executablePath}{Environment.NewLine}",
                outputFile
            );
            return;
        }

        WriteOutput(
            $"{command}: not found{Environment.NewLine}",
            outputFile
        );
    }

    static string? FindExecutable(string command)
    {
        string? path =
            Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
            return null;

        foreach (
            string directory in path.Split(Path.PathSeparator)
        )
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
                    mode.HasFlag(
                        UnixFileMode.UserExecute
                    ) ||
                    mode.HasFlag(
                        UnixFileMode.GroupExecute
                    ) ||
                    mode.HasFlag(
                        UnixFileMode.OtherExecute
                    );

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
        string? outputFile)
    {
        string? executablePath =
            FindExecutable(command);

        if (executablePath == null)
        {
            Console.WriteLine(
                $"{command}: command not found"
            );
            return;
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/env",
            UseShellExecute = false,
            RedirectStandardOutput = outputFile != null
        };

        processInfo.ArgumentList.Add(command);

        foreach (string arg in args)
        {
            processInfo.ArgumentList.Add(arg);
        }

        using Process? process =
            Process.Start(processInfo);

        if (process == null)
            return;

        if (outputFile != null)
        {
            string output =
                process.StandardOutput.ReadToEnd();

            File.WriteAllText(outputFile, output);
        }

        process.WaitForExit();
    }
}