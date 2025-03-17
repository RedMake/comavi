using System;
using System.Runtime.CompilerServices;

namespace COMAVI_SA.Utils
{
    /// <summary>
    /// Clase utilitaria que garantiza que ningún valor sea nulo.
    /// </summary>
    public static class NotNull
    {
        /// <summary>
        /// Valida que el valor proporcionado no sea nulo, lanzando una excepción si lo es.
        /// </summary>
        /// <typeparam name="T">Tipo del valor a validar</typeparam>
        /// <param name="value">Valor a validar</param>
        /// <param name="paramName">Nombre del parámetro (asignado automáticamente por el compilador)</param>
        /// <param name="message">Mensaje personalizado opcional</param>
        /// <returns>El valor original si no es nulo</returns>
        /// <exception cref="ArgumentNullException">Lanzada cuando el valor es nulo</exception>
        public static T Check<T>(T value, [CallerArgumentExpression("value")] string paramName = null, string message = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(
                    paramName,
                    message ?? $"El valor de '{paramName}' no puede ser nulo.");
            }
            return value;
        }

        /// <summary>
        /// Valida que la cadena proporcionada no sea nula ni vacía, lanzando una excepción si lo es.
        /// </summary>
        /// <param name="value">Cadena a validar</param>
        /// <param name="paramName">Nombre del parámetro (asignado automáticamente por el compilador)</param>
        /// <param name="message">Mensaje personalizado opcional</param>
        /// <returns>La cadena original si no es nula ni vacía</returns>
        /// <exception cref="ArgumentNullException">Lanzada cuando la cadena es nula</exception>
        /// <exception cref="ArgumentException">Lanzada cuando la cadena está vacía</exception>
        public static string CheckNotNullOrEmpty(string value, [CallerArgumentExpression("value")] string paramName = null, string message = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(
                    paramName,
                    message ?? $"La cadena '{paramName}' no puede ser nula.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    message ?? $"La cadena '{paramName}' no puede estar vacía o contener solo espacios.",
                    paramName);
            }

            return value;
        }
    }
}