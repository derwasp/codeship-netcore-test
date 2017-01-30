using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace TestApp.Controllers.v1
{
    /// <summary>
    ///     Diagnostics/healthcheck controller.
    /// </summary>
    [Route("v1/[controller]")]
    public sealed class DiagnosticsController : Controller
    {
        /// <summary>
        ///     Used to healthcheck the service.
        /// </summary>
        /// <returns>
        ///     Service version in SemVer format.
        /// </returns>
        [HttpGet("")]
        [ProducesResponseType(typeof (string), 200)]
        public IActionResult Get()
        {
            var informationalVersion =
                this.GetType()
                .GetTypeInfo().Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            return this.Ok(informationalVersion.InformationalVersion);
        }
    }
}
