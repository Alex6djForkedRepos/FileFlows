using FileFlows.Server.Helpers;
using FileFlows.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FileFlows.Server.Authentication;

/// <summary>
/// FileFlows API authentication attribute
/// </summary>
public class FileFlowsApiAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>
    /// If this needs a valid Node UID to be called
    /// </summary>
    private readonly bool Node;

    /// <summary>
    /// Initialises a new instance of the attribute
    /// </summary>
    /// <param name="node">if this needs a valid Node UID to be caleld</param>
    public FileFlowsApiAuthorizeAttribute(bool node = true)
    {
        Node = node;
    }
    
    
    /// <summary>
    /// Handles the on on authorization
    /// </summary>
    /// <param name="context">the context</param>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (LicenseHelper.IsLicensed(LicenseFlags.UserSecurity) == false)
            return;
        
        var appSettings = ServiceLoader.Load<AppSettingsService>().Settings;
        if (appSettings.Security == SecurityMode.Off)
            return;

        var settings = await ServiceLoader.Load<SettingsService>().Get();
        
        var token = context.HttpContext.Request.Headers["x-token"].ToString();
        
        if(token != settings.ApiToken)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
    }
}