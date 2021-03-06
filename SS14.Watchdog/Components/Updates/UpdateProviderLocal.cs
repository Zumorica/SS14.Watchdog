using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SS14.Watchdog.Components.ServerManagement;
using SS14.Watchdog.Configuration.Updates;

namespace SS14.Watchdog.Components.Updates
{
    /// <summary>
    ///     Update provider that allows doing manual updates on local files as updating method.
    /// </summary>
    public sealed class UpdateProviderLocal : UpdateProvider
    {
        private readonly IServerInstance _serverInstance;
        private readonly UpdateProviderLocalConfiguration _specificConfiguration;
        private readonly ILogger<UpdateProviderLocal> _logger;
        private readonly IConfiguration _configuration;

        public UpdateProviderLocal(IServerInstance serverInstance,
            UpdateProviderLocalConfiguration specificConfiguration,
            ILogger<UpdateProviderLocal> logger,
            IConfiguration configuration)
        {
            _serverInstance = serverInstance;
            _specificConfiguration = specificConfiguration;
            _logger = logger;
            _configuration = configuration;
        }

        public override Task<bool> CheckForUpdateAsync(string? currentVersion, CancellationToken cancel = default)
        {
            return Task.FromResult(currentVersion != _specificConfiguration.CurrentVersion);
        }

        public override Task<RevisionDescription?> RunUpdateAsync(string? currentVersion, string binPath,
            CancellationToken cancel = default)
        {
            if (currentVersion == _specificConfiguration.CurrentVersion)
            {
                return Task.FromResult<RevisionDescription?>(null);
            }

            var binariesPath = Path.Combine(_serverInstance.InstanceDir, "binaries");
            if (!Directory.Exists(binariesPath))
            {
                throw new InvalidOperationException(
                    "Expected binaries/ directory containing all client binaries in the instance folder.");
            }

            var binariesRoot = new Uri(new Uri(_configuration["BaseUrl"]),
                $"instances/{_serverInstance.Key}/binaries/");

            DownloadInfoPair? GetInfoPair(string platform)
            {
                var fileName = GetBuildFilename(platform);
                var diskFileName = Path.Combine(binariesPath, fileName);

                if (!File.Exists(diskFileName))
                {
                    return null;
                }

                var download = new Uri(binariesRoot, fileName);
                var hash = GetFileHash(diskFileName);

                _logger.LogTrace("SHA256 hash for {fileName} is {hash}", fileName, hash);

                return new DownloadInfoPair(download.ToString(), hash);
            }

            var revisionDescription = new RevisionDescription(
                _specificConfiguration.CurrentVersion,
                GetInfoPair(PlatformNameWindows),
                GetInfoPair(PlatformNameLinux),
                GetInfoPair(PlatformNameMacOS));

            // ReSharper disable once RedundantTypeArgumentsOfMethod
            return Task.FromResult<RevisionDescription?>(revisionDescription);
        }

        private static string GetFileHash(string filePath)
        {
            using var file = File.OpenRead(filePath);
            using var sha = SHA256.Create();

            return ByteArrayToString(sha.ComputeHash(file));
        }

        // https://stackoverflow.com/a/311179/4678631
        private static string ByteArrayToString(byte[] ba)
        {
            var hex = new StringBuilder(ba.Length * 2);
            foreach (var b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }

            return hex.ToString();
        }
    }
}