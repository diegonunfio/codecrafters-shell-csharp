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

            string input = ReadInput();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            List<string> parts = ParseInput(input);

            if (parts.Count == 0)
                continue;

            string? outputFile = null;
            string? errorFile = null;
            bool appendOutput = false;
            bool appendError = false;

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
                        appendError = false;
                        i++;
                    }

                    continue;
                }

                if (parts[i] == "2>>")
                {
                    if (i + 1 < parts.Count)
                    {
                        errorFile = parts[i + 1];
                        appendError = true;
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
                appendOutput,
                appendError
            );
        }
    }

    static string ReadInput()
    {
        StringBuilder input = new();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return input.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (input.Length > 0)
                {
                    input.Length--;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                string current = input.ToString();

                if (!current.Any(char.IsWhiteSpace))
                {
                    string? match = FindCompletion(current);

                    if (match != null)
                    {
                        string remaining = match.Substring(current.Length);

                        input.Append(remaining);
                        input.Append(' ');

                        Console.Write(remaining);
                        Console.Write(" ");
                    }
                    else
                    {
                        Console.Write('\x07');
                    }
                }
                else
                {
                    Console.Write('\x07');
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                input.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }

    static string? FindCompletion(string prefix)
    {
        foreach (string builtin in builtins)
        {
            if (builtin.StartsWith(prefix, StringComparison.Ordinal))
                return builtin;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
            return null;

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(directory))
                continue;

            if (!Directory.Exists(directory))
                continue;

            try
            {
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    string name = Path.GetFileName(file);

                    if (!name.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    if (IsExecutable(file))
                        return name;
                }
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    static bool IsExecutable(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            UnixFileMode mode = File.GetUnixFileMode(filePath);

            return
                mode.HasFlag(UnixFileMode.UserExecute) ||
                mode.HasFlag(UnixFileMode.GroupExecute) ||
                mode.HasFlag(UnixFileMode.OtherExecute);
        }
        catch
        {
            return false;
        }
    }

    static void ExecuteCommand(
        string command,
        string[] args,
        string? outputFile,
        string? errorFile,
        bool appendOutput,
        bool appendError)
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

                PrepareErrorFile(errorFile, appendError);
                break;

            case "type":
                HandleType(
                    args,
                    outputFile,
                    appendOutput
                );

                PrepareErrorFile(errorFile, appendError);
                break;

            case "pwd":
                WriteOutput(
                    Directory.GetCurrentDirectory() + Environment.NewLine,
                    outputFile,
                    appendOutput
                );

                PrepareErrorFile(errorFile, appendError);
                break;

            case "cd":
                HandleCd(
                    args,
                    errorFile,
                    appendError
                );
                break;

            default:
                RunExternalCommand(
                    command,
                    args,
                    outputFile,
                    errorFile,
                    appendOutput,
                    appendError
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
            File.AppendAllText(outputFile, text);
        else
            File.WriteAllText(outputFile, text);
    }

    static void WriteError(
        string text,
        string? errorFile,
        bool append)
    {
        if (errorFile == null)
        {
            Console.Error.Write(text);
            return;
        }

        if (append)
            File.AppendAllText(errorFile, text);
        else
            File.WriteAllText(errorFile, text);
    }

    static void PrepareErrorFile(
        string? errorFile,
        bool appendError)
    {
        if (errorFile == null)
            return;

        if (appendError)
        {
            if (!File.Exists(errorFile))
                File.WriteAllText(errorFile, "");
        }
        else
        {
            File.WriteAllText(errorFile, "");
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

                bool isAppend =
                    i + 1 < input.Length &&
                    input[i + 1] == '>';

                if (isAppend)
                {
                    i++;

                    if (prefix == "1")
                    {
                        args.Add("1>>");
                    }
                    else if (prefix == "2")
                    {
                        args.Add("2>>");
                    }
                    else
                    {
                        if (argumentStarted)
                            args.Add(prefix);

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
                            args.Add(prefix);

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
            args.Add(current.ToString());

        return args;
    }

    static void HandleCd(
        string[] args,
        string? errorFile,
        bool appendError)
    {
        if (args.Length == 0)
        {
            PrepareErrorFile(errorFile, appendError);
            return;
        }

        string directory = args[0];

        if (directory == "~")
        {
            string? home =
                Environment.GetEnvironmentVariable("HOME");

            if (string.IsNullOrEmpty(home) ||
                !Directory.Exists(home))
            {
                WriteError(
                    $"cd: {directory}: No such file or directory{Environment.NewLine}",
                    errorFile,
                    appendError
                );

                return;
            }

            Directory.SetCurrentDirectory(home);
            PrepareErrorFile(errorFile, appendError);
            return;
        }

        string targetPath =
            Path.IsPathRooted(directory)
                ? directory
                : Path.Combine(
                    Directory.GetCurrentDirectory(),
                    directory
                );

        if (!Directory.Exists(targetPath))
        {
            WriteError(
                $"cd: {directory}: No such file or directory{Environment.NewLine}",
                errorFile,
                appendError
            );

            return;
        }

        Directory.SetCurrentDirectory(
            Path.GetFullPath(targetPath)
        );

        PrepareErrorFile(errorFile, appendError);
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

            string fullPath = Path.Combine(directory, command);

            if (IsExecutable(fullPath))
                return fullPath;
        }

        return null;
    }

    static void RunExternalCommand(
        string command,
        string[] args,
        string? outputFile,
        string? errorFile,
        bool appendOutput,
        bool appendError)
    {
        string? executablePath = FindExecutable(command);

        if (executablePath == null)
        {
            WriteError(
                $"{command}: command not found{Environment.NewLine}",
                errorFile,
                appendError
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
            processInfo.ArgumentList.Add(arg);

        using Process? process = Process.Start(processInfo);

        if (process == null)
            return;

        string? stdout = null;
        string? stderr = null;

        if (outputFile != null)
            stdout = process.StandardOutput.ReadToEnd();

        if (errorFile != null)
            stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (outputFile != null)
        {
            if (appendOutput)
                File.AppendAllText(outputFile, stdout ?? "");
            else
                File.WriteAllText(outputFile, stdout ?? "");
        }

        if (errorFile != null)
        {
            if (appendError)
                File.AppendAllText(errorFile, stderr ?? "");
            else
                File.WriteAllText(errorFile, stderr ?? "");
        }
    }
}