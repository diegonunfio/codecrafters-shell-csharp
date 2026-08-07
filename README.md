# C# Shell

A Unix-like shell built from scratch in C# and .NET as part of the CodeCrafters **Build Your Own Shell** challenge.

The project focuses on understanding how a shell works internally: parsing input, resolving executables, managing processes, handling redirections, autocompletion, and background jobs.

## Features

* Built-in commands: `echo`, `cd`, `pwd`, `type`, `exit`, `complete`, `jobs`
* External command execution using `PATH`
* Single and double quoted arguments
* Backslash escaping
* stdout/stderr redirection
* Append redirection with `>>`
* File and directory tab completion
* Executable and builtin command completion
* Programmable completion with `complete -C`
* Longest common prefix completion
* Background execution with `&`
* Background job tracking with `jobs`
* Job status markers (`+` and `-`)
* Automatic reaping of completed jobs
* Job number recycling

## Example

```sh
$ echo hello
hello

$ type echo
echo is a shell builtin

$ sleep 10 &
[1] 12345

$ jobs
[1]+  Running                 sleep 10 &
```

Completed jobs are detected automatically:

```sh
$ sleep 2 &
[1] 12345

$ echo done
done
[1]+  Done                    sleep 2
$
```

## Running locally

Requires:

* .NET SDK 10
* Linux or WSL

Clone and run:

```sh
git clone https://github.com/diegonunfio/codecrafters-shell-csharp.git
cd codecrafters-shell-csharp
dotnet run
```

The implementation is located in:

```text
src/Program.cs
```

> On Windows, using WSL is recommended because the shell relies on Unix process and filesystem behavior.

## About

This started as my solution to the CodeCrafters [Build Your Own Shell](https://app.codecrafters.io/courses/shell/overview) challenge.

The main goal was to go beyond using a shell every day and understand how command parsing, process execution, completion, redirection, and job management work underneath.
