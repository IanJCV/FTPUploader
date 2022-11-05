using FluentFTP;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.Security.Authentication;

namespace FTPUploader
{
    internal class FTPUploader
    {
        private static FTPUploader _uploader;

        private bool _override;
        private string _filePath, _ip, _username, _password;

        private static readonly string[] FILE_EXTENSIONS = new string[]
        {
            // Images
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".gif",

            // Video
            ".mp4",

            // Audio
            ".mp3",
            ".wav",
            ".ogg",

            // Documents
            ".doc",
            ".docx",
            ".pdf"
        };

        static void Main(string[] args)
        {

            if (args == null || args.Length == 0)
            {
                Console.WriteLine("Insufficient arguments provided!");
                Console.ReadKey();
                return;
            }

            _uploader = new();
            _uploader.Init(args);
            if (!_uploader._override) _uploader.Upload();
        }

        private void Init(string[] args)
        {
            _filePath = args[0] != null ? args[0] : throw new ApplicationException("Could not parse arguments!");

            if (args.ElementAtOrDefault(1) != null)
            {
                if (args[1] == "override")
                {
                    _override = true;

                    _ip = args[2] != null ? args[2] : throw new ApplicationException("Could not parse arguments!");
                    _username = args[3] != null ? args[3] : throw new ApplicationException("Could not parse arguments!");
                    _password = args[4] != null ? args[4] : throw new ApplicationException("Could not parse arguments!");

                    CheckOrModifyRegistry();
                    return;
                }

                _ip = args[1] != null ? args[1] : throw new ApplicationException("Could not parse arguments!");
                _username = args[2] != null ? args[2] : throw new ApplicationException("Could not parse arguments!");
                _password = args[3] != null ? args[3] : throw new ApplicationException("Could not parse arguments!");
            }
        }

        private void Upload()
        {
            _password = Base64Decode(_password);

            var client = new FtpClient(_ip, _username, _password);
            if (File.Exists($"{AppContext.BaseDirectory}config.json") && !_override)
            {
                var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText($"{AppContext.BaseDirectory}config.json"));
                client.LoadProfile(config.ConvertToProfile(_ip, _username, _password));
            }
            else
            {
                var profile = client.AutoDetect(firstOnly: true)[0];
                if (profile != null)
                {
                    client.LoadProfile(profile);

                    var c = Config.FromProfile(profile);
                    var config = JsonConvert.SerializeObject(c);
                    File.WriteAllText($"{AppContext.BaseDirectory}config.json", config);
                }
            }

            client.Config.ValidateAnyCertificate = true;
            //client.Config.LogToConsole = true;
            client.AutoConnect();

            string remotePath = Path.GetFileName(_filePath);

            client.UploadFile(_filePath, remotePath, FtpRemoteExists.Skip);

            client.Disconnect();
            client.Dispose();
        }

        private void CheckOrModifyRegistry()
        {
            foreach (var e in FILE_EXTENSIONS)
            {
                var path = $"SystemFileAssociations\\{e}\\Shell\\FTPUploader";

                if (_override == false && Registry.ClassesRoot.OpenSubKey(path + "\\command") != null)
                    continue;

                var baseKey = Registry.ClassesRoot.CreateSubKey(path);
                var commandKey = baseKey.CreateSubKey("command");

                baseKey.SetValue("", "Upload to FTP Server");
                commandKey.SetValue("", $"\"{AppContext.BaseDirectory}FTPUploader.exe\" \"%1\" {_ip} {_username} {Base64Encode(_password)}");
            }
        }

        public static string Base64Encode(string plainText)
        {
            if (plainText == null) throw new ArgumentNullException("Could not encode string!");

            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            if (base64EncodedData == null) throw new ArgumentNullException("Could not decode string!");

            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }

    [Serializable]
    public class Config
    {
        public FtpEncryptionMode encryption;
        public SslProtocols sslProtocols;
        public FtpDataConnectionType dataConnection;

        internal static Config FromProfile(FtpProfile profile)
        {
            var config = new Config();
            config.encryption = profile.Encryption;
            config.sslProtocols = profile.Protocols;
            config.dataConnection = profile.DataConnection;
            return config;
        }

        internal FtpProfile ConvertToProfile(string host, string username, string password)
        {
            return new FtpProfile()
            {
                Host = host,
                Credentials = new NetworkCredential(username, password),
                Encryption = encryption,
                Protocols = sslProtocols,
                DataConnection = dataConnection,
                Encoding = Encoding.UTF8
            };
        }
    }
}
