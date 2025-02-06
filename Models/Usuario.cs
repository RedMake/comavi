using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace COMAVI_SA.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        [Required]
        [DisplayName("Nombre de Usuario")]
        public string NombreUsuario { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [PasswordPropertyText(true)]
        [DisplayName("Contraseña")]
        public string Contrasena { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime? ExpiracionToken { get; set; } = DateTime.Now;

        public string? TokenRecuperacion { get; set; } = string.Empty;


    }
}
