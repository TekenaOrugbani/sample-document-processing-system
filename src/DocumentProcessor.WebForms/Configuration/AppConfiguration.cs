using Microsoft.Extensions.Configuration;

namespace DocumentProcessor.WebForms.Configuration
{
    public static class AppConfiguration
    {
        private static IConfiguration? _configuration;

        public static void Configure(IConfiguration configuration) =>
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public static IConfiguration Current =>
            _configuration ?? throw new InvalidOperationException(
                "AppConfiguration.Configure was not called at startup.");
    }
}
