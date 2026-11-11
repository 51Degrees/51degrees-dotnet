/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.IpIntelligence.Examples;

public static class ExampleUtils
{
    /// <summary>
    /// The default environment variable key used to get the resource key
    /// to use when running cloud examples. This aligned name is checked
    /// first, before any legacy variable names.
    /// </summary>
    public const string CLOUD_RESOURCE_KEY_ENV_VAR = "_51DEGREES_RESOURCE_KEY";

    /// <summary>
    /// The legacy environment variable key used to get the resource key
    /// to use when running cloud examples. Retained for backwards
    /// compatibility and checked when <see cref="CLOUD_RESOURCE_KEY_ENV_VAR"/>
    /// is not set.
    /// </summary>
    public const string LEGACY_CLOUD_RESOURCE_KEY_ENV_VAR = "RESOURCE_KEY";

    /// <summary>
    /// The legacy environment variable key used by other 51Degrees
    /// repositories to get the resource key. Retained for backwards
    /// compatibility and checked last.
    /// </summary>
    public const string LEGACY_SUPER_RESOURCE_KEY_ENV_VAR = "SUPER_RESOURCE_KEY";

    /// <summary>
    /// The default environment variable key used to get the end point URL
    /// to use when running cloud examples. Can be used to override the
    /// appsettings.json configuration for testing custom end points.
    /// </summary>
    public const string CLOUD_END_POINT_ENV_VAR = "_51DEGREES_CLOUD_ENDPOINT";

    /// <summary>
    /// Timeout used when searching for files.
    /// </summary>
    private const int FindFileTimeoutMs = 10000;

    /// <summary>
    /// If data file is older than this number of days then a warning will be displayed.
    /// </summary>
    public const int DataFileAgeWarning = 30;

    private const string DATA_OPTION = "--data-file";
    private const string DATA_OPTION_SHORT = "-d";
    private const string ADDRESSES_OPTION = "--ip-addresses-file";
    private const string ADDRESSES_OPTION_SHORT = "-a";
    private const string JSON_OPTION = "--json-output";
    private const string JSON_OPTION_SHORT = "-j";
    private const string HELP_OPTION = "--51help";
    private const string HELP_OPTION_SHORT = "-51h";

    private static string OptionMessage(string message, string option, string shortOption)
    {
        var padding = 32 - option.Length - shortOption.Length;
        return $"  {option}, {shortOption}{new string(' ', padding)}: {message}";
    }

    /// <summary>
    /// Print the available options to the output.
    /// </summary>
    private static void PrintHelp()
    {
        Console.WriteLine("Available options are:");
        Console.WriteLine(OptionMessage("Path to a 51Degrees IPI data file", DATA_OPTION, DATA_OPTION_SHORT));
        Console.WriteLine(OptionMessage("Path to a IP Addresses YAML file", ADDRESSES_OPTION, ADDRESSES_OPTION_SHORT));
        Console.WriteLine(OptionMessage("Path to a file to output JSON format results to", JSON_OPTION, JSON_OPTION_SHORT));
        Console.WriteLine(OptionMessage("Print this help", HELP_OPTION, HELP_OPTION_SHORT));
    }


