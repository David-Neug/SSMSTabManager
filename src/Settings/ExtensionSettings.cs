using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SSMSTabManager
{
    [DataContract]
    internal sealed class ExtensionSettings
    {
        public const string FileName = "SSMSTabManager.json";
        private const string DefaultTargetDirectory = @"%USERPROFILE%\.ssmstabmanager";

        [DataMember(Name = "targetDirectory")]
        private string ConfiguredTargetDirectory { get; set; }

        public string TargetDirectory =>
            Path.GetFullPath(Environment.ExpandEnvironmentVariables(
                string.IsNullOrWhiteSpace(ConfiguredTargetDirectory) ? DefaultTargetDirectory : ConfiguredTargetDirectory.Trim()));

        public static string FilePath =>
            Path.Combine(Path.GetDirectoryName(typeof(ExtensionSettings).Assembly.Location), FileName);

        public static ExtensionSettings Load()
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                return new ExtensionSettings();
            }

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return (ExtensionSettings)new DataContractJsonSerializer(typeof(ExtensionSettings)).ReadObject(stream)
                        ?? new ExtensionSettings();
                }
            }
            catch (SerializationException ex)
            {
                throw new InvalidDataException("Neplatný soubor nastavení " + path + ":\n" + ex.Message, ex);
            }
        }
    }
}
