﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.IoTJourney.Logging;

namespace Microsoft.Practices.IoTJourney.ScenarioSimulator.ConsoleHost
{
    internal class Program
    {
        private static FileSystemWatcher _fileSystemWatcher;

        private static void Main(string[] args)
        {
            var observableEventListener = new ObservableEventListener();

            observableEventListener.EnableEvents(
              ScenarioSimulatorEventSource.Log, EventLevel.Informational);

            observableEventListener.LogToConsole();

            var configuration = SimulatorConfiguration.GetCurrentConfiguration();

            var deviceSimulator = new SimulationProfile("Console", 1, configuration);

            // check for scenario specified on the command line
            if (args.Length > 0)
            {
                var scenario = args.Contains("/default", StringComparer.OrdinalIgnoreCase)
                    ? SimulationScenarios.DefaultScenario()
                    : args.First(x => !x.StartsWith("/"));

                var ct = args.Contains("/webjob", StringComparer.OrdinalIgnoreCase)
                    ? GetWebJobCancellationToken()
                    : CancellationToken.None;

                deviceSimulator.RunSimulationAsync(scenario, ct).Wait(); // todo, use await after #138 is merged
                return;
            }

            // no command line arguments, so prompt with a menu
            var options = SimulationScenarios
                .AllScenarios
                .ToDictionary(
                    scenario => "Run " + scenario,
                    scenario => (Func<CancellationToken, Task>)(token => deviceSimulator.RunSimulationAsync(scenario, token)));

            Tests.Common.ConsoleHost.WithOptions(options);
        }
        
        private static CancellationToken GetWebJobCancellationToken()
        {
            // See: http://blog.amitapple.com/post/2014/05/webjobs-graceful-shutdown

            var shutdownFile = Environment.GetEnvironmentVariable("WEBJOBS_SHUTDOWN_FILE");
            var directory = Path.GetDirectoryName(shutdownFile);
            if (directory == null)
                return CancellationToken.None;

            var cts = new CancellationTokenSource();
            _fileSystemWatcher = new FileSystemWatcher(directory);
            _fileSystemWatcher.Created += (sender, args) =>
            {
                if (args.FullPath.Equals(Path.GetFullPath(shutdownFile), StringComparison.OrdinalIgnoreCase))
                    cts.Cancel();
            };

            _fileSystemWatcher.EnableRaisingEvents = true;
            return cts.Token;
        }
    }
}
