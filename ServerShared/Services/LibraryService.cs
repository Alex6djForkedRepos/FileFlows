﻿namespace FileFlows.ServerShared.Services;

using FileFlows.Shared.Helpers;
using FileFlows.Shared.Models;

/// <summary>
/// Interface for communicating with FileFlows server for libraries
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Gets a library by its UID
    /// </summary>
    /// <param name="uid">The UID of the library</param>
    /// <returns>An instance of the library if found</returns>
    Task<Library> Get(Guid uid);
}

/// <summary>
/// Service for communicating with FileFlows server for libraries
/// </summary>
public class LibraryService : Service, ILibraryService
{

    /// <summary>
    /// Gets or sets a function to load an instance of a ILibraryService
    /// </summary>
    public static Func<ILibraryService> Loader { get; set; }

    /// <summary>
    /// Loads an instance of the library service
    /// </summary>
    /// <returns>an instance of the library service</returns>
    public static ILibraryService Load()
    {
        if (Loader == null)
            return new LibraryService();
        return Loader.Invoke();
    }

    /// <summary>
    /// Gets a library by its UID
    /// </summary>
    /// <param name="uid">The UID of the library</param>
    /// <returns>An instance of the library if found</returns>
    public async Task<Library> Get(Guid uid)
    {
        try
        {
            var result = await HttpHelper.Get<Library>($"{ServiceBaseUrl}/api/library/" + uid.ToString());
            if (result.Success == false)
                throw new Exception("Failed to locate library: " + result.Body);
            return result.Data;
        }
        catch (Exception ex)
        {
            Logger.Instance?.WLog("Failed to get library: " + uid + " => " + ex.Message);
            return null;
        }
    }
}
