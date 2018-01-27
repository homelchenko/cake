// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Cake.Arguments
{
    internal sealed class ArgumentParser : IArgumentParser
    {
        private const string DefaultScriptFilePath = "./build.cake";

        private readonly ICakeLog _log;
        private readonly VerbosityParser _verbosityParser;

        public ArgumentParser(ICakeLog log, VerbosityParser parser)
        {
            _log = log;
            _verbosityParser = parser;
        }

        public CakeOptions Parse(IEnumerable<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var options = new CakeOptions();
            var isParsingOptions = false;

            var arguments = args.ToList();
            if (NoUserArguments(arguments))
            {
                return BuildDefaultOptions();
            }

            foreach (var argument in arguments)
            {
                var value = argument.UnQuote();

                if (isParsingOptions)
                {
                    if (IsOption(value))
                    {
                        if (!ParseOption(value, options))
                        {
                            options.HasError = true;
                            return options;
                        }
                    }
                    else
                    {
                        _log.Error("More than one build script specified.");
                        options.HasError = true;
                        return options;
                    }
                }
                else
                {
                    try
                    {
                        // If they didn't provide a specific build script, search for a default.
                        if (IsOption(argument))
                        {
                            // Make sure we parse the option
                            if (!ParseOption(value, options))
                            {
                                options.HasError = true;
                                return options;
                            }

                            options.Script = DefaultScriptFilePath;
                            continue;
                        }

                        // Quoted?
                        options.Script = new FilePath(value);
                    }
                    finally
                    {
                        // Start parsing options.
                        isParsingOptions = true;
                    }
                }
            }

            return options;
        }

        private static bool NoUserArguments(IList<string> arguments)
        {
            return arguments.Count == 0;
        }

        private CakeOptions BuildDefaultOptions()
        {
            var options = new CakeOptions();

            SetDefaultOptions(options);

            return options;
        }

        private void SetDefaultOptions(CakeOptions options)
        {
            SetDefaultScript(options);
        }

        private void SetDefaultScript(CakeOptions options)
        {
            options.Script = DefaultScriptFilePath;
        }

        private bool IsOption(string argument)
        {
            if (IsEmptyArgument(argument))
            {
                return false;
            }

            return IsSingleDashArgument(argument) || IsDoubleDashArgument(argument);
        }

        private bool IsEmptyArgument(string argument)
        {
            return string.IsNullOrWhiteSpace(argument);
        }

        private bool IsSingleDashArgument(string argument)
        {
            return argument.StartsWith("-");
        }

        private bool IsDoubleDashArgument(string argument)
        {
            return argument.StartsWith("--");
        }

        private bool ParseOption(string argument, CakeOptions options)
        {
            string name, value;

            var nameIndex = argument.StartsWith("--") ? 2 : 1;
            var separatorIndex = argument.IndexOfAny(new[] { '=' });
            if (separatorIndex < 0)
            {
                name = argument.Substring(nameIndex);
                value = string.Empty;
            }
            else
            {
                name = argument.Substring(nameIndex, separatorIndex - nameIndex);
                value = argument.Substring(separatorIndex + 1);
            }

            return ParseOption(name, value.UnQuote(), options);
        }

        private bool ParseOption(string name, string value, CakeOptions options)
        {
            if (IsVerbosityOption(name))
            {
                Verbosity verbosity;
                if (!_verbosityParser.TryParse(value, out verbosity))
                {
                    verbosity = Verbosity.Normal;
                    options.HasError = true;
                }
                options.Verbosity = verbosity;
            }

            if (IsShowDescriptionOption(name))
            {
                options.ShowDescription = ParseShowDescriptionOption(value);
            }

            if (IsPerformDryRunOption(name))
            {
                options.PerformDryRun = ParseBooleanValue(value);
            }

            if (name.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("?", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowHelp = ParseBooleanValue(value);
            }

            if (name.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ver", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowVersion = ParseBooleanValue(value);
            }

            if (name.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("d", StringComparison.OrdinalIgnoreCase))
            {
                options.PerformDebug = ParseBooleanValue(value);
            }

            if (name.Equals("mono", StringComparison.OrdinalIgnoreCase))
            {
                options.Mono = ParseBooleanValue(value);
            }

            if (name.Equals("bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                options.Bootstrap = ParseBooleanValue(value);
            }

            if (options.Arguments.ContainsKey(name))
            {
                _log.Error("Multiple arguments with the same name ({0}).", name);
                return false;
            }

            options.Arguments.Add(name, value);
            return true;
        }

        private bool IsPerformDryRunOption(string name)
        {
            return IsDryRunOption(name) || IsNoopOption(name) || IsWhatIfOption(name);
        }

        private bool IsDryRunOption(string name)
        {
            return name.Equals("dryrun", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsNoopOption(string name)
        {
            return name.Equals("noop", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsWhatIfOption(string name)
        {
            return name.Equals("whatif", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsVerbosityOption(string name)
        {
            return IsShortVerbosityOption(name) || IsLongVerbosityOption(name);
        }

        private bool IsShortVerbosityOption(string name)
        {
            return name.Equals("v", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLongVerbosityOption(string name)
        {
            return name.Equals("verbosity", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsShowDescriptionOption(string name)
        {
            return IsShortShowDescriptionOption(name) || IsLongShowDescriptionOption(name);
        }

        private bool IsShortShowDescriptionOption(string name)
        {
            return name.Equals("s", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLongShowDescriptionOption(string name)
        {
            return name.Equals("showdescription", StringComparison.OrdinalIgnoreCase);
        }

        private bool ParseShowDescriptionOption(string value)
        {
            return ParseBooleanValue(value);
        }

        private static bool ParseBooleanValue(string value)
        {
            value = (value ?? string.Empty).UnQuote();
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            throw new InvalidOperationException("Argument value is not a valid boolean value.");
        }
    }
}