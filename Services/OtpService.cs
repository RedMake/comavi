using OtpNet;
using System.Security.Cryptography;

namespace COMAVI_SA.Services
{
    public interface IOtpService
    {
        string GenerateSecret();
        string GenerateQrCodeUri(string secret, string email);
        bool VerifyOtp(string secret, string code);
        List<string> GenerateBackupCodes(int cantidad = 8);

    }

    public class OtpService : IOtpService
    {
        private readonly IConfiguration _configuration;

        public OtpService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateSecret()
        {
            var secretLength = _configuration.GetValue<int>("OtpSettings:SecretLength", 20);
            var key = KeyGeneration.GenerateRandomKey(secretLength);
            return Base32Encoding.ToString(key);
        }

        public string GenerateQrCodeUri(string secret, string email)
        {
            var issuer = "COMAVI_DockTrack";
            var encodedIssuer = Uri.EscapeDataString(issuer);
            var encodedEmail = Uri.EscapeDataString(email);

            // Formato URL de otpauth compatible con la mayoría de las aplicaciones TOTP
            return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
        }

        public bool VerifyOtp(string secret, string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code) || !long.TryParse(code, out _))
                {
                    return false;
                }

                var step = _configuration.GetValue<int>("OtpSettings:Step", 30);
                var digits = _configuration.GetValue<int>("OtpSettings:Digits", 6);

                var key = Base32Encoding.ToBytes(secret);

                var totp = new Totp(key, step: step, totpSize: digits, mode: OtpHashMode.Sha1);

                // Permitir un rango de tiempo para verificación (configurador para +/- 30 segundos)
                return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<string> GenerateBackupCodes(int cantidad = 8)
        {
            var backupCodes = new List<string>();

            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < cantidad; i++)
                {
                    var codeBytes = new byte[5]; // 10 caracteres hexadecimales (5 bytes)
                    rng.GetBytes(codeBytes);

                    // Convertir a hexadecimal y formatear como XXXXX-XXXXX
                    var codeHex = BitConverter.ToString(codeBytes).Replace("-", "");
                    var formattedCode = $"{codeHex.Substring(0, 5)}-{codeHex.Substring(5, 5)}";

                    backupCodes.Add(formattedCode);
                }
            }

            return backupCodes;
        }
    }
}