    /// <summary>
    /// Parse the command line arguments passed to the example to get the common
    /// options.
    /// </summary>
    /// <param name="args">
    /// Command line options.
    /// </param>
    /// <returns>
    /// Parsed options, or null if help output is requested.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// If an invalid argument is passed.
    /// </exception>
    public static ExampleOptions ParseOptions(string[] args)
    {
        var options = new ExampleOptions();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("-"))
            {
                switch (args[i])
                {
                    case DATA_OPTION:
                    case DATA_OPTION_SHORT:
                        // Set data file path
                        options.DataFilePath = args[i + 1];
                        break;
                    case ADDRESSES_OPTION:
                    case ADDRESSES_OPTION_SHORT:
                        // Set data file path
                        options.EvidenceFile = args[i + 1];
                        break;
                    case JSON_OPTION:
                    case JSON_OPTION_SHORT:
                        // Set data file path
                        options.JsonOutput = args[i + 1];
                        break;
                    case HELP_OPTION:
                    case HELP_OPTION_SHORT:
                        // Set data file path
                        PrintHelp();
                        return null;
                    default:
                        throw new ArgumentException(
                            $"The option '{args[i]}' is not recognized. " +
                            $"Use {HELP_OPTION} ({HELP_OPTION_SHORT}) to list options");
                }
            }
            else
            {
                // Do nothing, this is a value.
            }
        }
        return options;
    }

    /// <summary>
    /// The data repository's folder, relative to the root of this repository.
    /// It holds the free data files, each listed as a link until it is
    /// downloaded, and the scripts that download them.
    /// </summary>
    private static readonly string DataFolder = Path.Combine("cxx", "data");

    /// <summary>
    /// Added to a data file's name to give the name of the file beside it
    /// that holds the address it is downloaded from.
    /// </summary>
    private const string LinkExtension = ".link";

    /// <summary>
    /// Sent with each download, so that downloads by these examples can be
    /// counted. It is left out inside GitHub Actions, where the data
    /// repository's script names the build instead, so that test runs are
    /// not counted as people using the examples.
    /// </summary>
    private const string DownloadSource = "examples-51degrees-dotnet";

    private static readonly bool IsWindows =
        Environment.OSVersion.Platform == PlatformID.Win32NT;

    private static readonly bool InGitHubActions =
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    /// <summary>
    /// The data repository's download script for this platform.
    /// </summary>
    private static readonly string DataScript = IsWindows ?
        "get-data-files.ps1" :
        "get-data-files.sh";

    /// <summary>
    /// Finds a file by name. The data repository in cxx/data is looked in
    /// first, and a free data file it links to is downloaded there when it
    /// is missing. Any other file is searched for with a background task
    /// within the working directory, and then each parent directory in turn,
    /// until it is found or a timeout is triggered.
    /// </summary>
    /// <param name="filename">
    /// The name of the file to find.
    /// </param>
    /// <param name="dir">
    /// The directory to start looking from. If not provided the current directory is used.
    /// </param>
    /// <returns>
    /// The path to the file, or null if it could not be found.
    /// </returns>
    public static string FindFile(
        string filename,
        DirectoryInfo dir = null)
    {
        var data = FindDataFolder(dir);
        if (data != null && Path.GetFileName(filename) == filename)
        {
            var file = FindInFolder(data, filename);
            if (file != null)
            {
                return file;
            }
            // A linked file is only looked for here, so that an older copy
            // elsewhere in the tree is never used in place of the download.
            var link = FindInFolder(data, filename + LinkExtension);
            if (link != null)
            {
                return DownloadDataFile(data, link, filename);
            }
        }
        var cancel = new CancellationTokenSource();
        // Start the file system search as a separate task.
        var searchTask = Task.Run(() => FindFile(filename, dir, cancel.Token));
        // Wait for either the search or a timeout task to complete.
        Task.WaitAny(searchTask, Task.Delay(FindFileTimeoutMs));
        cancel.Cancel();
        // If search has not got a result then return null.
        return searchTask.IsCompleted ? searchTask.Result : null;
    }

    private static string FindFile(
        string filename,
        DirectoryInfo dir,
        CancellationToken cancel)
    {
        if (dir == null)
        {
            dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        }
        string result = null;

        try
        {
            var files = dir.GetFiles(filename, SearchOption.AllDirectories);
            if (files.Length == 0 &&
                dir.Parent != null &&
                cancel.IsCancellationRequested == false)
            {
                result = FindFile(filename, dir.Parent, cancel);
            }
            else if (files.Length > 0)
            {
                result = files[0].FullName;
            }
        }
        // No matter what goes wrong here, we just want to indicate that we
        // couldn't find the file by returning null.
        catch { result = null; }

        return result;
    }

    /// <summary>
    /// Walks up from the directory to find the data repository. A folder
    /// without the download script, such as a submodule that has not been
    /// checked out, is passed over.
    /// </summary>
    private static string FindDataFolder(DirectoryInfo dir)
    {
        var current = dir ??
            new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var data = Path.Combine(current.FullName, DataFolder);
            if (File.Exists(Path.Combine(data, DataScript)))
            {
                return data;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string FindInFolder(string folder, string filename)
    {
        try
        {
            var files = Directory.GetFiles(
                folder,
                filename,
                SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }
        // A folder that cannot be read does not hold the file.
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads a free data file with the data repository's script, which
    /// saves it beside its link, and says what it runs. The script is used
    /// rather than a download here so that every language downloads the
    /// files the same way.
    /// </summary>
    /// <returns>
    /// The path to the file, or null if it could not be downloaded.
    /// </returns>
    private static string DownloadDataFile(
        string data,
        string link,
        string filename)
    {
        var script = Path.Combine(data, DataScript);
        ProcessStartInfo start;
        if (IsWindows)
        {
            var powerShell = PowerShell();
            start = new ProcessStartInfo(
                powerShell,
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" " +
                $"\"{filename}\"" +
                (InGitHubActions ? "" : $" -Source {DownloadSource}"));
            if (powerShell == WindowsPowerShell)
            {
                // Started from PowerShell 7, Windows PowerShell inherits its
                // module path and then cannot load its own commands.
                start.Environment.Remove("PSModulePath");
            }
        }
        else
        {
            start = new ProcessStartInfo(
                "sh",
                $"\"{script}\" " +
                (InGitHubActions ? "" : $"-s {DownloadSource} ") +
                $"\"{filename}\"");
        }
        start.UseShellExecute = false;
        var command = $"{start.FileName} {start.Arguments}";
        Console.WriteLine($"'{filename}' is missing, so downloading it with:");
        Console.WriteLine($"  {command}");
        try
        {
            using (var process = Process.Start(start))
            {
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        var file = Path.Combine(Path.GetDirectoryName(link), filename);
        if (File.Exists(file))
        {
            return file;
        }
        Console.Error.WriteLine(
            $"'{filename}' could not be downloaded. Run this command to get it:");
        Console.Error.WriteLine($"  {command}");
        return null;
    }

    private const string WindowsPowerShell = "powershell";

    /// <summary>
    /// PowerShell 7 when it is on the path, otherwise Windows PowerShell.
    /// </summary>
    private static string PowerShell()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var folder in path.Split(Path.PathSeparator))
        {
            try
            {
                if (File.Exists(Path.Combine(folder.Trim('"'), "pwsh.exe")))
                {
                    return "pwsh";
                }
            }
            // A malformed entry on the path cannot hold PowerShell 7.
            catch (ArgumentException)
            {
            }
        }
        return WindowsPowerShell;
    }

    /// <summary>
    /// Get the path to an IP Intelligence data file. The environment
    /// variables are checked first for an explicit path. The aligned
    /// variable named by <see cref="Constants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR"/>
    /// takes precedence over the legacy variable named by
    /// <see cref="Constants.LEGACY_IP_INTELLIGENCE_DATA_FILE_ENV_VAR"/>.
    /// If neither is set, the folder hierarchy is searched for the
    /// supplied file name using <see cref="FindFile(string, DirectoryInfo)"/>.
    /// </summary>
    /// <param name="filename">
    /// The data file name to search for when no environment variable
    /// supplies an explicit path.
    /// </param>
    /// <param name="dir">
    /// The directory to start searching from. If not provided the current
    /// directory is used.
    /// </param>
    /// <returns>
    /// The path to the data file, or null if no file could be found.
    /// </returns>
    public static string FindDataFile(
        string filename,
        DirectoryInfo dir = null)
    {
        var path = Environment.GetEnvironmentVariable(
            Constants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.GetEnvironmentVariable(
                Constants.LEGACY_IP_INTELLIGENCE_DATA_FILE_ENV_VAR);
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            path = FindFile(filename, dir);
        }
        return path;
    }

    /// <summary>
    /// Display information about the data file and log warnings if specific requirements
    /// are not met.
    /// </summary>
    /// <param name="engine"></param>
    /// <param name="logger"></param>
    public static void LogDataFileInfo(DataFileInfo info, ILogger logger)
    {
        if (info != null)
        {
            logger.LogInformation($"Using a '{info.Tier}' data file created " +
                $"'{info.PublishDate}' from location '{info.Filepath}'");
        }
    }

    /// <summary>
    /// Log a warning if the data file is old. An example run on a free data
    /// file says what the Enterprise data file adds in its own output.
    /// </summary>
    /// <param name="engine"></param>
    /// <param name="logger"></param>
    public static void LogDataFileStandardWarnings(DataFileInfo info, ILogger logger)
    {
        if (info != null)
        {
            if (DateTime.UtcNow > info.PublishDate.AddDays(DataFileAgeWarning))
            {
                logger.LogWarning($"This example is using a data file that is more than " +
                    $"{DataFileAgeWarning} days old. A more recent data file may be needed " +
                    $"for a more precise detection. The latest lite " +
                    $"data file is downloaded by running get-data-files.ps1 -Update or " +
                    $"get-data-files.sh -u in the data repository, the cxx/data submodule, " +
                    $"from GitHub https://github.com/51Degrees/data. Contact us " +
                    $"about the Enterprise data file, which includes automatic daily " +
                    $"updates, at https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-fiftyone.ipintelligence.examples-exampleutils.cs&utm_term=data-file-age-warning");
            }
        }
    }

    /// <summary>
    /// Checks if the supplied 51Degrees resource key / license key is invalid.
    /// Note that this cannot determine if the key is definitely valid, just whether it is
    /// definitely invalid.
    /// </summary>
    /// <param name="key">
    /// The key to check.
    /// </param>
    /// <returns></returns>
    public static bool IsInvalidKey(string key)
    {
        try
        {
            if (key == null) 
            {
                return true;
            }

            byte[] data = Convert.FromBase64String(key);
            string decodedString = Encoding.UTF8.GetString(data);

            return key.Trim().Length < 19 ||
                decodedString.Length < 14;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// This collection contains the various input values that will be passed to the
    /// IP Intelligence algorithm when running examples
    /// </summary>
    public static readonly List<Dictionary<string, object>>
        EvidenceValues = new List<Dictionary<string, object>>()
    {
        new Dictionary<string, object>()
        {
            { "query.client-ip", "116.154.188.222" }
        },
        new Dictionary<string, object>()
        {
            { "query.client-ip", "82.12.34.23" }
        },
        new Dictionary<string, object>()
        {
            { "query.client-ip", "187.102.194.5" }
        },
        new Dictionary<string, object>()
        {
            { "query.client-ip", "2001:0db8:085a:0000:0000:8a2e:0370:7334" }
        },
    };

    /// <summary>
    /// Checks if an environment variable exists with the key name provided
    /// and then runs the action with the value, or an empty string if the
    /// key does not exist.
    /// </summary>
    /// <param name="envVarName"></param>
    /// <param name="setValue"></param>
    public static void GetKeyFromEnv(
        string envVarName,
        Action<string> setValue)
    {
        var superKey = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrWhiteSpace(superKey) == false)
        {
            setValue(superKey);
        }
        else
        {
            setValue(string.Empty);
        }
    }

    /// <summary>
    /// Get the resource key from the environment. The aligned
    /// '_51DEGREES_RESOURCE_KEY' variable is checked first, followed by
    /// the legacy 'RESOURCE_KEY' and 'SUPER_RESOURCE_KEY' variables.
    /// </summary>
    /// <returns>
    /// The resource key, or null if no environment variable is set.
    /// </returns>
    public static string GetResourceKeyFromEnv()
    {
        var key = Environment.GetEnvironmentVariable(
            CLOUD_RESOURCE_KEY_ENV_VAR);
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Environment.GetEnvironmentVariable(
                LEGACY_CLOUD_RESOURCE_KEY_ENV_VAR);
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Environment.GetEnvironmentVariable(
                LEGACY_SUPER_RESOURCE_KEY_ENV_VAR);
        }
        return key;
    }

    /// <summary>
    /// Get the resource key from the environment and run the action with
    /// the value, or an empty string if no resource key is set. The
    /// aligned '_51DEGREES_RESOURCE_KEY' variable is checked first,
    /// followed by the legacy 'RESOURCE_KEY' and 'SUPER_RESOURCE_KEY'
    /// variables.
    /// </summary>
    /// <param name="setValue"></param>
    public static void GetResourceKeyFromEnv(Action<string> setValue)
    {
        var key = GetResourceKeyFromEnv();
        setValue(string.IsNullOrWhiteSpace(key) ? string.Empty : key);
    }
}
