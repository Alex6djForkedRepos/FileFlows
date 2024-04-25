using System.Threading;
using System.Web;
using Microsoft.AspNetCore.Components;

namespace FileFlows.Client.Services;

/// <summary>
/// Profile service
/// </summary>
public class ProfileService
{
    /// <summary>
    /// Semaphore to ensure profile is only fetched once
    /// </summary>
    private SemaphoreSlim _semaphore = new(1);
    /// <summary>
    /// The cached profile
    /// </summary>
    private Profile _profile;

    private NavigationManager NavigationManager;
    private FFLocalStorageService LocalStorageService;
    /// <summary>
    /// Represents the method that handles the Refresh event.
    /// </summary>
    public delegate void OnRefreshDelegate();
    /// <summary>
    /// Occurs when the paused label changes.
    /// </summary>
    public event OnRefreshDelegate OnRefresh;

    public ProfileService(NavigationManager navigationManager, FFLocalStorageService localStorageService)
    {
        NavigationManager = navigationManager;
        LocalStorageService = localStorageService;
    }

    /// <summary>
    /// Gets the profile
    /// </summary>
    /// <returns>the users profile</returns>
    public async Task<Profile> Get()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_profile != null)
                return _profile;
            var result = await HttpHelper.Get<Profile>("/api/profile");
            if (result.Success == false)
                throw new UnauthorizedAccessException();

            _profile = result.Data;
            return _profile;
        }
        catch (Exception ex)
        {
#if(DEBUG)
            NavigationManager.NavigateTo("http://localhost:6868/login", true);
#else
            NavigationManager.NavigateTo("/login", true);
#endif
            return null;
            
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Refreshes the profile
    /// </summary>
    public async Task Refresh()
    {
        await _semaphore.WaitAsync();
        try
        {
            var result = await HttpHelper.Get<Profile>("/api/profile");
            if (result.Success == false)
            {
#if(DEBUG)
                NavigationManager.NavigateTo("http://localhost:6868/login", true);
#else
                NavigationManager.NavigateTo("/login", true);
#endif
                return;
            }
            var newProfile = result.Data;
            if (_profile == null)
            {
                _profile = newProfile;
                return;
            }

            _profile.ConfigurationStatus = newProfile.ConfigurationStatus;
            _profile.Uid = newProfile.Uid;
            _profile.Name = newProfile.Name;
            _profile.Language = newProfile.Language;
            _profile.License = newProfile.License;
            _profile.Role = newProfile.Role;
            _profile.Security = newProfile.Security;
            _profile.IsWebView = newProfile.IsWebView;
            _profile.UsersEnabled = newProfile.UsersEnabled;
            _profile.UnreadNotifications = newProfile.UnreadNotifications;
        }
        finally
        {
            _semaphore.Release();
        }
        OnRefresh?.Invoke();
    }

    /// <summary>
    /// Performs a logout
    /// </summary>
    /// <param name="message">Optional message to show on the login page</param>
    public async Task Logout(string? message = null)
    {
        await LocalStorageService.SetAccessToken(null);
        HttpHelper.Client.DefaultRequestHeaders.Authorization = null;
        string suffix = string.IsNullOrWhiteSpace(message) ? string.Empty : "?msg=" + HttpUtility.UrlEncode(message);
#if(DEBUG)
        NavigationManager.NavigateTo("http://localhost:6868/login" + suffix, true);
#else
        NavigationManager.NavigateTo("/login" + suffix, true);
#endif
    }

    /// <summary>
    /// Clears the access token
    /// </summary>
    public async Task ClearAccessToken()
    {
        await LocalStorageService.SetAccessToken(null);
        HttpHelper.Client.DefaultRequestHeaders.Authorization = null;
    }
}